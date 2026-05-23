using Cratebase.Api.Features.ArtistRelations;
using Cratebase.Api.Features.OwnedItems;
using Cratebase.Api.Features.Playlists;
using Cratebase.Api.Features.Ratings;
using Cratebase.Api.Features.Releases;
using Cratebase.Api.Features.Settings;
using Cratebase.Api.Features.TrackRelations;
using Cratebase.Domain.Catalog;
using Cratebase.Domain.Imports;
using Cratebase.Domain.Settings;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Domain.SharedKernel.Optional;
using Cratebase.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace Cratebase.Api.Features.Exports;

public static partial class ExportsEndpointRouteBuilderExtensions
{
    private static async Task<IReadOnlyList<OwnedItemResponse>> LoadOwnedItemsAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        CancellationToken cancellationToken)
    {
        return
        [
            .. (await context.OwnedItems.AsNoTracking()
                .Where(item => item.CollectionId == collectionId)
                .ToArrayAsync(cancellationToken))
                .OrderBy(item => item.Id.Value)
                .Select(OwnedItemMapper.ToResponse)
        ];
    }

    private static async Task<IReadOnlyList<PlaylistResponse>> LoadPlaylistsAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        CancellationToken cancellationToken)
    {
        Domain.Playlists.Playlist[] playlists = await context.Playlists.AsNoTracking()
            .Include(playlist => playlist.Entries)
            .Where(playlist => playlist.CollectionId == collectionId)
            .OrderBy(playlist => playlist.Name)
            .ToArrayAsync(cancellationToken);
        List<PlaylistResponse> responses = new(playlists.Length);
        foreach (Domain.Playlists.Playlist playlist in playlists)
        {
            responses.Add(await PlaylistMapper.ToResponseAsync(playlist, context, cancellationToken));
        }

        return responses;
    }

    private static async Task<IReadOnlyList<ArtistRelationResponse>> LoadArtistRelationsAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        CancellationToken cancellationToken)
    {
        return
        [
            .. (await context.ArtistRelations.AsNoTracking()
                .Where(relation => relation.CollectionId == collectionId)
                .ToArrayAsync(cancellationToken))
                .OrderBy(relation => relation.Id.Value)
                .Select(ArtistRelationMapper.ToResponse)
        ];
    }

    private static async Task<IReadOnlyList<TrackRelationResponse>> LoadTrackRelationsAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        CancellationToken cancellationToken)
    {
        return
        [
            .. (await context.TrackRelations.AsNoTracking()
                .Where(relation => relation.CollectionId == collectionId)
                .ToArrayAsync(cancellationToken))
                .OrderBy(relation => relation.Id.Value)
                .Select(TrackRelationMapper.ToResponse)
        ];
    }

    private static async Task<IReadOnlyList<DictionaryEntryResponse>> LoadDictionariesAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        CancellationToken cancellationToken)
    {
        return
        [
            .. (await context.CollectionDictionaryEntries.AsNoTracking()
                .Where(entry => entry.CollectionId == collectionId)
                .OrderBy(entry => entry.Kind)
                .ThenBy(entry => entry.SortOrder)
                .ThenBy(entry => entry.Name)
                .ToArrayAsync(cancellationToken))
                .Select(ToDictionaryResponse)
        ];
    }

    private static async Task<IReadOnlyList<ImportPatternResponse>> LoadImportPatternsAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        CancellationToken cancellationToken)
    {
        return
        [
            .. (await context.ImportPatterns.AsNoTracking()
                .Where(pattern => pattern.CollectionId == collectionId)
                .OrderBy(pattern => pattern.Kind)
                .ThenBy(pattern => pattern.SortOrder)
                .ThenBy(pattern => pattern.Template)
                .ToArrayAsync(cancellationToken))
                .Select(ToImportPatternResponse)
        ];
    }

    private static async Task<IReadOnlyList<RatingCriterionResponse>> LoadRatingCriteriaAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        CancellationToken cancellationToken)
    {
        return
        [
            .. (await context.RatingCriteria.AsNoTracking()
                .Where(criterion => criterion.CollectionId == collectionId)
                .OrderBy(criterion => criterion.SortOrder)
                .ThenBy(criterion => criterion.Name)
                .ToArrayAsync(cancellationToken))
                .Select(RatingEndpointHelpers.ToCriterionResponse)
        ];
    }

    private static async Task<IReadOnlyList<RatingValueResponse>> LoadRatingsAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        CancellationToken cancellationToken)
    {
        return
        [
            .. (await context.RatingValues.AsNoTracking()
                .Where(rating => rating.CollectionId == collectionId)
                .ToArrayAsync(cancellationToken))
                .OrderBy(rating => rating.Id.Value)
                .Select(RatingEndpointHelpers.ToValueResponse)
        ];
    }

    private static CoverImageResponse? ToCoverImageResponse(Release release)
    {
        return release.Summary.Metadata.CoverImage is PresentOptionalValue<CoverImage> { Value: CoverImage coverImage }
            ? new CoverImageResponse(
                $"/api/releases/{release.Id.Value}/cover-image",
                coverImage.ContentType,
                coverImage.OriginalFileName,
                coverImage.SizeBytes,
                coverImage.SourceType)
            : null;
    }

    private static DictionaryEntryResponse ToDictionaryResponse(CollectionDictionaryEntry entry)
    {
        return new DictionaryEntryResponse(
            entry.Id.Value,
            DictionaryKindMapper.ToCode(entry.Kind),
            entry.Code,
            entry.Name,
            entry.SortOrder,
            entry.IsActive,
            entry.IsBuiltin,
            entry.IsProtected,
            OptionalString(entry.MediaProfile));
    }

    private static ImportPatternResponse ToImportPatternResponse(ImportPattern pattern)
    {
        return new ImportPatternResponse(
            pattern.Id.Value,
            ImportPatternKindMapper.ToCode(pattern.Kind),
            pattern.Template,
            pattern.SortOrder,
            pattern.IsActive,
            pattern.IsBuiltin);
    }

    private static int? ToDurationSeconds(Track track)
    {
        return track.Details.Duration.HasValue
            ? track.Details.Duration.Match(value => (int)value.TotalSeconds, () => 0)
            : null;
    }

    private static Guid? OptionalGuid(IOptionalValue<LabelId>? optional)
    {
        return optional is { HasValue: true } ? optional.Match(value => value.Value, () => Guid.Empty) : null;
    }

    private static int? OptionalInt(IOptionalValue<int>? optional)
    {
        return optional is { HasValue: true } ? optional.Match(value => value, () => 0) : null;
    }

    private static string? OptionalDate(IOptionalValue<DateOnly>? optional)
    {
        return optional is { HasValue: true } ? optional.Match(value => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), () => string.Empty) : null;
    }

    private static string? OptionalString(IOptionalValue<string>? optional)
    {
        return optional is { HasValue: true } ? optional.Match(value => value, () => string.Empty) : null;
    }
}
