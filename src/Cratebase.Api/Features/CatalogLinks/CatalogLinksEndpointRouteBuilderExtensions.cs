using Cratebase.Api.Auth;
using Cratebase.Api.Http;
using Cratebase.Application.Security;
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

    private sealed record CatalogLinksResponse(IReadOnlyList<CatalogLinkResponse> Items);

    private sealed record CatalogLinkResponse(string Kind, Guid Id, string Title, string? Subtitle);
}
