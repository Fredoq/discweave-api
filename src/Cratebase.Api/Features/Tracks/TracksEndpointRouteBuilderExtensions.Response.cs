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
        Credit[] trackCredits = await context.Credits.AsNoTracking()
            .Where(credit =>
                credit.CollectionId == collectionId &&
                EF.Property<TrackId?>(credit, "_targetTrackId") == track.Id)
            .ToArrayAsync(cancellationToken);
        ArtistId[] trackArtistIds = [.. trackCredits.Select(credit => credit.Contributor.ArtistId).Distinct()];
        Release[] appearanceReleases = await context.Releases.AsNoTracking()
            .Where(release =>
                release.CollectionId == collectionId &&
                release.Tracklist.Any(releaseTrack => releaseTrack.TrackId == track.Id))
            .ToArrayAsync(cancellationToken);
        List<Credit> releaseCredits = [];
        foreach (ReleaseId releaseId in appearanceReleases.Select(release => release.Id))
        {
            releaseCredits.AddRange(await context.Credits.AsNoTracking()
                .Where(credit =>
                    credit.CollectionId == collectionId &&
                    EF.Property<ReleaseId?>(credit, "_targetReleaseId") == releaseId)
                .ToArrayAsync(cancellationToken));
        }

        ArtistId[] releaseArtistIds = [.. releaseCredits.Select(credit => credit.Contributor.ArtistId).Distinct()];
        ArtistId[] artistIds = [.. trackArtistIds.Concat(releaseArtistIds).Distinct()];
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

        LabelId[] labelIds = [.. appearanceReleases.SelectMany(release => release.Labels).Select(label => label.LabelId).Distinct()];
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
