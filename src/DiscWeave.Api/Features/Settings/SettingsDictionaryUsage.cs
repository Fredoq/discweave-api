using DiscWeave.Domain.Catalog;
using DiscWeave.Domain.Collection;
using DiscWeave.Domain.Credits;
using DiscWeave.Domain.Relations;
using DiscWeave.Domain.Settings;
using DiscWeave.Domain.SharedKernel.Optional;
using DiscWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DiscWeave.Api.Features.Settings;

public static partial class SettingsDictionariesEndpointRouteBuilderExtensions
{
    private const string GenresNavigation = "_genres";

    private static async Task<bool> IsEntryUsedAsync(
        DiscWeaveDbContext context,
        CollectionDictionaryEntry entry,
        CancellationToken cancellationToken)
    {
        return entry.Kind switch
        {
            DictionaryKind.ReleaseType => await context.Releases.AnyAsync(release => release.CollectionId == entry.CollectionId && release.Summary.Metadata.Type == entry.Code, cancellationToken),
            DictionaryKind.CreditRole => await IsCreditRoleUsedAsync(context, entry, cancellationToken),
            DictionaryKind.MediaType => await context.OwnedItems.AnyAsync(item => item.CollectionId == entry.CollectionId && EF.Property<string>(item, "_mediumType") == entry.Code, cancellationToken),
            DictionaryKind.ArtistRelationType => await context.ArtistRelations.AnyAsync(relation => relation.CollectionId == entry.CollectionId && relation.Type == entry.Code, cancellationToken),
            DictionaryKind.TrackRelationType => await context.TrackRelations.AnyAsync(relation => relation.CollectionId == entry.CollectionId && relation.RelationType == entry.Code, cancellationToken),
            DictionaryKind.Genre => await IsGenreUsedAsync(context, entry, cancellationToken),
            _ => false
        };
    }

    private static async Task<bool> IsCreditRoleUsedAsync(
        DiscWeaveDbContext context,
        CollectionDictionaryEntry entry,
        CancellationToken cancellationToken)
    {
        Credit[] credits = await context.Credits.AsNoTracking()
            .Where(credit => credit.CollectionId == entry.CollectionId)
            .ToArrayAsync(cancellationToken);

        return credits.Any(credit => credit.Roles.Contains(entry.Code, StringComparer.Ordinal));
    }

    private static async Task<bool> IsGenreUsedAsync(
        DiscWeaveDbContext context,
        CollectionDictionaryEntry entry,
        CancellationToken cancellationToken)
    {
        Release[] releases = await context.Releases.AsNoTracking()
            .Include(GenresNavigation)
            .Where(release => release.CollectionId == entry.CollectionId)
            .ToArrayAsync(cancellationToken);
        if (releases.Any(release => release.Cataloging.Genres.Any(genre => genre.Name == entry.Code)))
        {
            return true;
        }

        Track[] tracks = await context.Tracks.AsNoTracking()
            .Include(GenresNavigation)
            .Where(track => track.CollectionId == entry.CollectionId)
            .ToArrayAsync(cancellationToken);

        return tracks.Any(track => track.Cataloging.Genres.Any(genre => genre.Name == entry.Code));
    }

    private static async Task ReplaceUsagesAsync(
        DiscWeaveDbContext context,
        CollectionDictionaryEntry entry,
        string replacementCode,
        CancellationToken cancellationToken)
    {
        switch (entry.Kind)
        {
            case DictionaryKind.ReleaseType:
                await ReplaceReleaseTypeUsagesAsync(context, entry, replacementCode, cancellationToken);
                break;
            case DictionaryKind.CreditRole:
                await ReplaceCreditRoleUsagesAsync(context, entry, replacementCode, cancellationToken);
                break;
            case DictionaryKind.MediaType:
                await ReplaceMediaTypeUsagesAsync(context, entry, replacementCode, cancellationToken);
                break;
            case DictionaryKind.ArtistRelationType:
                await ReplaceArtistRelationTypeUsagesAsync(context, entry, replacementCode, cancellationToken);
                break;
            case DictionaryKind.TrackRelationType:
                await ReplaceTrackRelationTypeUsagesAsync(context, entry, replacementCode, cancellationToken);
                break;
            case DictionaryKind.Genre:
                await ReplaceGenreUsagesAsync(context, entry, replacementCode, cancellationToken);
                break;
            default:
                throw new InvalidOperationException("Dictionary kind is not supported");
        }
    }

    private static async Task ReplaceReleaseTypeUsagesAsync(
        DiscWeaveDbContext context,
        CollectionDictionaryEntry entry,
        string replacementCode,
        CancellationToken cancellationToken)
    {
        Release[] releases = await context.Releases
            .Where(release => release.CollectionId == entry.CollectionId && release.Summary.Metadata.Type == entry.Code)
            .ToArrayAsync(cancellationToken);
        foreach (Release release in releases)
        {
            release.UpdateSummary(release.Summary.WithMetadata(release.Summary.Metadata.WithType(replacementCode)));
        }
    }

