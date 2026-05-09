using Cratebase.Api.Features.Credits;
using Cratebase.Domain.Catalog;
using Cratebase.Domain.Credits;
using Cratebase.Domain.SharedKernel.Errors;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Infrastructure.Persistence;

namespace Cratebase.Api.Features.Releases;

public static partial class ReleasesEndpointRouteBuilderExtensions
{
    private static readonly CreditArtistResolverErrors ReleaseCreditArtistErrors = new(
        "release.artist_conflict",
        "Release artist does not exist",
        "release.artist_name_required",
        "Release artist name is required");

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
            Artist artist = await CreditArtistResolver.ResolveAsync(
                creditRequest.ArtistId,
                creditRequest.Name,
                context,
                collectionId,
                ReleaseCreditArtistErrors,
                cancellationToken);
            CreditRole role = CreditMapper.ParseRole(string.IsNullOrWhiteSpace(creditRequest.Role) ? "mainArtist" : creditRequest.Role);
            resolved.Add(new ResolvedCredit(artist, role));
        }

        return resolved;
    }

    private sealed record ResolvedCredit(Artist Artist, CreditRole Role);
}
