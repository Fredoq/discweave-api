using DiscWeave.Api.Features.Credits;
using DiscWeave.Api.Features.ExternalSources;
using DiscWeave.Domain.Catalog;
using DiscWeave.Domain.Credits;
using DiscWeave.Domain.SharedKernel.Ids;
using DiscWeave.Domain.SharedKernel.Optional;
using DiscWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Linq.Expressions;

namespace DiscWeave.Api.Features.Releases;

public static partial class ReleasesEndpointRouteBuilderExtensions
{
    private const int CreditLookupBatchSize = 64;

    private static async Task<ReleaseResponse> ToReleaseResponseAsync(
        Release release,
        DiscWeaveDbContext context,
        CollectionId collectionId,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ReleaseResponse> responses = await ToReleaseResponsesAsync([release], context, collectionId, cancellationToken);
        return responses[0];
    }

    private static async Task<IReadOnlyList<ReleaseResponse>> ToReleaseResponsesAsync(
        Release[] releases,
        DiscWeaveDbContext context,
        CollectionId collectionId,
        CancellationToken cancellationToken)
    {
        if (releases.Length == 0)
        {
            return [];
        }

        ReleaseId[] releaseIds = [.. releases.Select(release => release.Id).Distinct()];
        TrackId[] trackIds = [.. releases.SelectMany(release => release.Tracklist).Select(track => track.TrackId).Distinct()];
        Credit[] releaseCredits = await LoadReleaseCreditsAsync(context, collectionId, releaseIds, cancellationToken);
        List<Credit> trackCredits = await LoadTrackCreditsAsync(context, collectionId, trackIds, cancellationToken);
        ArtistId[] artistIds = [.. releaseCredits.Concat(trackCredits).Select(credit => credit.Contributor.ArtistId).Distinct()];
        Dictionary<ArtistId, Artist> artistsById = await LoadArtistsByIdAsync(context, collectionId, artistIds, cancellationToken);
        LabelId[] labelIds = [.. releases.SelectMany(release => release.Labels).Select(label => label.LabelId).Distinct()];
        Dictionary<LabelId, Label> labelsById = await LoadLabelsByIdAsync(context, collectionId, labelIds, cancellationToken);
        Dictionary<TrackId, Track> tracksById = await LoadTracksByIdAsync(context, collectionId, trackIds, cancellationToken);

        return
        [
            .. releases.Select(release => ToReleaseResponse(
                release,
                releaseCredits,
                trackCredits,
                artistsById,
                labelsById,
                tracksById))
        ];
    }

