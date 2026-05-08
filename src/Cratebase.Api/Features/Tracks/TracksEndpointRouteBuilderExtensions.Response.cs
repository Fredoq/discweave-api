using Cratebase.Api.Features.Credits;
using Cratebase.Domain.Catalog;
using Cratebase.Domain.Credits;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Api.Features.Tracks;

public static partial class TracksEndpointRouteBuilderExtensions
{
    private static async Task<TrackResponse> ToTrackResponseAsync(
        Track track,
        CratebaseDbContext context,
        CollectionId collectionId,
        CancellationToken cancellationToken)
    {
        Credit[] credits = await context.Credits.AsNoTracking()
            .Where(credit => credit.CollectionId == collectionId)
            .ToArrayAsync(cancellationToken);
        Credit[] trackCredits = [.. credits.Where(credit => credit.Target is TrackCreditTarget target && target.TrackId == track.Id)];
        ArtistId[] trackArtistIds = [.. trackCredits.Select(credit => credit.Contributor.ArtistId).Distinct()];
        Release[] releases = await context.Releases.AsNoTracking()
            .Where(release => release.CollectionId == collectionId)
            .ToArrayAsync(cancellationToken);
        Release[] appearanceReleases = [.. releases.Where(release => release.Tracklist.Any(releaseTrack => releaseTrack.TrackId == track.Id))];
        ReleaseId[] appearanceReleaseIds = [.. appearanceReleases.Select(release => release.Id)];
        Credit[] releaseCredits = [.. credits.Where(credit => credit.Target is ReleaseCreditTarget target && appearanceReleaseIds.Contains(target.ReleaseId))];
        ArtistId[] releaseArtistIds = [.. releaseCredits.Select(credit => credit.Contributor.ArtistId).Distinct()];
        ArtistId[] artistIds = [.. trackArtistIds.Concat(releaseArtistIds).Distinct()];
        var artistsById = (await context.Artists.AsNoTracking()
                .Where(artist => artist.CollectionId == collectionId)
                .ToArrayAsync(cancellationToken))
            .Where(artist => artistIds.Contains(artist.Id))
            .ToDictionary(artist => artist.Id);
        LabelId[] labelIds = [.. appearanceReleases.SelectMany(release => release.Labels).Select(label => label.LabelId).Distinct()];
        var labelsById = (await context.Labels.AsNoTracking()
                .Where(label => label.CollectionId == collectionId)
                .ToArrayAsync(cancellationToken))
            .Where(label => labelIds.Contains(label.Id))
            .ToDictionary(label => label.Id);

        return new TrackResponse(
            track.Id.Value,
            track.Title,
            ToDurationSeconds(track),
            [.. track.Cataloging.Genres.Select(genre => genre.Name)],
            [.. track.Cataloging.Tags.Select(tag => tag.Name)],
            [.. trackCredits.Select(credit => ToTrackCreditResponse(credit, artistsById))],
            [.. appearanceReleases
                .SelectMany(release => release.Tracklist
                    .Where(releaseTrack => releaseTrack.TrackId == track.Id)
                    .Select(releaseTrack => ToReleaseAppearanceResponse(release, releaseTrack, track, releaseCredits, artistsById, labelsById)))
                .OrderBy(appearance => appearance.ReleaseTitle)
                .ThenBy(appearance => appearance.Position)]);
    }

    private static TrackCreditResponse ToTrackCreditResponse(Credit credit, Dictionary<ArtistId, Artist> artistsById)
    {
        ArtistId artistId = credit.Contributor.ArtistId;

        return new TrackCreditResponse(
            artistId.Value,
            artistsById.TryGetValue(artistId, out Artist? artist) ? artist.Name : credit.Contributor.Name,
            CreditMapper.ToRoleCode(credit.Role));
    }

    private static TrackReleaseAppearanceResponse ToReleaseAppearanceResponse(
        Release release,
        ReleaseTrack releaseTrack,
        Track track,
        IReadOnlyList<Credit> releaseCredits,
        Dictionary<ArtistId, Artist> artistsById,
        Dictionary<LabelId, Label> labelsById)
    {
        Credit[] credits = [.. releaseCredits.Where(credit => credit.Target is ReleaseCreditTarget target && target.ReleaseId == release.Id)];
        string releaseArtist = release.IsVariousArtists
            ? "Various Artists"
            : FormatReleaseArtists(credits, artistsById);
        ReleaseLabel? releaseLabel = release.Labels.Count > 0 ? release.Labels[0] : null;

        return new TrackReleaseAppearanceResponse(
            release.Id.Value,
            release.Summary.Title,
            releaseArtist,
            ToReleaseYear(release),
            releaseLabel is not null ? FormatLabel(releaseLabel, labelsById) : null,
            releaseTrack.Position.Number,
            ToDurationSeconds(track),
            releaseTrack.VersionNote is { HasValue: true } versionNote ? versionNote.Match(value => value, () => string.Empty) : null);
    }

    private static string FormatReleaseArtists(IReadOnlyList<Credit> releaseCredits, Dictionary<ArtistId, Artist> artistsById)
    {
        string[] artistNames =
        [
            .. releaseCredits
                .Where(credit => credit.Role == CreditRole.MainArtist)
                .Select(credit => artistsById.TryGetValue(credit.Contributor.ArtistId, out Artist? artist) ? artist.Name : credit.Contributor.Name)
        ];

        return artistNames.Length > 0 ? string.Join(", ", artistNames) : "Unknown artist";
    }

    private static string FormatLabel(ReleaseLabel releaseLabel, Dictionary<LabelId, Label> labelsById)
    {
        return labelsById.TryGetValue(releaseLabel.LabelId, out Label? label)
            ? label.Name
            : "Unknown label";
    }

    private static int? ToDurationSeconds(Track track)
    {
        return track.Details.Duration.HasValue
            ? track.Details.Duration.Match(value => (int)value.TotalSeconds, () => 0)
            : null;
    }

    private static int? ToReleaseYear(Release release)
    {
        return release.Summary.Metadata.Year.HasValue
            ? release.Summary.Metadata.Year.Match(value => value, () => 0)
            : null;
    }
}
