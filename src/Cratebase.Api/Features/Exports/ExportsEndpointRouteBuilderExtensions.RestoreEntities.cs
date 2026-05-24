using Cratebase.Api.Features.ArtistRelations;
using Cratebase.Api.Features.Artists;
using Cratebase.Api.Features.Credits;
using Cratebase.Api.Features.Labels;
using Cratebase.Api.Features.OwnedItems;
using Cratebase.Api.Features.Ratings;
using Cratebase.Api.Features.Releases;
using Cratebase.Api.Features.Settings;
using Cratebase.Api.Features.Tracks;
using Cratebase.Api.Features.TrackRelations;
using Cratebase.Domain.Catalog;
using Cratebase.Domain.Collection;
using Cratebase.Domain.Credits;
using Cratebase.Domain.Imports;
using Cratebase.Domain.Ratings;
using Cratebase.Domain.Relations;
using Cratebase.Domain.Settings;
using Cratebase.Domain.SharedKernel.Errors;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Infrastructure.Persistence;

namespace Cratebase.Api.Features.Exports;

public static partial class ExportsEndpointRouteBuilderExtensions
{
    private static void RestoreDictionaries(
        CratebaseDbContext context,
        CollectionId collectionId,
        IReadOnlyList<DictionaryEntryResponse> entries)
    {
        foreach (DictionaryEntryResponse response in entries)
        {
            DictionaryKind kind = DictionaryKindMapper.Parse(response.Kind);
            CollectionDictionaryEntry entry = kind == DictionaryKind.MediaType
                ? CollectionDictionaryEntry.CreateMedia(
                    new CollectionDictionaryEntryId(response.Id),
                    collectionId,
                    response.Code,
                    response.Name,
                    response.SortOrder,
                    response.IsBuiltin,
                    response.MediaProfile ?? "other")
                : CollectionDictionaryEntry.Create(
                    new CollectionDictionaryEntryId(response.Id),
                    collectionId,
                    kind,
                    response.Code,
                    response.Name,
                    response.SortOrder,
                    response.IsBuiltin);
            if (!response.IsActive)
            {
                entry.Deactivate();
            }

            _ = context.CollectionDictionaryEntries.Add(entry);
        }
    }

    private static void RestoreRatingCriteria(
        CratebaseDbContext context,
        CollectionId collectionId,
        IReadOnlyList<RatingCriterionResponse> criteria)
    {
        foreach (RatingCriterionResponse response in criteria)
        {
            RatingTargetType[] targetTypes = [.. response.TargetTypes.Select(RatingTargetTypeCodes.FromCode)];
            RatingCriterion criterion = response.IsProtected
                ? RatingCriterion.CreateProtected(new RatingCriterionId(response.Id), collectionId, response.Code, response.Name, targetTypes, response.SortOrder)
                : RatingCriterion.Create(new RatingCriterionId(response.Id), collectionId, response.Code, response.Name, targetTypes, response.SortOrder);
            if (!response.IsActive)
            {
                criterion.Deactivate();
            }

            _ = context.RatingCriteria.Add(criterion);
        }
    }

    private static void RestoreImportPatterns(
        CratebaseDbContext context,
        CollectionId collectionId,
        IReadOnlyList<ImportPatternResponse> patterns)
    {
        foreach (ImportPatternResponse response in patterns)
        {
            var pattern = ImportPattern.Create(
                collectionId,
                new ImportPatternId(response.Id),
                ImportPatternKindMapper.Parse(response.Kind),
                response.Template,
                response.SortOrder,
                response.IsBuiltin);
            _ = context.ImportPatterns.Add(pattern);
            context.Entry(pattern).Property(item => item.IsActive).CurrentValue = response.IsActive;
        }
    }

    private static ArtistLookup RestoreArtists(
        CratebaseDbContext context,
        CollectionId collectionId,
        IReadOnlyList<ArtistResponse> artists)
    {
        var restored = new Dictionary<ArtistId, Artist>();
        foreach (ArtistResponse response in artists)
        {
            ArtistId artistId = new(response.Id);
            Artist artist = response.Type switch
            {
                "person" => Person.Create(collectionId, artistId, response.Name),
                "group" => Group.Create(collectionId, artistId, response.Name),
                _ => throw new DomainException("artist.type_invalid", "Artist type is invalid")
            };
            _ = context.Artists.Add(artist);
            restored.Add(artistId, artist);
        }

        return new ArtistLookup(restored);
    }

