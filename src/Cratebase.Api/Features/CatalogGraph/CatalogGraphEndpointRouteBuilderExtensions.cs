using Cratebase.Api.Auth;
using Cratebase.Api.Http;
using Cratebase.Application.Security;
using Cratebase.Domain.Catalog;
using Cratebase.Domain.Collection;
using Cratebase.Domain.Credits;
using Cratebase.Domain.Relations;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Api.Features.CatalogGraph;

public static class CatalogGraphEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapCatalogGraphEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder group = endpoints.MapGroup("/api/catalog-graph")
            .WithTags("Catalog Graph")
            .RequireAuthorization(CratebaseAuthorizationPolicies.CollectionMember);
        _ = group.MapGet("/{entityType}/{entityId:guid}", GetContextAsync).WithName("GetCatalogGraphContext");

        return endpoints;
    }

    private static async Task<IResult> GetContextAsync(
        string entityType,
        Guid entityId,
        CratebaseDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        GraphData data = await GraphData.LoadAsync(context, currentCollection.CollectionId, cancellationToken);

        return entityType.Trim().ToLowerInvariant() switch
        {
            "artist" when data.Artists.TryGetValue(new ArtistId(entityId), out Artist? artist) => Results.Ok(ArtistContext(artist, data)),
            "release" when data.Releases.TryGetValue(new ReleaseId(entityId), out Release? release) => Results.Ok(ReleaseContext(release, data)),
            "track" when data.Tracks.TryGetValue(new TrackId(entityId), out Track? track) => Results.Ok(TrackContext(track, data)),
            "owneditem" or "owned-item" when data.OwnedItems.TryGetValue(new OwnedItemId(entityId), out OwnedItem? item) => Results.Ok(OwnedItemContext(item, data)),
            "label" when data.Labels.TryGetValue(new LabelId(entityId), out Label? label) => Results.Ok(LabelContext(label, data)),
            _ => EndpointErrors.NotFound("catalog_graph.not_found", "Catalog graph entity was not found")
        };
    }

    private static CatalogGraphContextResponse LabelContext(Label label, GraphData data)
    {
        Release[] releases = [.. data.Releases.Values.Where(release => ReleaseLabelIds(release).Contains(label.Id)).OrderBy(release => release.Summary.Title)];
        OwnedItem[] ownedItems = [.. data.OwnedItems.Values.Where(item => releases.Any(release => item.Target is ReleaseOwnedItemTarget target && target.ReleaseId == release.Id))];

        return Response(
            Entity(label.Id.Value, "label", label.Name, "label", $"{releases.Length} releases"),
            releases: [.. releases.Select(release => Link(release.Id.Value, "release", release.Summary.Title, null, "label release"))],
            ownedCopies: [.. ownedItems.Select(item => OwnedItemLink(item, data, "owned copy"))],
            media: [.. ownedItems.Select(item => Link(item.Id.Value, "ownedItem", item.Holding.Medium.Code, StatusCode(item.Holding.Status), "media"))],
            signals: Signals(ownedItems));
    }

    private static CatalogGraphContextResponse ArtistContext(Artist artist, GraphData data)
    {
        Credit[] credits = [.. data.Credits.Where(credit => credit.Contributor.ArtistId == artist.Id)];
        ArtistRelation[] relations = [.. data.ArtistRelations.Where(relation => relation.SourceArtistId == artist.Id || relation.TargetArtistId == artist.Id)];

        return Response(
            Entity(artist.Id.Value, "artist", artist.Name, artist.GetType().Name, string.Join(", ", credits.Select(credit => credit.Role).Distinct())),
            releases: [.. credits.Select(credit => CreditTargetLink(credit, data)).Where(link => link is { Type: "release" }).Select(link => link!)],
            tracks: [.. credits.Select(credit => CreditTargetLink(credit, data)).Where(link => link is { Type: "track" }).Select(link => link!)],
            credits: [.. credits.Select(credit => CreditTargetLink(credit, data)).WhereNotNull()],
            relations: [.. relations.Select(relation => Link(relation.Id.Value, "relation", ArtistRelationTitle(relation, data), relation.Type, "artist relation"))]);
    }

    private static CatalogGraphContextResponse ReleaseContext(Release release, GraphData data)
    {
        OwnedItem[] ownedItems = [.. data.OwnedItems.Values.Where(item => item.Target is ReleaseOwnedItemTarget target && target.ReleaseId == release.Id)];
        Credit[] credits = [.. data.Credits.Where(credit => credit.Target is ReleaseCreditTarget target && target.ReleaseId == release.Id)];
        Label[] labels = [.. ReleaseLabelIds(release).Select(id => data.Labels.GetValueOrDefault(id)).WhereNotNull()];

        return Response(
            Entity(release.Id.Value, "release", release.Summary.Title, labels.FirstOrDefault()?.Name, string.Join(", ", release.Cataloging.Tags.Select(tag => tag.Name))),
            artists: [.. credits.Select(credit => Link(credit.Contributor.ArtistId.Value, "artist", credit.Contributor.Name, credit.Role, "credit"))],
            tracks: [.. release.Tracklist.Select(track => data.Tracks.GetValueOrDefault(track.TrackId)).WhereNotNull().Select(track => Link(track.Id.Value, "track", track.Title, null, "tracklist"))],
            ownedCopies: [.. ownedItems.Select(item => OwnedItemLink(item, data, "owned copy"))],
            labels: [.. labels.Select(label => Link(label.Id.Value, "label", label.Name, null, "label"))],
            credits: [.. credits.Select(credit => Link(credit.Contributor.ArtistId.Value, "artist", credit.Contributor.Name, credit.Role, "credit"))],
            media: [.. ownedItems.Select(item => Link(item.Id.Value, "ownedItem", item.Holding.Medium.Code, StatusCode(item.Holding.Status), "media"))],
            signals: Signals(ownedItems));
    }

    private static CatalogGraphContextResponse TrackContext(Track track, GraphData data)
    {
        OwnedItem[] ownedItems = [.. data.OwnedItems.Values.Where(item => item.Target is TrackOwnedItemTarget target && target.TrackId == track.Id)];
        Credit[] credits = [.. data.Credits.Where(credit => credit.Target is TrackCreditTarget target && target.TrackId == track.Id)];
        TrackRelation[] relations = [.. data.TrackRelations.Where(relation => relation.SourceTrackId == track.Id || relation.TargetTrackId == track.Id)];
        Release[] releases = [.. data.Releases.Values.Where(release => release.Tracklist.Any(item => item.TrackId == track.Id))];

        return Response(
            Entity(track.Id.Value, "track", track.Title, releases.FirstOrDefault()?.Summary.Title, string.Join(", ", track.Cataloging.Tags.Select(tag => tag.Name))),
            artists: [.. credits.Select(credit => Link(credit.Contributor.ArtistId.Value, "artist", credit.Contributor.Name, credit.Role, "credit"))],
            releases: [.. releases.Select(release => Link(release.Id.Value, "release", release.Summary.Title, null, "appears on"))],
            ownedCopies: [.. ownedItems.Select(item => OwnedItemLink(item, data, "owned copy"))],
            credits: [.. credits.Select(credit => Link(credit.Contributor.ArtistId.Value, "artist", credit.Contributor.Name, credit.Role, "credit"))],
            relations: [.. relations.Select(relation => Link(relation.Id.Value, "relation", TrackRelationTitle(relation, data), relation.RelationType, "track relation"))],
            media: [.. ownedItems.Select(item => Link(item.Id.Value, "ownedItem", item.Holding.Medium.Code, StatusCode(item.Holding.Status), "media"))],
            signals: Signals(ownedItems));
    }

    private static CatalogGraphContextResponse OwnedItemContext(OwnedItem item, GraphData data)
    {
        CatalogGraphContextResponse.LinkResponse targetLink = item.Target switch
        {
            ReleaseOwnedItemTarget releaseTarget when data.Releases.TryGetValue(releaseTarget.ReleaseId, out Release? release) => Link(release.Id.Value, "release", release.Summary.Title, null, "owned target"),
            TrackOwnedItemTarget trackTarget when data.Tracks.TryGetValue(trackTarget.TrackId, out Track? track) => Link(track.Id.Value, "track", track.Title, null, "owned target"),
            _ => Link(item.Id.Value, "ownedItem", "Owned item", null, "owned target")
        };

        return Response(
            Entity(item.Id.Value, "ownedItem", targetLink.Title, StatusCode(item.Holding.Status), item.Holding.Medium.Code),
            releases: targetLink.Type == "release" ? [targetLink] : [],
            tracks: targetLink.Type == "track" ? [targetLink] : [],
            media: [Link(item.Id.Value, "ownedItem", item.Holding.Medium.Code, StatusCode(item.Holding.Status), "media")],
            signals: Signals([item]));
    }

    private static CatalogGraphContextResponse Response(
        CatalogGraphContextResponse.EntityResponse entity,
        IReadOnlyList<CatalogGraphContextResponse.LinkResponse>? artists = null,
        IReadOnlyList<CatalogGraphContextResponse.LinkResponse>? releases = null,
        IReadOnlyList<CatalogGraphContextResponse.LinkResponse>? tracks = null,
        IReadOnlyList<CatalogGraphContextResponse.LinkResponse>? ownedCopies = null,
        IReadOnlyList<CatalogGraphContextResponse.LinkResponse>? labels = null,
        IReadOnlyList<CatalogGraphContextResponse.LinkResponse>? credits = null,
        IReadOnlyList<CatalogGraphContextResponse.LinkResponse>? relations = null,
        IReadOnlyList<CatalogGraphContextResponse.LinkResponse>? media = null,
        IReadOnlyList<string>? signals = null)
    {
        return new CatalogGraphContextResponse(
            entity,
            new CatalogGraphContextResponse.SectionsResponse(
                artists ?? [],
                releases ?? [],
                tracks ?? [],
                ownedCopies ?? [],
                labels ?? [],
                credits ?? [],
                relations ?? [],
                media ?? []),
            signals ?? []);
    }

    private static CatalogGraphContextResponse.EntityResponse Entity(Guid id, string type, string title, string? subtitle, string? summary)
    {
        return new CatalogGraphContextResponse.EntityResponse(id, type, title, subtitle, summary);
    }

    private static CatalogGraphContextResponse.LinkResponse Link(Guid id, string type, string title, string? subtitle, string relation)
    {
        return new CatalogGraphContextResponse.LinkResponse(id, type, title, subtitle, relation);
    }

    private static CatalogGraphContextResponse.LinkResponse OwnedItemLink(OwnedItem item, GraphData data, string relation)
    {
        string title = item.Target switch
        {
            ReleaseOwnedItemTarget target when data.Releases.TryGetValue(target.ReleaseId, out Release? release) => release.Summary.Title,
            TrackOwnedItemTarget target when data.Tracks.TryGetValue(target.TrackId, out Track? track) => track.Title,
            _ => "Owned item"
        };

        return Link(item.Id.Value, "ownedItem", title, $"{StatusCode(item.Holding.Status)} on {item.Holding.Medium.Code}", relation);
    }

    private static CatalogGraphContextResponse.LinkResponse? CreditTargetLink(Credit credit, GraphData data)
    {
        return credit.Target switch
        {
            ReleaseCreditTarget target when data.Releases.TryGetValue(target.ReleaseId, out Release? release) => Link(release.Id.Value, "release", release.Summary.Title, credit.Role, "credit"),
            TrackCreditTarget target when data.Tracks.TryGetValue(target.TrackId, out Track? track) => Link(track.Id.Value, "track", track.Title, credit.Role, "credit"),
            _ => null
        };
    }

    private static IEnumerable<LabelId> ReleaseLabelIds(Release release)
    {
        foreach (ReleaseLabel label in release.Labels)
        {
            yield return label.LabelId;
        }

        if (release.Summary.Metadata.LabelId.HasValue)
        {
            yield return release.Summary.Metadata.LabelId.Match(value => value, () => default);
        }
    }

    private static string ArtistRelationTitle(ArtistRelation relation, GraphData data)
    {
        return $"{data.Artists.GetValueOrDefault(relation.SourceArtistId)?.Name ?? "Artist"} to {data.Artists.GetValueOrDefault(relation.TargetArtistId)?.Name ?? "Artist"}";
    }

    private static string TrackRelationTitle(TrackRelation relation, GraphData data)
    {
        return $"{data.Tracks.GetValueOrDefault(relation.SourceTrackId)?.Title ?? "Track"} to {data.Tracks.GetValueOrDefault(relation.TargetTrackId)?.Title ?? "Track"}";
    }

    private static IReadOnlyList<string> Signals(IReadOnlyList<OwnedItem> items)
    {
        return [.. items.SelectMany(item => new[] { item.Holding.Medium.Code, StatusCode(item.Holding.Status) }).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase)];
    }

    private static string StatusCode(OwnershipStatus status)
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

    private sealed record GraphData(
        Dictionary<ArtistId, Artist> Artists,
        Dictionary<LabelId, Label> Labels,
        Dictionary<ReleaseId, Release> Releases,
        Dictionary<TrackId, Track> Tracks,
        Dictionary<OwnedItemId, OwnedItem> OwnedItems,
        IReadOnlyList<Credit> Credits,
        IReadOnlyList<ArtistRelation> ArtistRelations,
        IReadOnlyList<TrackRelation> TrackRelations)
    {
        public static async Task<GraphData> LoadAsync(CratebaseDbContext context, CollectionId collectionId, CancellationToken cancellationToken)
        {
            Artist[] artists = await context.Artists.AsNoTracking().Where(item => item.CollectionId == collectionId).ToArrayAsync(cancellationToken);
            Label[] labels = await context.Labels.AsNoTracking().Where(item => item.CollectionId == collectionId).ToArrayAsync(cancellationToken);
            Release[] releases = await context.Releases.AsNoTracking().Include("_genres").Include("_tags").Where(item => item.CollectionId == collectionId).ToArrayAsync(cancellationToken);
            Track[] tracks = await context.Tracks.AsNoTracking().Include("_genres").Include("_tags").Where(item => item.CollectionId == collectionId).ToArrayAsync(cancellationToken);
            OwnedItem[] ownedItems = await context.OwnedItems.AsNoTracking().Where(item => item.CollectionId == collectionId).ToArrayAsync(cancellationToken);
            Credit[] credits = await context.Credits.AsNoTracking().Where(item => item.CollectionId == collectionId).ToArrayAsync(cancellationToken);
            ArtistRelation[] artistRelations = await context.ArtistRelations.AsNoTracking().Where(item => item.CollectionId == collectionId).ToArrayAsync(cancellationToken);
            TrackRelation[] trackRelations = await context.TrackRelations.AsNoTracking().Where(item => item.CollectionId == collectionId).ToArrayAsync(cancellationToken);

            return new GraphData(
                artists.ToDictionary(item => item.Id),
                labels.ToDictionary(item => item.Id),
                releases.ToDictionary(item => item.Id),
                tracks.ToDictionary(item => item.Id),
                ownedItems.ToDictionary(item => item.Id),
                credits,
                artistRelations,
                trackRelations);
        }
    }
}
