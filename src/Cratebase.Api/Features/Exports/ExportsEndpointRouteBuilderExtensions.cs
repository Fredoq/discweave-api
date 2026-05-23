using Cratebase.Api.Auth;
using Cratebase.Api.Features.Artists;
using Cratebase.Api.Features.Credits;
using Cratebase.Api.Features.Labels;
using Cratebase.Api.Features.Releases;
using Cratebase.Api.Features.Tracks;
using Cratebase.Application.Security;
using Cratebase.Domain.Catalog;
using Cratebase.Domain.Credits;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Api.Features.Exports;

public static partial class ExportsEndpointRouteBuilderExtensions
{
    private const int FormatVersion = 1;

    public static IEndpointRouteBuilder MapExportsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder group = endpoints.MapGroup("/api/exports")
            .WithTags("Exports")
            .RequireAuthorization(CratebaseAuthorizationPolicies.CollectionMember);

        _ = group.MapGet("/json", ExportJsonAsync).WithName("ExportCollectionJson");
        _ = group.MapGet("/csv", ExportCsvAsync).WithName("ExportCollectionCsv");

        return endpoints;
    }

    private static async Task<IResult> ExportJsonAsync(
        CratebaseDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        ExportSnapshotResponse snapshot = await BuildSnapshotAsync(
            context,
            currentCollection.CollectionId,
            cancellationToken);

        return Results.Ok(snapshot);
    }

    private static async Task<ExportSnapshotResponse> BuildSnapshotAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        CancellationToken cancellationToken)
    {
        Artist[] artists = await context.Artists.AsNoTracking()
            .Where(artist => artist.CollectionId == collectionId)
            .OrderBy(artist => artist.Name)
            .ToArrayAsync(cancellationToken);
        Label[] labels = await context.Labels.AsNoTracking()
            .Where(label => label.CollectionId == collectionId)
            .OrderBy(label => label.Name)
            .ToArrayAsync(cancellationToken);
        Release[] releases = await context.Releases.AsNoTracking()
            .Include("_genres")
            .Include("_tags")
            .Where(release => release.CollectionId == collectionId)
            .OrderBy(release => release.Summary.Title)
            .ToArrayAsync(cancellationToken);
        Track[] tracks = await context.Tracks.AsNoTracking()
            .Include("_genres")
            .Include("_tags")
            .Where(track => track.CollectionId == collectionId)
            .OrderBy(track => track.Title)
            .ToArrayAsync(cancellationToken);
        Credit[] credits = await context.Credits.AsNoTracking()
            .Where(credit => credit.CollectionId == collectionId)
            .ToArrayAsync(cancellationToken);
        Dictionary<ArtistId, Artist> artistsById = artists.ToDictionary(artist => artist.Id);
        Dictionary<LabelId, Label> labelsById = labels.ToDictionary(label => label.Id);
        Dictionary<TrackId, Track> tracksById = tracks.ToDictionary(track => track.Id);
        var releaseCreditsByReleaseId = credits
            .Where(credit => credit.Target is ReleaseCreditTarget)
            .GroupBy(credit => ((ReleaseCreditTarget)credit.Target).ReleaseId)
            .ToDictionary(group => group.Key, group => group.OrderBy(credit => credit.Id.Value).ToArray());
        var trackCreditsByTrackId = credits
            .Where(credit => credit.Target is TrackCreditTarget)
            .GroupBy(credit => ((TrackCreditTarget)credit.Target).TrackId)
            .ToDictionary(group => group.Key, group => group.OrderBy(credit => credit.Id.Value).ToArray());
        var appearancesByTrackId = releases
            .SelectMany(release => release.Tracklist.Select(releaseTrack => new TrackReleaseAppearance(release, releaseTrack)))
            .GroupBy(appearance => appearance.ReleaseTrack.TrackId)
            .ToDictionary(group => group.Key, group => group.ToArray());
        Credit[] orderedCredits = [.. credits.OrderBy(credit => credit.Id.Value)];

        return new ExportSnapshotResponse(
            FormatVersion,
            [.. artists.Select(ToArtistResponse)],
            [.. labels.Select(label => new LabelResponse(label.Id.Value, label.Name))],
            [.. releases.Select(release => ToReleaseResponse(release, releaseCreditsByReleaseId, trackCreditsByTrackId, artistsById, labelsById, tracksById))],
            [.. tracks.Select(track => ToTrackResponse(track, trackCreditsByTrackId, appearancesByTrackId, releaseCreditsByReleaseId, artistsById, labelsById))],
            await LoadOwnedItemsAsync(context, collectionId, cancellationToken),
            await LoadPlaylistsAsync(context, collectionId, cancellationToken),
            [.. orderedCredits.Select(CreditMapper.ToResponse)],
            await LoadArtistRelationsAsync(context, collectionId, cancellationToken),
            await LoadTrackRelationsAsync(context, collectionId, cancellationToken),
            await LoadDictionariesAsync(context, collectionId, cancellationToken),
            await LoadImportPatternsAsync(context, collectionId, cancellationToken),
            await LoadRatingCriteriaAsync(context, collectionId, cancellationToken),
            await LoadRatingsAsync(context, collectionId, cancellationToken));
    }

    private static ArtistResponse ToArtistResponse(Artist artist)
    {
        string type = artist switch
        {
            Person => "person",
            Group => "group",
            _ => throw new InvalidOperationException("Artist type is not supported")
        };

        return new ArtistResponse(artist.Id.Value, type, artist.Name);
    }

    private static ReleaseResponse ToReleaseResponse(
        Release release,
        IReadOnlyDictionary<ReleaseId, Credit[]> releaseCreditsByReleaseId,
        IReadOnlyDictionary<TrackId, Credit[]> trackCreditsByTrackId,
        IReadOnlyDictionary<ArtistId, Artist> artistsById,
        Dictionary<LabelId, Label> labelsById,
        Dictionary<TrackId, Track> tracksById)
    {
        ReleaseMetadata metadata = release.Summary.Metadata;
        Credit[] releaseCredits = releaseCreditsByReleaseId.GetValueOrDefault(release.Id) ?? [];

        return new ReleaseResponse(
            release.Id.Value,
            release.Summary.Title,
            metadata.Type,
            OptionalGuid(metadata.LabelId),
            OptionalInt(metadata.Year),
            OptionalDate(metadata.ReleaseDate),
            [.. release.Cataloging.Genres.Select(genre => genre.Name)],
            [.. release.Cataloging.Tags.Select(tag => tag.Name)],
            release.IsVariousArtists,
            release.IsNotOnLabel,
            ToCoverImageResponse(release),
            [.. releaseCredits.Select(credit => ToReleaseArtistCreditResponse(credit, artistsById))],
            [.. release.Labels.Select(label => ToReleaseLabelResponse(label, labelsById))],
            [.. release.Tracklist.OrderBy(track => track.Position.Number).Select(track => ToReleaseTracklistItemResponse(track, trackCreditsByTrackId, artistsById, tracksById))]);
    }

    private static TrackResponse ToTrackResponse(
        Track track,
        IReadOnlyDictionary<TrackId, Credit[]> trackCreditsByTrackId,
        IReadOnlyDictionary<TrackId, TrackReleaseAppearance[]> appearancesByTrackId,
        IReadOnlyDictionary<ReleaseId, Credit[]> releaseCreditsByReleaseId,
        IReadOnlyDictionary<ArtistId, Artist> artistsById,
        Dictionary<LabelId, Label> labelsById)
    {
        Credit[] trackCredits = trackCreditsByTrackId.GetValueOrDefault(track.Id) ?? [];
        TrackReleaseAppearance[] appearances = appearancesByTrackId.GetValueOrDefault(track.Id) ?? [];

        return new TrackResponse(
            track.Id.Value,
            track.Title,
            ToDurationSeconds(track),
            [.. track.Cataloging.Genres.Select(genre => genre.Name)],
            [.. track.Cataloging.Tags.Select(tag => tag.Name)],
            [.. trackCredits.Select(credit => ToTrackCreditResponse(credit, artistsById))],
            [.. appearances
                .Select(appearance => ToTrackReleaseAppearanceResponse(
                    appearance.Release,
                    appearance.ReleaseTrack,
                    track,
                    releaseCreditsByReleaseId,
                    artistsById,
                    labelsById))
                .OrderBy(appearance => appearance.ReleaseTitle)
                .ThenBy(appearance => appearance.Position)]);
    }

    private static ReleaseArtistCreditResponse ToReleaseArtistCreditResponse(Credit credit, IReadOnlyDictionary<ArtistId, Artist> artistsById)
    {
        ArtistId artistId = credit.Contributor.ArtistId;
        return new ReleaseArtistCreditResponse(
            artistId.Value,
            artistsById.TryGetValue(artistId, out Artist? artist) ? artist.Name : credit.Contributor.Name,
            CreditMapper.ToRoleCode(credit.Role));
    }

    private static TrackCreditResponse ToTrackCreditResponse(Credit credit, IReadOnlyDictionary<ArtistId, Artist> artistsById)
    {
        ArtistId artistId = credit.Contributor.ArtistId;
        return new TrackCreditResponse(
            artistId.Value,
            artistsById.TryGetValue(artistId, out Artist? artist) ? artist.Name : credit.Contributor.Name,
            CreditMapper.ToRoleCode(credit.Role));
    }

    private static ReleaseTracklistItemResponse ToReleaseTracklistItemResponse(
        ReleaseTrack releaseTrack,
        IReadOnlyDictionary<TrackId, Credit[]> trackCreditsByTrackId,
        IReadOnlyDictionary<ArtistId, Artist> artistsById,
        Dictionary<TrackId, Track> tracksById)
    {
        _ = tracksById.TryGetValue(releaseTrack.TrackId, out Track? track);
        Credit[] trackCredits = trackCreditsByTrackId.GetValueOrDefault(releaseTrack.TrackId) ?? [];

        return new ReleaseTracklistItemResponse(
            releaseTrack.TrackId.Value,
            track?.Title ?? "Unknown track",
            releaseTrack.Position.Number,
            track is null ? null : ToDurationSeconds(track),
            [.. trackCredits.Select(credit => ToReleaseArtistCreditResponse(credit, artistsById))],
            OptionalString(releaseTrack.VersionNote));
    }

    private static TrackReleaseAppearanceResponse ToTrackReleaseAppearanceResponse(
        Release release,
        ReleaseTrack releaseTrack,
        Track track,
        IReadOnlyDictionary<ReleaseId, Credit[]> releaseCreditsByReleaseId,
        IReadOnlyDictionary<ArtistId, Artist> artistsById,
        Dictionary<LabelId, Label> labelsById)
    {
        Credit[] releaseCredits = releaseCreditsByReleaseId.GetValueOrDefault(release.Id) ?? [];
        ReleaseLabel? releaseLabel = release.Labels.Count > 0 ? release.Labels[0] : null;

        return new TrackReleaseAppearanceResponse(
            release.Id.Value,
            release.Summary.Title,
            release.IsVariousArtists ? "Various Artists" : FormatReleaseArtists(releaseCredits, artistsById),
            OptionalInt(release.Summary.Metadata.Year),
            releaseLabel is null ? null : ToReleaseLabelResponse(releaseLabel, labelsById).Name,
            releaseTrack.Position.Number,
            ToDurationSeconds(track),
            OptionalString(releaseTrack.VersionNote));
    }

    private sealed record TrackReleaseAppearance(Release Release, ReleaseTrack ReleaseTrack);

    private static string FormatReleaseArtists(IReadOnlyList<Credit> credits, IReadOnlyDictionary<ArtistId, Artist> artistsById)
    {
        string[] artistNames =
        [
            .. credits
                .Where(credit => credit.Role == "mainArtist")
                .OrderBy(credit => credit.Contributor.ArtistId.Value)
                .Select(credit => artistsById.TryGetValue(credit.Contributor.ArtistId, out Artist? artist) ? artist.Name : credit.Contributor.Name)
        ];

        return artistNames.Length > 0 ? string.Join(", ", artistNames) : "Unknown artist";
    }

    private static ReleaseLabelResponse ToReleaseLabelResponse(ReleaseLabel releaseLabel, Dictionary<LabelId, Label> labelsById)
    {
        return new ReleaseLabelResponse(
            releaseLabel.LabelId.Value,
            labelsById.TryGetValue(releaseLabel.LabelId, out Label? label) ? label.Name : "Unknown label",
            OptionalString(releaseLabel.CatalogNumber),
            releaseLabel.HasNoCatalogNumber);
    }
}