    private static ReleaseResponse ToReleaseResponse(
        Release release,
        IReadOnlyList<Credit> releaseCredits,
        IReadOnlyList<Credit> trackCredits,
        Dictionary<ArtistId, Artist> artistsById,
        Dictionary<LabelId, Label> labelsById,
        Dictionary<TrackId, Track> tracksById)
    {
        ReleaseMetadata metadata = release.Summary.Metadata;
        Credit[] credits = [.. releaseCredits.Where(credit => credit.Target is ReleaseCreditTarget target && target.ReleaseId == release.Id)];

        return new ReleaseResponse(
            release.Id.Value,
            release.Summary.Title,
            metadata.Type,
            metadata.LabelId.HasValue ? metadata.LabelId.Match(value => value.Value, () => Guid.Empty) : null,
            metadata.Year.HasValue ? metadata.Year.Match(value => value, () => 0) : null,
            metadata.ReleaseDate.HasValue ? metadata.ReleaseDate.Match(value => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), () => string.Empty) : null,
            [.. release.Cataloging.Genres.Select(genre => genre.Name)],
            [.. release.Cataloging.Tags.Select(tag => tag.Name)],
            release.IsVariousArtists,
            release.IsNotOnLabel,
            ToCoverImageResponse(release),
            ExternalSourceReferenceMapper.ToResponses(release.ExternalSources),
            [.. credits.Select(credit => ToArtistCreditResponse(credit, artistsById))],
            [.. release.Labels.Select(label => ToReleaseLabelResponse(label, labelsById))],
            [.. release.Tracklist.OrderBy(track => track.Position.Number).Select(track => ToTracklistItemResponse(track, tracksById, trackCredits, artistsById))]);
    }

    private static CoverImageResponse? ToCoverImageResponse(Release release)
    {
        return release.Summary.Metadata.CoverImage is PresentOptionalValue<CoverImage> { Value: { } coverImage }
            ? new CoverImageResponse(
                $"/api/releases/{release.Id.Value}/cover-image",
                coverImage.ContentType,
                coverImage.OriginalFileName,
                coverImage.SizeBytes,
                coverImage.SourceType)
            : null;
    }

    private static async Task<Credit[]> LoadReleaseCreditsAsync(
        DiscWeaveDbContext context,
        CollectionId collectionId,
        ReleaseId[] releaseIds,
        CancellationToken cancellationToken)
    {
        if (releaseIds.Length == 0)
        {
            return [];
        }

        List<Credit> credits = [];
        foreach (ReleaseId[] batch in releaseIds.Chunk(CreditLookupBatchSize))
        {
            credits.AddRange(await context.Credits.AsNoTracking()
                .Where(credit => credit.CollectionId == collectionId)
                .Where(HasAnyTargetReleaseId(batch))
                .ToArrayAsync(cancellationToken));
        }

        return
        [
            .. credits
                .OrderBy(credit => credit.Contributor.ArtistId.Value)
                .ThenBy(credit => CreditMapper.ToRoleCode(credit.Role))
        ];
    }

    private static Expression<Func<Credit, bool>> HasAnyTargetReleaseId(ReleaseId[] releaseIds)
    {
        Expression<Func<Credit, ReleaseId?>> targetReleaseId = credit => EF.Property<ReleaseId?>(credit, "_targetReleaseId");
        Expression? body = null;

        foreach (ReleaseId releaseId in releaseIds)
        {
            BinaryExpression targetMatches = Expression.Equal(targetReleaseId.Body, Expression.Constant((ReleaseId?)releaseId, typeof(ReleaseId?)));
            body = body is null ? targetMatches : Expression.OrElse(body, targetMatches);
        }

        return Expression.Lambda<Func<Credit, bool>>(body ?? Expression.Constant(false), targetReleaseId.Parameters);
    }

    private static async Task<List<Credit>> LoadTrackCreditsAsync(
        DiscWeaveDbContext context,
        CollectionId collectionId,
        TrackId[] trackIds,
        CancellationToken cancellationToken)
    {
        if (trackIds.Length == 0)
        {
            return [];
        }

        List<Credit> credits = [];
        foreach (TrackId[] batch in trackIds.Chunk(CreditLookupBatchSize))
        {
            credits.AddRange(await context.Credits.AsNoTracking()
                .Where(credit => credit.CollectionId == collectionId)
                .Where(HasAnyTargetTrackId(batch))
                .ToArrayAsync(cancellationToken));
        }

        return
        [
            .. credits
                .OrderBy(credit => credit.Contributor.ArtistId.Value)
                .ThenBy(credit => CreditMapper.ToRoleCode(credit.Role))
        ];
    }

    private static Expression<Func<Credit, bool>> HasAnyTargetTrackId(TrackId[] trackIds)
    {
        Expression<Func<Credit, TrackId?>> targetTrackId = credit => EF.Property<TrackId?>(credit, "_targetTrackId");
        Expression? body = null;

        foreach (TrackId trackId in trackIds)
        {
            BinaryExpression targetMatches = Expression.Equal(targetTrackId.Body, Expression.Constant((TrackId?)trackId, typeof(TrackId?)));
            body = body is null ? targetMatches : Expression.OrElse(body, targetMatches);
        }

        return Expression.Lambda<Func<Credit, bool>>(body ?? Expression.Constant(false), targetTrackId.Parameters);
    }

    private static async Task<Dictionary<ArtistId, Artist>> LoadArtistsByIdAsync(
        DiscWeaveDbContext context,
        CollectionId collectionId,
        IEnumerable<ArtistId> artistIds,
        CancellationToken cancellationToken)
    {
        ArtistId[] ids = [.. artistIds.Distinct()];
        return ids.Length == 0
            ? []
            : await context.Artists.AsNoTracking()
            .Where(artist => artist.CollectionId == collectionId && ids.Contains(artist.Id))
            .ToDictionaryAsync(artist => artist.Id, cancellationToken);
    }

    private static async Task<Dictionary<LabelId, Label>> LoadLabelsByIdAsync(
        DiscWeaveDbContext context,
        CollectionId collectionId,
        IEnumerable<LabelId> labelIds,
        CancellationToken cancellationToken)
    {
        LabelId[] ids = [.. labelIds.Distinct()];
        return ids.Length == 0
            ? []
            : await context.Labels.AsNoTracking()
            .Where(label => label.CollectionId == collectionId && ids.Contains(label.Id))
            .ToDictionaryAsync(label => label.Id, cancellationToken);
    }

    private static async Task<Dictionary<TrackId, Track>> LoadTracksByIdAsync(
        DiscWeaveDbContext context,
        CollectionId collectionId,
        IEnumerable<TrackId> trackIds,
        CancellationToken cancellationToken)
    {
        TrackId[] ids = [.. trackIds.Distinct()];
        return ids.Length == 0
            ? []
            : await context.Tracks.AsNoTracking()
            .Where(track => track.CollectionId == collectionId && ids.Contains(track.Id))
            .ToDictionaryAsync(track => track.Id, cancellationToken);
    }

    private static ReleaseArtistCreditResponse ToArtistCreditResponse(Credit credit, IReadOnlyDictionary<ArtistId, Artist> artistsById)
    {
        ArtistId artistId = credit.Contributor.ArtistId;

        return new ReleaseArtistCreditResponse(
            artistId.Value,
            artistsById.TryGetValue(artistId, out Artist? artist) ? artist.Name : credit.Contributor.Name,
            CreditMapper.ToRoleCode(credit.Role),
            [.. credit.Roles.Select(CreditMapper.ToRoleCode)]);
    }

    private static ReleaseLabelResponse ToReleaseLabelResponse(ReleaseLabel releaseLabel, Dictionary<LabelId, Label> labelsById)
    {
        IOptionalValue<string> catalogNumber = releaseLabel.CatalogNumber;

        return new ReleaseLabelResponse(
            releaseLabel.LabelId.Value,
            labelsById.TryGetValue(releaseLabel.LabelId, out Label? label) ? label.Name : "Unknown label",
            catalogNumber is { HasValue: true } ? catalogNumber.Match(value => value, () => string.Empty) : null,
            releaseLabel.HasNoCatalogNumber);
    }

    private static ReleaseTracklistItemResponse ToTracklistItemResponse(
        ReleaseTrack releaseTrack,
        Dictionary<TrackId, Track> tracksById,
        IReadOnlyList<Credit> trackCredits,
        IReadOnlyDictionary<ArtistId, Artist> artistsById)
    {
        _ = tracksById.TryGetValue(releaseTrack.TrackId, out Track? track);
        Credit[] credits = [.. trackCredits.Where(credit => credit.Target is TrackCreditTarget target && target.TrackId == releaseTrack.TrackId)];
        int? durationSeconds = track is not null && track.Details.Duration.HasValue
            ? track.Details.Duration.Match(value => (int)value.TotalSeconds, () => 0)
            : null;

        return new ReleaseTracklistItemResponse(
            releaseTrack.TrackId.Value,
            track?.Title ?? "Unknown track",
            releaseTrack.Position.Number,
            OptionalString(releaseTrack.Position.Disc),
            OptionalString(releaseTrack.Position.Side),
            durationSeconds,
            [.. credits.Select(credit => ToArtistCreditResponse(credit, artistsById))],
            releaseTrack.VersionNote is { HasValue: true } versionNote ? versionNote.Match(value => value, () => string.Empty) : null);
    }

}
