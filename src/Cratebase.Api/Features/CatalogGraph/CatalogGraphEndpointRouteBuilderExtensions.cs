using Cratebase.Api.Auth;
using Cratebase.Api.Http;
using Cratebase.Application.Security;
using Cratebase.Domain.Catalog;
using Cratebase.Domain.Collection;
using Cratebase.Domain.Credits;
using Cratebase.Domain.Playlists;
using Cratebase.Domain.Relations;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Api.Features.CatalogGraph;

public static partial class CatalogGraphEndpointRouteBuilderExtensions
{
    private const string ArtistEntityType = "artist";
    private const string ReleaseEntityType = "release";
    private const string TrackEntityType = "track";
    private const string OwnedItemEntityType = "ownedItem";
    private const string OwnedItemRouteType = "owneditem";
    private const string OwnedItemHyphenRouteType = "owned-item";
    private const string LabelEntityType = "label";
    private const string PlaylistEntityType = "playlist";
    private const string RelationEntityType = "relation";
    private const string CreditRelation = "credit";
    private const string MediaRelation = "media";
    private const string LabelRelation = "label";
    private const string NotFoundCode = "catalog_graph.not_found";
    private const string NotFoundMessage = "Catalog graph entity was not found";

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
        CollectionId collectionId = currentCollection.CollectionId;
        string normalizedEntityType = entityType.Trim().ToLowerInvariant();

