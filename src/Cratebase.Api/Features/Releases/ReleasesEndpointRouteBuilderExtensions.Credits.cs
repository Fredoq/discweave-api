using Cratebase.Api.Features.Credits;
using Cratebase.Domain.Catalog;
using Cratebase.Domain.Credits;
using Cratebase.Domain.SharedKernel.Errors;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Api.Features.Releases;

public static partial class ReleasesEndpointRouteBuilderExtensions
{
    private static async Task<IReadOnlyList<ResolvedCredit>> ResolveTrackCreditsAsync(
        IReadOnlyList<ReleaseArtistCreditRequest>? artistCredits,
        IReadOnlyList<ResolvedCredit> releaseCredits,
        bool isVariousArtists,
        CratebaseDbContext context,
        CollectionId collectionId,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ResolvedCredit> resolvedCredits = await ResolveCreditsAsync(artistCredits, context, collectionId, cancellationToken);
        return resolvedCredits.Count > 0
            ? resolvedCredits
            : ResolveDefaultTrackCredits(releaseCredits, isVariousArtists);
    }

    private static IReadOnlyList<ResolvedCredit> ResolveDefaultTrackCredits(
        IReadOnlyList<ResolvedCredit> releaseCredits,
        bool isVariousArtists)
    {
        return isVariousArtists
            ? throw new DomainException("track.artist_required", "Track artist is required for Various Artists releases")
            : [.. releaseCredits.Where(credit => credit.Role == CreditRole.MainArtist)];
    }

    private static async Task<IReadOnlyList<ResolvedCredit>> ResolveCreditsAsync(
        IReadOnlyList<ReleaseArtistCreditRequest>? artistCredits,
        CratebaseDbContext context,
        CollectionId collectionId,
        CancellationToken cancellationToken)
    {
        if (artistCredits is null || artistCredits.Count == 0)
        {
            return [];
        }

        var resolved = new List<ResolvedCredit>();
        foreach (ReleaseArtistCreditRequest creditRequest in artistCredits)
        {
            Artist artist = await ResolveArtistAsync(creditRequest, context, collectionId, cancellationToken);
            CreditRole role = CreditMapper.ParseRole(string.IsNullOrWhiteSpace(creditRequest.Role) ? "mainArtist" : creditRequest.Role);
            resolved.Add(new ResolvedCredit(artist, role));
        }

        return resolved;
    }

    private static async Task<Artist> ResolveArtistAsync(
        ReleaseArtistCreditRequest creditRequest,
        CratebaseDbContext context,
        CollectionId collectionId,
        CancellationToken cancellationToken)
    {
        if (creditRequest.ArtistId is { } artistId)
        {
            Artist? existing = await context.Artists.SingleOrDefaultAsync(
                artist => artist.CollectionId == collectionId && artist.Id == new ArtistId(artistId),
                cancellationToken);

            return existing ?? throw new DomainException("release.artist_conflict", "Release artist does not exist");
        }

        if (string.IsNullOrWhiteSpace(creditRequest.Name))
        {
            throw new DomainException("release.artist_name_required", "Release artist name is required");
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

    private sealed record ResolvedCredit(Artist Artist, CreditRole Role);
}