    private static void RestoreLabels(
        CratebaseDbContext context,
        CollectionId collectionId,
        IReadOnlyList<LabelResponse> labels)
    {
        foreach (LabelResponse response in labels)
        {
            _ = context.Labels.Add(Label.Create(collectionId, new LabelId(response.Id), response.Name));
        }
    }

    private static void RestoreTracks(
        CratebaseDbContext context,
        CollectionId collectionId,
        IReadOnlyList<TrackResponse> tracks)
    {
        foreach (TrackResponse response in tracks)
        {
            var track = Track.Create(collectionId, new TrackId(response.Id), response.Title);
            if (response.DurationSeconds is { } durationSeconds)
            {
                track.UpdateDetails(TrackDetails.Empty.WithDuration(TimeSpan.FromSeconds(durationSeconds)));
            }

            track.UpdateCataloging(ToCataloging(response.Genres, response.Tags));
            _ = context.Tracks.Add(track);
        }
    }

    private static void RestoreReleases(
        CratebaseDbContext context,
        CollectionId collectionId,
        IReadOnlyList<ReleaseResponse> releases)
    {
        foreach (ReleaseResponse response in releases)
        {
            var release = Release.Create(collectionId, new ReleaseId(response.Id), response.Title);
            release.UpdateSummary(ReleaseSummary.Create(response.Title).WithMetadata(ToReleaseMetadata(response)));
            release.UpdateCataloging(ToCataloging(response.Genres, response.Tags));
            release.UpdateArtistDisplay(response.IsVariousArtists);
            release.UpdateLabels(response.NotOnLabel, [.. response.Labels.Where(label => label.LabelId.HasValue).Select(ToReleaseLabel)]);
            release.ReplaceTracklist([.. response.Tracklist.Select(ToReleaseTrack)]);
            _ = context.Releases.Add(release);
        }
    }

    private static void RestoreOwnedItems(
        CratebaseDbContext context,
        CollectionId collectionId,
        IReadOnlyList<OwnedItemResponse> ownedItems)
    {
        foreach (OwnedItemResponse response in ownedItems)
        {
            IMedium medium = ToMedium(response.Medium);
            var item = OwnedItem.Create(
                collectionId,
                new OwnedItemId(response.Id),
                OwnedItemMapper.CreateTarget(response.TargetType, response.TargetId),
                OwnedItemMapper.ParseOwnershipStatus(response.Status),
                medium);
            item.UpdateHolding(OwnedItemMapper.CreateHolding(medium, response.Status, response.Condition, response.StorageLocation));
            _ = context.OwnedItems.Add(item);
        }
    }

    private static void RestoreCredits(
        CratebaseDbContext context,
        CollectionId collectionId,
        IReadOnlyList<CreditResponse> credits,
        ArtistLookup artists)
    {
        foreach (CreditResponse response in credits)
        {
            Artist artist = artists.Get(new ArtistId(response.ContributorArtistId));
            _ = context.Credits.Add(Credit.Create(
                collectionId,
                new CreditId(response.Id),
                CreditContributor.FromArtist(artist),
                ToCreditTarget(response.TargetType, response.TargetId),
                response.Role));
        }
    }

    private static void RestoreArtistRelations(
        CratebaseDbContext context,
        CollectionId collectionId,
        IReadOnlyList<ArtistRelationResponse> relations)
    {
        foreach (ArtistRelationResponse response in relations)
        {
            ArtistRelation relation = response.StartYear.HasValue || response.EndYear.HasValue
                ? ArtistRelation.Create(
                    new ArtistRelationId(response.Id),
                    collectionId,
                    new ArtistId(response.SourceArtistId),
                    new ArtistId(response.TargetArtistId),
                    response.Type,
                    ToRelationPeriod(response.StartYear, response.EndYear))
                : ArtistRelation.Create(
                    new ArtistRelationId(response.Id),
                    collectionId,
                    new ArtistId(response.SourceArtistId),
                    new ArtistId(response.TargetArtistId),
                    response.Type);
            _ = context.ArtistRelations.Add(relation);
        }
    }

    private static void RestoreTrackRelations(
        CratebaseDbContext context,
        CollectionId collectionId,
        IReadOnlyList<TrackRelationResponse> relations)
    {
        foreach (TrackRelationResponse response in relations)
        {
            _ = context.TrackRelations.Add(TrackRelation.Create(
                new TrackRelationId(response.Id),
                collectionId,
                new TrackId(response.SourceTrackId),
                new TrackId(response.TargetTrackId),
                response.Type));
        }
    }
}