        return normalizedEntityType switch
        {
            ArtistEntityType => await ArtistResultAsync(context, collectionId, new ArtistId(entityId), cancellationToken),
            ReleaseEntityType => await ReleaseResultAsync(context, collectionId, new ReleaseId(entityId), cancellationToken),
            TrackEntityType => await TrackResultAsync(context, collectionId, new TrackId(entityId), cancellationToken),
            OwnedItemRouteType or OwnedItemHyphenRouteType => await OwnedItemResultAsync(context, collectionId, new OwnedItemId(entityId), cancellationToken),
            LabelEntityType => await LabelResultAsync(context, collectionId, new LabelId(entityId), cancellationToken),
            PlaylistEntityType => await PlaylistResultAsync(context, collectionId, new PlaylistId(entityId), cancellationToken),
            _ => EndpointErrors.NotFound(NotFoundCode, NotFoundMessage)
        };
    }

    private static async Task<IResult> ArtistResultAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        ArtistId artistId,
        CancellationToken cancellationToken)
    {
        GraphData? data = await GraphData.LoadArtistAsync(context, collectionId, artistId, cancellationToken);
        return data is not null && data.Artists.TryGetValue(artistId, out Artist? artist)
            ? Results.Ok(ArtistContext(artist, data))
            : EndpointErrors.NotFound(NotFoundCode, NotFoundMessage);
    }

    private static async Task<IResult> ReleaseResultAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        ReleaseId releaseId,
        CancellationToken cancellationToken)
    {
        GraphData? data = await GraphData.LoadReleaseAsync(context, collectionId, releaseId, cancellationToken);
        return data is not null && data.Releases.TryGetValue(releaseId, out Release? release)
            ? Results.Ok(ReleaseContext(release, data))
            : EndpointErrors.NotFound(NotFoundCode, NotFoundMessage);
    }

    private static async Task<IResult> TrackResultAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        TrackId trackId,
        CancellationToken cancellationToken)
    {
        GraphData? data = await GraphData.LoadTrackAsync(context, collectionId, trackId, cancellationToken);
        return data is not null && data.Tracks.TryGetValue(trackId, out Track? track)
            ? Results.Ok(TrackContext(track, data))
            : EndpointErrors.NotFound(NotFoundCode, NotFoundMessage);
    }

    private static async Task<IResult> OwnedItemResultAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        OwnedItemId ownedItemId,
        CancellationToken cancellationToken)
    {
        GraphData? data = await GraphData.LoadOwnedItemAsync(context, collectionId, ownedItemId, cancellationToken);
        return data is not null && data.OwnedItems.TryGetValue(ownedItemId, out OwnedItem? item)
            ? Results.Ok(OwnedItemContext(item, data))
            : EndpointErrors.NotFound(NotFoundCode, NotFoundMessage);
    }

    private static async Task<IResult> LabelResultAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        LabelId labelId,
        CancellationToken cancellationToken)
    {
        GraphData? data = await GraphData.LoadLabelAsync(context, collectionId, labelId, cancellationToken);
        return data is not null && data.Labels.TryGetValue(labelId, out Label? label)
            ? Results.Ok(LabelContext(label, data))
            : EndpointErrors.NotFound(NotFoundCode, NotFoundMessage);
    }

    private static async Task<IResult> PlaylistResultAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        PlaylistId playlistId,
        CancellationToken cancellationToken)
    {
        GraphData? data = await GraphData.LoadPlaylistAsync(context, collectionId, playlistId, cancellationToken);
        return data is not null && data.Playlists.TryGetValue(playlistId, out Playlist? playlist)
            ? Results.Ok(PlaylistContext(playlist, data))
            : EndpointErrors.NotFound(NotFoundCode, NotFoundMessage);
    }

    private static CatalogGraphContextResponse LabelContext(Label label, GraphData data)
    {
        Release[] releases = [.. data.Releases.Values.Where(release => ReleaseLabelIds(release).Contains(label.Id)).OrderBy(release => release.Summary.Title)];
        OwnedItem[] ownedItems = [.. data.OwnedItems.Values.Where(item => releases.Any(release => item.Target is ReleaseOwnedItemTarget target && target.ReleaseId == release.Id))];

        return Response(
            Entity(label.Id.Value, LabelEntityType, label.Name, LabelEntityType, $"{releases.Length} releases"),
            new GraphSections
            {
                Releases = [.. releases.Select(release => Link(release.Id.Value, ReleaseEntityType, release.Summary.Title, null, "label release"))],
                OwnedCopies = [.. ownedItems.Select(item => OwnedItemLink(item, data, "owned copy"))],
                Media = [.. ownedItems.Select(item => Link(item.Id.Value, OwnedItemEntityType, item.Holding.Medium.Code, StatusCode(item.Holding.Status), MediaRelation))],
                Playlists = PlaylistLinksForReleases(releases.Select(release => release.Id), data),
                CollectorSignals = Signals(ownedItems)
            });
    }

    private static CatalogGraphContextResponse ArtistContext(Artist artist, GraphData data)
    {
        Credit[] credits = [.. data.Credits.Where(credit => credit.Contributor.ArtistId == artist.Id)];
        ArtistRelation[] relations = [.. data.ArtistRelations.Where(relation => relation.SourceArtistId == artist.Id || relation.TargetArtistId == artist.Id)];

        return Response(
            Entity(artist.Id.Value, ArtistEntityType, artist.Name, artist.GetType().Name, string.Join(", ", credits.Select(credit => credit.Role).Distinct())),
            new GraphSections
            {
                Releases = [.. credits.Select(credit => CreditTargetLink(credit, data)).Where(link => link is { Type: ReleaseEntityType }).Select(link => link!)],
                Tracks = [.. credits.Select(credit => CreditTargetLink(credit, data)).Where(link => link is { Type: TrackEntityType }).Select(link => link!)],
                Credits = [.. credits.Select(credit => CreditTargetLink(credit, data)).WhereNotNull()],
                Relations = [.. relations.Select(relation => Link(relation.Id.Value, RelationEntityType, ArtistRelationTitle(relation, data), relation.Type, "artist relation"))]
            });
    }

    private static CatalogGraphContextResponse ReleaseContext(Release release, GraphData data)
    {
        OwnedItem[] ownedItems = [.. data.OwnedItems.Values.Where(item => item.Target is ReleaseOwnedItemTarget target && target.ReleaseId == release.Id)];
        Credit[] credits = [.. data.Credits.Where(credit => credit.Target is ReleaseCreditTarget target && target.ReleaseId == release.Id)];
        Label[] labels = [.. ReleaseLabelIds(release).Select(id => data.Labels.GetValueOrDefault(id)).WhereNotNull()];

        return Response(
            Entity(release.Id.Value, ReleaseEntityType, release.Summary.Title, labels.FirstOrDefault()?.Name, string.Join(", ", release.Cataloging.Tags.Select(tag => tag.Name))),
            new GraphSections
            {
                Artists = [.. credits.Select(credit => Link(credit.Contributor.ArtistId.Value, ArtistEntityType, credit.Contributor.Name, credit.Role, CreditRelation))],
                Tracks = [.. release.Tracklist.Select(track => data.Tracks.GetValueOrDefault(track.TrackId)).WhereNotNull().Select(track => Link(track.Id.Value, TrackEntityType, track.Title, null, "tracklist"))],
                OwnedCopies = [.. ownedItems.Select(item => OwnedItemLink(item, data, "owned copy"))],
                Labels = [.. labels.Select(label => Link(label.Id.Value, LabelEntityType, label.Name, null, LabelRelation))],
                Playlists = PlaylistLinksForRelease(release.Id, data),
                Credits = [.. credits.Select(credit => Link(credit.Contributor.ArtistId.Value, ArtistEntityType, credit.Contributor.Name, credit.Role, CreditRelation))],
                Media = [.. ownedItems.Select(item => Link(item.Id.Value, OwnedItemEntityType, item.Holding.Medium.Code, StatusCode(item.Holding.Status), MediaRelation))],
                CollectorSignals = Signals(ownedItems)
            });
    }

    private static CatalogGraphContextResponse TrackContext(Track track, GraphData data)
    {
        OwnedItem[] ownedItems = [.. data.OwnedItems.Values.Where(item => item.Target is TrackOwnedItemTarget target && target.TrackId == track.Id)];
        Credit[] credits = [.. data.Credits.Where(credit => credit.Target is TrackCreditTarget target && target.TrackId == track.Id)];
        TrackRelation[] relations = [.. data.TrackRelations.Where(relation => relation.SourceTrackId == track.Id || relation.TargetTrackId == track.Id)];
        Release[] releases = [.. data.Releases.Values.Where(release => release.Tracklist.Any(item => item.TrackId == track.Id))];

        return Response(
            Entity(track.Id.Value, TrackEntityType, track.Title, releases.FirstOrDefault()?.Summary.Title, string.Join(", ", track.Cataloging.Tags.Select(tag => tag.Name))),
            new GraphSections
            {
                Artists = [.. credits.Select(credit => Link(credit.Contributor.ArtistId.Value, ArtistEntityType, credit.Contributor.Name, credit.Role, CreditRelation))],
                Releases = [.. releases.Select(release => Link(release.Id.Value, ReleaseEntityType, release.Summary.Title, null, "appears on"))],
                OwnedCopies = [.. ownedItems.Select(item => OwnedItemLink(item, data, "owned copy"))],
                Credits = [.. credits.Select(credit => Link(credit.Contributor.ArtistId.Value, ArtistEntityType, credit.Contributor.Name, credit.Role, CreditRelation))],
                Relations = [.. relations.Select(relation => Link(relation.Id.Value, RelationEntityType, TrackRelationTitle(relation, data), relation.RelationType, "track relation"))],
                Playlists = PlaylistLinksForTrack(track.Id, data),
                Media = [.. ownedItems.Select(item => Link(item.Id.Value, OwnedItemEntityType, item.Holding.Medium.Code, StatusCode(item.Holding.Status), MediaRelation))],
                CollectorSignals = Signals(ownedItems)
            });
    }

    private static CatalogGraphContextResponse OwnedItemContext(OwnedItem item, GraphData data)
    {
        CatalogGraphContextResponse.LinkResponse targetLink = item.Target switch
        {
            ReleaseOwnedItemTarget releaseTarget when data.Releases.TryGetValue(releaseTarget.ReleaseId, out Release? release) => Link(release.Id.Value, ReleaseEntityType, release.Summary.Title, null, "owned target"),
            TrackOwnedItemTarget trackTarget when data.Tracks.TryGetValue(trackTarget.TrackId, out Track? track) => Link(track.Id.Value, TrackEntityType, track.Title, null, "owned target"),
            _ => Link(item.Id.Value, OwnedItemEntityType, "Owned item", null, "owned target")
        };

        return Response(
            Entity(item.Id.Value, OwnedItemEntityType, targetLink.Title, StatusCode(item.Holding.Status), item.Holding.Medium.Code),
            new GraphSections
            {
                Releases = targetLink.Type == ReleaseEntityType ? [targetLink] : [],
                Tracks = targetLink.Type == TrackEntityType ? [targetLink] : [],
                Playlists = item.Target switch
                {
                    ReleaseOwnedItemTarget releaseTarget => PlaylistLinksForRelease(releaseTarget.ReleaseId, data),
                    TrackOwnedItemTarget trackTarget => PlaylistLinksForTrack(trackTarget.TrackId, data),
                    _ => []
                },
                Media = [Link(item.Id.Value, OwnedItemEntityType, item.Holding.Medium.Code, StatusCode(item.Holding.Status), MediaRelation)],
                CollectorSignals = Signals([item])
            });
    }

    private static CatalogGraphContextResponse Response(
        CatalogGraphContextResponse.EntityResponse entity,
        GraphSections sections)
    {
        return new CatalogGraphContextResponse(
            entity,
            new CatalogGraphContextResponse.SectionsResponse(
                sections.Artists,
                sections.Releases,
                sections.Tracks,
                sections.OwnedCopies,
                sections.Labels,
                sections.Playlists,
                sections.Credits,
                sections.Relations,
                sections.Media),
            sections.CollectorSignals);
    }

    private static CatalogGraphContextResponse.EntityResponse Entity(Guid id, string type, string title, string? subtitle, string? summary)
    {
        return new CatalogGraphContextResponse.EntityResponse(id, type, title, subtitle, summary);
    }

    private static CatalogGraphContextResponse.LinkResponse Link(Guid id, string type, string title, string? subtitle, string relation)
    {
        return new CatalogGraphContextResponse.LinkResponse(id, type, title, subtitle, relation);
    }

}
