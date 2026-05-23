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
    private const string ArtistKind = "artist";
    private const string LabelKind = "label";
    private const string OwnedItemKind = "ownedItem";
    private const string PlaylistKind = "playlist";
    private const string ReleaseKind = "release";
    private const string TrackKind = "track";

    private static readonly string[] DefaultKinds = [ArtistKind, ReleaseKind, TrackKind, OwnedItemKind, LabelKind, PlaylistKind];

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
        List<CatalogLinkResponse> items =
        [
            .. await ArtistLinksAsync(context, currentCollection.CollectionId, requestedKinds, pattern, normalizedLimit, cancellationToken),
            .. await LabelLinksAsync(context, currentCollection.CollectionId, requestedKinds, pattern, normalizedLimit, cancellationToken),
            .. await ReleaseLinksAsync(context, currentCollection.CollectionId, requestedKinds, pattern, normalizedLimit, cancellationToken),
            .. await TrackLinksAsync(context, currentCollection.CollectionId, requestedKinds, pattern, normalizedLimit, cancellationToken),
            .. await OwnedItemLinksAsync(context, currentCollection.CollectionId, requestedKinds, normalizedQuery, pattern, normalizedLimit, cancellationToken),
            .. await PlaylistLinksAsync(context, currentCollection.CollectionId, requestedKinds, pattern, normalizedLimit, cancellationToken)
        ];

        CatalogLinkResponse[] page = [.. items.OrderBy(item => item.Title).ThenBy(item => item.Kind).ThenBy(item => item.Id).Take(normalizedLimit)];
        return Results.Ok(new CatalogLinksResponse(page));
    }

    private static string[] ParseKinds(string? kinds)
    {
        return string.IsNullOrWhiteSpace(kinds)
            ? DefaultKinds
            : [.. kinds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
    }

    private static async Task<IReadOnlyList<CatalogLinkResponse>> ArtistLinksAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        string[] requestedKinds,
        string? pattern,
        int limit,
        CancellationToken cancellationToken)
    {
        return KindRequested(requestedKinds, ArtistKind)
            ? await context.Artists.AsNoTracking()
                .Where(item => item.CollectionId == collectionId && (pattern == null || EF.Functions.ILike(item.Name, pattern)))
                .OrderBy(item => item.Name)
                .Take(limit)
                .Select(item => new CatalogLinkResponse(ArtistKind, item.Id.Value, item.Name, ArtistKind))
                .ToArrayAsync(cancellationToken)
            : [];
    }

    private static async Task<IReadOnlyList<CatalogLinkResponse>> LabelLinksAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        string[] requestedKinds,
        string? pattern,
        int limit,
        CancellationToken cancellationToken)
    {
        return KindRequested(requestedKinds, LabelKind)
            ? await context.Labels.AsNoTracking()
                .Where(item => item.CollectionId == collectionId && (pattern == null || EF.Functions.ILike(item.Name, pattern)))
                .OrderBy(item => item.Name)
                .Take(limit)
                .Select(item => new CatalogLinkResponse(LabelKind, item.Id.Value, item.Name, LabelKind))
                .ToArrayAsync(cancellationToken)
            : [];
    }

    private static async Task<IReadOnlyList<CatalogLinkResponse>> ReleaseLinksAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        string[] requestedKinds,
        string? pattern,
        int limit,
        CancellationToken cancellationToken)
    {
        return KindRequested(requestedKinds, ReleaseKind)
            ? await context.Releases.AsNoTracking()
                .Where(item => item.CollectionId == collectionId && (pattern == null || EF.Functions.ILike(item.Summary.Title, pattern)))
                .OrderBy(item => item.Summary.Title)
                .Take(limit)
                .Select(item => new CatalogLinkResponse(ReleaseKind, item.Id.Value, item.Summary.Title, item.Summary.Metadata.Type))
                .ToArrayAsync(cancellationToken)
            : [];
    }

    private static async Task<IReadOnlyList<CatalogLinkResponse>> TrackLinksAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        string[] requestedKinds,
        string? pattern,
        int limit,
        CancellationToken cancellationToken)
    {
        return KindRequested(requestedKinds, TrackKind)
            ? await context.Tracks.AsNoTracking()
                .Where(item => item.CollectionId == collectionId && (pattern == null || EF.Functions.ILike(item.Title, pattern)))
                .OrderBy(item => item.Title)
                .Take(limit)
                .Select(item => new CatalogLinkResponse(TrackKind, item.Id.Value, item.Title, TrackKind))
                .ToArrayAsync(cancellationToken)
            : [];
    }

    private static async Task<IReadOnlyList<CatalogLinkResponse>> OwnedItemLinksAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        string[] requestedKinds,
        string? query,
        string? pattern,
        int limit,
        CancellationToken cancellationToken)
    {
        if (!KindRequested(requestedKinds, OwnedItemKind))
        {
            return [];
        }

        OwnedItem[] ownedItems = await context.OwnedItems.AsNoTracking()
            .Where(item => item.CollectionId == collectionId)
            .OrderBy(item => item.Id)
            .Take(pattern == null ? limit : Math.Min(limit * 5, 250))
            .ToArrayAsync(cancellationToken);
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

    private static async Task<IReadOnlyList<CatalogLinkResponse>> PlaylistLinksAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        string[] requestedKinds,
        string? pattern,
        int limit,
        CancellationToken cancellationToken)
    {
        return KindRequested(requestedKinds, PlaylistKind)
            ? await context.Playlists.AsNoTracking()
                .Where(item => item.CollectionId == collectionId && (pattern == null || EF.Functions.ILike(item.Name, pattern)))
                .OrderBy(item => item.Name)
                .Take(limit)
                .Select(item => new CatalogLinkResponse(PlaylistKind, item.Id.Value, item.Name, PlaylistKind))
                .ToArrayAsync(cancellationToken)
            : [];
    }

    private static bool KindRequested(string[] requestedKinds, string kind)
    {
        return requestedKinds.Contains(kind, StringComparer.OrdinalIgnoreCase);
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
        return new CatalogLinkResponse(OwnedItemKind, item.Id.Value, title, subtitle);
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
