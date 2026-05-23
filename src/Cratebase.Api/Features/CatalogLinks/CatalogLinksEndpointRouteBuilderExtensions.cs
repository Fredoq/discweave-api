using Cratebase.Api.Auth;
using Cratebase.Api.Http;
using Cratebase.Application.Security;
using Cratebase.Domain.Catalog;
using Cratebase.Domain.Collection;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Api.Features.CatalogLinks;

public static class CatalogLinksEndpointRouteBuilderExtensions
{
    private static readonly string[] DefaultKinds = ["artist", "release", "track", "ownedItem", "label", "playlist"];

    public static IEndpointRouteBuilder MapCatalogLinksEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder group = endpoints.MapGroup("/api/catalog-links")
            .WithTags("Catalog Links")
            .RequireAuthorization(CratebaseAuthorizationPolicies.CollectionMember);
        _ = group.MapGet("", ListCatalogLinksAsync).WithName("ListCatalogLinks");

        return endpoints;
    }

    private static async Task<IResult> ListCatalogLinksAsync(
        string? query,
        string? kinds,
        int? limit,
        CratebaseDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        if (!Pagination.TryNormalize(limit, 0, out int normalizedLimit, out _, out IResult error))
        {
            return error;
        }

        string[] requestedKinds = ParseKinds(kinds);
        string? normalizedQuery = string.IsNullOrWhiteSpace(query) ? null : query.Trim();
        string? pattern = string.IsNullOrWhiteSpace(query) ? null : $"%{query.Trim()}%";
        List<CatalogLinkResponse> items = [];

        if (requestedKinds.Contains("artist", StringComparer.OrdinalIgnoreCase))
        {
            items.AddRange(await context.Artists.AsNoTracking()
                .Where(item => item.CollectionId == currentCollection.CollectionId && (pattern == null || EF.Functions.ILike(item.Name, pattern)))
                .OrderBy(item => item.Name)
                .Take(normalizedLimit)
                .Select(item => new CatalogLinkResponse("artist", item.Id.Value, item.Name, "artist"))
                .ToArrayAsync(cancellationToken));
        }

        if (requestedKinds.Contains("label", StringComparer.OrdinalIgnoreCase))
        {
            items.AddRange(await context.Labels.AsNoTracking()
                .Where(item => item.CollectionId == currentCollection.CollectionId && (pattern == null || EF.Functions.ILike(item.Name, pattern)))
                .OrderBy(item => item.Name)
                .Take(normalizedLimit)
                .Select(item => new CatalogLinkResponse("label", item.Id.Value, item.Name, "label"))
                .ToArrayAsync(cancellationToken));
        }

        if (requestedKinds.Contains("release", StringComparer.OrdinalIgnoreCase))
        {
            items.AddRange(await context.Releases.AsNoTracking()
                .Where(item => item.CollectionId == currentCollection.CollectionId && (pattern == null || EF.Functions.ILike(item.Summary.Title, pattern)))
                .OrderBy(item => item.Summary.Title)
                .Take(normalizedLimit)
                .Select(item => new CatalogLinkResponse("release", item.Id.Value, item.Summary.Title, item.Summary.Metadata.Type))
                .ToArrayAsync(cancellationToken));
        }

        if (requestedKinds.Contains("track", StringComparer.OrdinalIgnoreCase))
        {
            items.AddRange(await context.Tracks.AsNoTracking()
                .Where(item => item.CollectionId == currentCollection.CollectionId && (pattern == null || EF.Functions.ILike(item.Title, pattern)))
                .OrderBy(item => item.Title)
                .Take(normalizedLimit)
                .Select(item => new CatalogLinkResponse("track", item.Id.Value, item.Title, "track"))
                .ToArrayAsync(cancellationToken));
        }

        if (requestedKinds.Contains("ownedItem", StringComparer.OrdinalIgnoreCase))
        {
            OwnedItem[] ownedItems = await context.OwnedItems.AsNoTracking()
                .Where(item => item.CollectionId == currentCollection.CollectionId)
                .OrderBy(item => item.Id)
                .Take(pattern == null ? normalizedLimit : Math.Min(normalizedLimit * 5, 250))
                .ToArrayAsync(cancellationToken);

            items.AddRange(await OwnedItemLinksAsync(context, currentCollection.CollectionId, ownedItems, normalizedQuery, cancellationToken));
        }

        if (requestedKinds.Contains("playlist", StringComparer.OrdinalIgnoreCase))
        {
            items.AddRange(await context.Playlists.AsNoTracking()
                .Where(item => item.CollectionId == currentCollection.CollectionId && (pattern == null || EF.Functions.ILike(item.Name, pattern)))
                .OrderBy(item => item.Name)
                .Take(normalizedLimit)
                .Select(item => new CatalogLinkResponse("playlist", item.Id.Value, item.Name, "playlist"))
                .ToArrayAsync(cancellationToken));
        }

        CatalogLinkResponse[] page = [.. items.OrderBy(item => item.Title).ThenBy(item => item.Kind).ThenBy(item => item.Id).Take(normalizedLimit)];
        return Results.Ok(new CatalogLinksResponse(page));
    }

    private static string[] ParseKinds(string? kinds)
    {
        return string.IsNullOrWhiteSpace(kinds)
            ? DefaultKinds
            : [.. kinds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
    }

    private static async Task<IReadOnlyList<CatalogLinkResponse>> OwnedItemLinksAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        IReadOnlyList<OwnedItem> ownedItems,
        string? query,
        CancellationToken cancellationToken)
    {
        ReleaseId[] releaseIds = [.. ownedItems.Select(item => item.Target).OfType<ReleaseOwnedItemTarget>().Select(target => target.ReleaseId).Distinct()];
        TrackId[] trackIds = [.. ownedItems.Select(item => item.Target).OfType<TrackOwnedItemTarget>().Select(target => target.TrackId).Distinct()];
        Dictionary<ReleaseId, Release> releases = releaseIds.Length == 0
            ? []
            : await context.Releases.AsNoTracking()
                .Where(release => release.CollectionId == collectionId && releaseIds.Contains(release.Id))
                .ToDictionaryAsync(release => release.Id, cancellationToken);
        Dictionary<TrackId, Track> tracks = trackIds.Length == 0
            ? []
            : await context.Tracks.AsNoTracking()
                .Where(track => track.CollectionId == collectionId && trackIds.Contains(track.Id))
                .ToDictionaryAsync(track => track.Id, cancellationToken);

        CatalogLinkResponse[] links =
        [
            .. ownedItems
                .Select(item => OwnedItemLink(item, releases, tracks))
                .Where(item => query is null ||
                    item.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    (item.Subtitle is not null && item.Subtitle.Contains(query, StringComparison.OrdinalIgnoreCase)))
        ];

        return links;
    }

    private static CatalogLinkResponse OwnedItemLink(
        OwnedItem item,
        Dictionary<ReleaseId, Release> releases,
        Dictionary<TrackId, Track> tracks)
    {
        string title = item.Target switch
        {
            ReleaseOwnedItemTarget target when releases.TryGetValue(target.ReleaseId, out Release? release) => release.Summary.Title,
            TrackOwnedItemTarget target when tracks.TryGetValue(target.TrackId, out Track? track) => track.Title,
            ReleaseOwnedItemTarget => "Unknown release",
            TrackOwnedItemTarget => "Unknown track",
            _ => "Unknown item"
        };

        string subtitle = $"{item.Holding.Medium.Code} / {OwnershipStatusCode(item.Holding.Status)}";
        return new CatalogLinkResponse("ownedItem", item.Id.Value, title, subtitle);
    }

    private static string OwnershipStatusCode(OwnershipStatus status)
    {
        return status switch
        {
            OwnershipStatus.Owned => "owned",
            OwnershipStatus.Wanted => "wanted",
            OwnershipStatus.Sold => "sold",
            OwnershipStatus.NeedsDigitization => "needsDigitization",
            _ => throw new InvalidOperationException("Ownership status is not supported")
        };
    }

    private sealed record CatalogLinksResponse(IReadOnlyList<CatalogLinkResponse> Items);

    private sealed record CatalogLinkResponse(string Kind, Guid Id, string Title, string? Subtitle);
}
