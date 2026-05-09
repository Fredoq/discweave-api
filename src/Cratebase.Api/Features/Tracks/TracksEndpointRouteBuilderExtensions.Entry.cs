using Cratebase.Api.Features.Credits;
using Cratebase.Domain.Catalog;
using Cratebase.Domain.Credits;
using Cratebase.Domain.SharedKernel.Errors;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Domain.SharedKernel.Optional;
using Cratebase.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Api.Features.Tracks;

public static partial class TracksEndpointRouteBuilderExtensions
{
    private static async Task ReplaceTrackCreditsAsync(
        Track track,
        IReadOnlyList<TrackCreditRequest> creditRequests,
        CratebaseDbContext context,
        CollectionId collectionId,
        CancellationToken cancellationToken)
    {
        Credit[] existingCredits = await context.Credits
            .Where(credit =>
                credit.CollectionId == collectionId &&
                EF.Property<TrackId?>(credit, "_targetTrackId") == track.Id)
            .ToArrayAsync(cancellationToken);
        context.Credits.RemoveRange(existingCredits);

        foreach (ResolvedTrackCredit resolved in await ResolveTrackCreditsAsync(creditRequests, context, collectionId, cancellationToken))
        {
            _ = context.Credits.Add(Credit.Create(
                collectionId,
                CreditId.New(),
                CreditContributor.FromArtist(resolved.Artist),
                CreditTarget.ForTrack(track.Id),
                resolved.Role));
        }
    }

    private static async Task ReplaceTrackAppearancesAsync(
        Track track,
        IReadOnlyList<TrackReleaseAppearanceRequest> appearanceRequests,
        CratebaseDbContext context,
        CollectionId collectionId,
        CancellationToken cancellationToken)
    {
        ReleaseId[] requestedReleaseIds = [.. appearanceRequests.Select(request => new ReleaseId(request.ReleaseId)).Distinct()];
        Release[] releases = await context.Releases
            .Where(release =>
                release.CollectionId == collectionId &&
                (release.Tracklist.Any(releaseTrack => releaseTrack.TrackId == track.Id) ||
                    requestedReleaseIds.Contains(release.Id)))
            .ToArrayAsync(cancellationToken);
        Dictionary<ReleaseId, Release> releasesById = releases.ToDictionary(release => release.Id);
        var requestedByRelease = new Dictionary<ReleaseId, TrackReleaseAppearanceRequest>();

        foreach (TrackReleaseAppearanceRequest request in appearanceRequests)
        {
            ReleaseId releaseId = new(request.ReleaseId);
            if (!releasesById.ContainsKey(releaseId))
            {
                throw new DomainException("track.release_conflict", "Track release appearance does not exist");
            }

            requestedByRelease.Add(releaseId, request);
        }

        foreach (Release release in releases)
        {
            List<ReleaseTrack> retained = [.. release.Tracklist.Where(releaseTrack => releaseTrack.TrackId != track.Id)];
            if (requestedByRelease.TryGetValue(release.Id, out TrackReleaseAppearanceRequest? request))
            {
                retained.Add(ReleaseTrack.Create(
                    track.Id,
                    TrackPosition.FromNumber(request.Position),
                    Optional.Missing<string>(),
                    ToOptionalString(request.VersionNote)));
            }

            bool hadTrack = release.Tracklist.Any(releaseTrack => releaseTrack.TrackId == track.Id);
            if (hadTrack || requestedByRelease.ContainsKey(release.Id))
            {
                release.ReplaceTracklist([.. retained.OrderBy(releaseTrack => releaseTrack.Position.Number)]);
            }
        }
    }

    private static async Task<IReadOnlyList<ResolvedTrackCredit>> ResolveTrackCreditsAsync(
        IReadOnlyList<TrackCreditRequest> creditRequests,
        CratebaseDbContext context,
        CollectionId collectionId,
        CancellationToken cancellationToken)
    {
        var resolved = new List<ResolvedTrackCredit>(creditRequests.Count);
        foreach (TrackCreditRequest creditRequest in creditRequests)
        {
            Artist artist = await ResolveTrackArtistAsync(creditRequest, context, collectionId, cancellationToken);
            CreditRole role = CreditMapper.ParseRole(string.IsNullOrWhiteSpace(creditRequest.Role) ? "mainArtist" : creditRequest.Role);
            resolved.Add(new ResolvedTrackCredit(artist, role));
        }

        return resolved;
    }

    private static async Task<Artist> ResolveTrackArtistAsync(
        TrackCreditRequest creditRequest,
        CratebaseDbContext context,
        CollectionId collectionId,
        CancellationToken cancellationToken)
    {
        if (creditRequest.ArtistId is { } artistId)
        {
            Artist? existing = await context.Artists.SingleOrDefaultAsync(
                artist => artist.CollectionId == collectionId && artist.Id == new ArtistId(artistId),
                cancellationToken);

            return existing ?? throw new DomainException("track.artist_conflict", "Track artist does not exist");
        }

        if (string.IsNullOrWhiteSpace(creditRequest.Name))
        {
            throw new DomainException("track.artist_name_required", "Track artist name is required");
        }

        string name = creditRequest.Name.Trim();
        Artist? pendingByName = context.ChangeTracker
            .Entries<Artist>()
            .Where(entry => entry.State == EntityState.Added)
            .Select(entry => entry.Entity)
            .FirstOrDefault(artist => artist.CollectionId == collectionId && artist.Name == name);
        if (pendingByName is not null)
        {
            return pendingByName;
        }

        Artist? existingByName = await context.Artists.FirstOrDefaultAsync(
            artist => artist.CollectionId == collectionId && artist.Name == name,
            cancellationToken);
        if (existingByName is not null)
        {
            return existingByName;
        }

        Artist created = Person.Create(collectionId, ArtistId.New(), name);
        _ = context.Artists.Add(created);

        return created;
    }

    private static IOptionalValue<string> ToOptionalString(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? Optional.Missing<string>()
            : Optional.From(value.Trim());
    }

    private sealed record ResolvedTrackCredit(Artist Artist, CreditRole Role);
}
