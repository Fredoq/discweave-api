using Cratebase.Api.Features.Credits;
using Cratebase.Domain.Catalog;
using Cratebase.Domain.Credits;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Domain.SharedKernel.Optional;
using Cratebase.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Api.Features.Releases;

public static partial class ReleasesEndpointRouteBuilderExtensions
{
    private static async Task<ReleaseResponse> ToReleaseResponseAsync(
        Release release,
        CratebaseDbContext context,
        CollectionId collectionId,
        CancellationToken cancellationToken)
    {
        ReleaseMetadata metadata = release.Summary.Metadata;
        TrackId[] trackIds = [.. release.Tracklist.Select(track => track.TrackId)];
        Credit[] credits = await context.Credits.AsNoTracking()
            .Where(credit =>
                credit.CollectionId == collectionId &&
                EF.Property<ReleaseId?>(credit, "_targetReleaseId") == release.Id)
            .ToArrayAsync(cancellationToken);
        Credit[] releaseCredits = credits;
        List<Credit> trackCredits = await LoadTrackCreditsAsync(context, collectionId, trackIds, cancellationToken);
        ArtistId[] artistIds = [.. releaseCredits.Concat(trackCredits).Select(credit => credit.Contributor.ArtistId).Distinct()];
        Dictionary<ArtistId, Artist> artistsById = await LoadArtistsByIdAsync(context, collectionId, artistIds, cancellationToken);
        LabelId[] labelIds = [.. release.Labels.Select(label => label.LabelId)];
        Dictionary<LabelId, Label> labelsById = await LoadLabelsByIdAsync(context, collectionId, labelIds, cancellationToken);
        Dictionary<TrackId, Track> tracksById = await LoadTracksByIdAsync(context, collectionId, trackIds, cancellationToken);

        return new ReleaseResponse(
            release.Id.Value,
            release.Summary.Title,
            ToReleaseTypeCode(metadata.Type),
            metadata.LabelId.HasValue ? metadata.LabelId.Match(value => value.Value, () => Guid.Empty) : null,
            metadata.Year.HasValue ? metadata.Year.Match(value => value, () => 0) : null,
            [.. release.Cataloging.Genres.Select(genre => genre.Name)],
            [.. release.Cataloging.Tags.Select(tag => tag.Name)],
            release.IsVariousArtists,
            release.IsNotOnLabel,
            [.. releaseCredits.Select(credit => ToArtistCreditResponse(credit, artistsById))],
            [.. release.Labels.Select(label => ToReleaseLabelResponse(label, labelsById))],
            [.. release.Tracklist.OrderBy(track => track.Position.Number).Select(track => ToTracklistItemResponse(track, tracksById, trackCredits, artistsById))]);
    }

    private static async Task<List<Credit>> LoadTrackCreditsAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        IEnumerable<TrackId> trackIds,
        CancellationToken cancellationToken)
    {
        List<Credit> credits = [];
        foreach (TrackId trackId in trackIds)
        {
            credits.AddRange(await context.Credits.AsNoTracking()
                .Where(credit =>
                    credit.CollectionId == collectionId &&
                    EF.Property<TrackId?>(credit, "_targetTrackId") == trackId)
                .ToArrayAsync(cancellationToken));
        }

        return credits;
    }

    private static async Task<Dictionary<ArtistId, Artist>> LoadArtistsByIdAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        IEnumerable<ArtistId> artistIds,
        CancellationToken cancellationToken)
    {
        Dictionary<ArtistId, Artist> artistsById = [];
        foreach (ArtistId artistId in artistIds)
        {
            Artist? artist = await context.Artists.AsNoTracking()
                .FirstOrDefaultAsync(artist => artist.CollectionId == collectionId && artist.Id == artistId, cancellationToken);
            if (artist is not null)
            {
                artistsById[artist.Id] = artist;
            }
        }

        return artistsById;
    }

    private static async Task<Dictionary<LabelId, Label>> LoadLabelsByIdAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        IEnumerable<LabelId> labelIds,
        CancellationToken cancellationToken)
    {
        Dictionary<LabelId, Label> labelsById = [];
        foreach (LabelId labelId in labelIds)
        {
            Label? label = await context.Labels.AsNoTracking()
                .FirstOrDefaultAsync(label => label.CollectionId == collectionId && label.Id == labelId, cancellationToken);
            if (label is not null)
            {
                labelsById[label.Id] = label;
            }
        }

        return labelsById;
    }

    private static async Task<Dictionary<TrackId, Track>> LoadTracksByIdAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        IEnumerable<TrackId> trackIds,
        CancellationToken cancellationToken)
    {
        Dictionary<TrackId, Track> tracksById = [];
        foreach (TrackId trackId in trackIds)
        {
            Track? track = await context.Tracks.AsNoTracking()
                .FirstOrDefaultAsync(track => track.CollectionId == collectionId && track.Id == trackId, cancellationToken);
            if (track is not null)
            {
                tracksById[track.Id] = track;
            }
        }

        return tracksById;
    }

    private static ReleaseArtistCreditResponse ToArtistCreditResponse(Credit credit, IReadOnlyDictionary<ArtistId, Artist> artistsById)
    {
        ArtistId artistId = credit.Contributor.ArtistId;

        return new ReleaseArtistCreditResponse(
            artistId.Value,
            artistsById.TryGetValue(artistId, out Artist? artist) ? artist.Name : credit.Contributor.Name,
            CreditMapper.ToRoleCode(credit.Role));
    }

    private static ReleaseLabelResponse ToReleaseLabelResponse(ReleaseLabel releaseLabel, Dictionary<LabelId, Label> labelsById)
    {
        IOptionalValue<string>? catalogNumber = releaseLabel.CatalogNumber;

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
            durationSeconds,
            [.. credits.Select(credit => ToArtistCreditResponse(credit, artistsById))],
            releaseTrack.VersionNote is { HasValue: true } versionNote ? versionNote.Match(value => value, () => string.Empty) : null);
    }

    private static string ToReleaseTypeCode(ReleaseType type)
    {
        return type switch
        {
            ReleaseType.Unknown => "unknown",
            ReleaseType.Album => "album",
            ReleaseType.Ep => "ep",
            ReleaseType.Standalone => "standalone",
            ReleaseType.Compilation => "compilation",
            ReleaseType.Bootleg => "bootleg",
            ReleaseType.Mixtape => "mixtape",
            ReleaseType.Promo => "promo",
            ReleaseType.Other => OtherTypeCode,
            _ => throw new InvalidOperationException("Release type is not supported")
        };
    }
}
