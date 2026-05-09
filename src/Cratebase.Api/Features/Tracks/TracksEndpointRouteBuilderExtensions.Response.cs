using Cratebase.Api.Features.Credits;
using Cratebase.Domain.Catalog;
using Cratebase.Domain.Credits;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

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
        trackCredits =
        [
            .. trackCredits
                .OrderBy(credit => credit.Contributor.ArtistId.Value)
                .ThenBy(credit => CreditMapper.ToRoleCode(credit.Role))
        ];
        ArtistId[] trackArtistIds = [.. trackCredits.Select(credit => credit.Contributor.ArtistId).Distinct()];
        Release[] appearanceReleases = await context.Releases.AsNoTracking()
            .Where(release =>
                release.CollectionId == collectionId &&
                release.Tracklist.Any(releaseTrack => releaseTrack.TrackId == track.Id))
            .ToArrayAsync(cancellationToken);
        ReleaseId[] appearanceReleaseIds = [.. appearanceReleases.Select(release => release.Id).Distinct()];
        Credit[] releaseCredits = appearanceReleaseIds.Length == 0
            ? []
            :
            [
                .. (await context.Credits.AsNoTracking()
                .Where(credit =>
                    credit.CollectionId == collectionId)
                .Where(HasAnyTargetReleaseId(appearanceReleaseIds))
                .ToArrayAsync(cancellationToken))
                .OrderBy(credit => credit.Contributor.ArtistId.Value)
                .ThenBy(credit => CreditMapper.ToRoleCode(credit.Role))
            ];

        ArtistId[] releaseArtistIds = [.. releaseCredits.Select(credit => credit.Contributor.ArtistId).Distinct()];
        ArtistId[] artistIds = [.. trackArtistIds.Concat(releaseArtistIds).Distinct()];
        Dictionary<ArtistId, Artist> artistsById = artistIds.Length == 0
            ? []
            : await context.Artists.AsNoTracking()
                .Where(artist => artist.CollectionId == collectionId && artistIds.Contains(artist.Id))
                .ToDictionaryAsync(artist => artist.Id, cancellationToken);

        LabelId[] labelIds = [.. appearanceReleases.SelectMany(release => release.Labels).Select(label => label.LabelId).Distinct()];
        Dictionary<LabelId, Label> labelsById = labelIds.Length == 0
            ? []
            : await context.Labels.AsNoTracking()
                .Where(label => label.CollectionId == collectionId && labelIds.Contains(label.Id))
                .ToDictionaryAsync(label => label.Id, cancellationToken);

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

    private static Expression<Func<Credit, bool>> HasAnyTargetReleaseId(IReadOnlyCollection<ReleaseId> releaseIds)
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
                .OrderBy(credit => credit.Contributor.ArtistId.Value)
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