    private static async Task ReplaceCreditRoleUsagesAsync(
        DiscWeaveDbContext context,
        CollectionDictionaryEntry entry,
        string replacementCode,
        CancellationToken cancellationToken)
    {
        Credit[] credits = await context.Credits
            .Where(credit => credit.CollectionId == entry.CollectionId)
            .ToArrayAsync(cancellationToken);
        foreach (Credit credit in credits.Where(credit => credit.Roles.Contains(entry.Code, StringComparer.Ordinal)))
        {
            credit.ReplaceRole(entry.Code, replacementCode);
        }
    }

    private static async Task ReplaceMediaTypeUsagesAsync(
        DiscWeaveDbContext context,
        CollectionDictionaryEntry entry,
        string replacementCode,
        CancellationToken cancellationToken)
    {
        OwnedItem[] ownedItems = await context.OwnedItems
            .Where(item => item.CollectionId == entry.CollectionId && EF.Property<string>(item, "_mediumType") == entry.Code)
            .ToArrayAsync(cancellationToken);
        foreach (OwnedItem item in ownedItems)
        {
            item.UpdateHolding(OwnedItemHolding.Create(item.Holding.Status, RecodeMedium(item.Holding.Medium, replacementCode)).WithDetails(item.Holding.Details));
        }
    }

    private static IMedium RecodeMedium(IMedium medium, string replacementCode)
    {
        return medium switch
        {
            DigitalFile digitalFile when digitalFile.ImportIdentity is PresentOptionalValue<FileImportIdentity> identity =>
                DigitalFile.Create(replacementCode, digitalFile.Path, digitalFile.Format, identity.Value),
            DigitalFile digitalFile => DigitalFile.Create(replacementCode, digitalFile.Path, digitalFile.Format),
            VinylRecord vinylRecord => VinylRecord.Create(replacementCode, vinylRecord.FormatDescription),
            CompactDisc compactDisc => CompactDisc.Create(replacementCode, compactDisc.DiscCount),
            CassetteTape cassetteTape => CassetteTape.Create(replacementCode, cassetteTape.TapeType),
            OtherMedium otherMedium => OtherMedium.Create(replacementCode, otherMedium.Name),
            _ => throw new InvalidOperationException("Medium type is not supported")
        };
    }

    private static async Task ReplaceArtistRelationTypeUsagesAsync(
        DiscWeaveDbContext context,
        CollectionDictionaryEntry entry,
        string replacementCode,
        CancellationToken cancellationToken)
    {
        ArtistRelation[] relations = await context.ArtistRelations
            .Where(relation => relation.CollectionId == entry.CollectionId && relation.Type == entry.Code)
            .ToArrayAsync(cancellationToken);
        foreach (ArtistRelation relation in relations)
        {
            if (relation.Period is PresentOptionalValue<ArtistRelationPeriod> period)
            {
                relation.Update(relation.SourceArtistId, relation.TargetArtistId, replacementCode, period.Value);
            }
            else
            {
                relation.Update(relation.SourceArtistId, relation.TargetArtistId, replacementCode);
            }
        }
    }

    private static async Task ReplaceTrackRelationTypeUsagesAsync(
        DiscWeaveDbContext context,
        CollectionDictionaryEntry entry,
        string replacementCode,
        CancellationToken cancellationToken)
    {
        TrackRelation[] relations = await context.TrackRelations
            .Where(relation => relation.CollectionId == entry.CollectionId && relation.RelationType == entry.Code)
            .ToArrayAsync(cancellationToken);
        foreach (TrackRelation relation in relations)
        {
            relation.Update(relation.SourceTrackId, relation.TargetTrackId, replacementCode);
        }
    }

    private static async Task ReplaceGenreUsagesAsync(
        DiscWeaveDbContext context,
        CollectionDictionaryEntry entry,
        string replacementCode,
        CancellationToken cancellationToken)
    {
        Release[] releases = await context.Releases
            .Include(GenresNavigation)
            .Include("_tags")
            .Where(release => release.CollectionId == entry.CollectionId)
            .ToArrayAsync(cancellationToken);
        foreach (Release release in releases.Where(release => release.Cataloging.Genres.Any(genre => genre.Name == entry.Code)))
        {
            release.UpdateCataloging(ReplaceGenre(release.Cataloging, entry.Code, replacementCode));
        }

        Track[] tracks = await context.Tracks
            .Include(GenresNavigation)
            .Include("_tags")
            .Where(track => track.CollectionId == entry.CollectionId)
            .ToArrayAsync(cancellationToken);
        foreach (Track track in tracks.Where(track => track.Cataloging.Genres.Any(genre => genre.Name == entry.Code)))
        {
            track.UpdateCataloging(ReplaceGenre(track.Cataloging, entry.Code, replacementCode));
        }
    }

    private static Cataloging ReplaceGenre(Cataloging cataloging, string oldCode, string replacementCode)
    {
        Cataloging updated = Cataloging.Empty;
        foreach (string genreName in cataloging.Genres.Select(genre => genre.Name))
        {
            updated = updated.WithGenre(Genre.FromName(genreName == oldCode ? replacementCode : genreName));
        }

        foreach (Tag tag in cataloging.Tags)
        {
            updated = updated.WithTag(tag);
        }

        return updated;
    }
}
