using Cratebase.Api.Features.Artists;
using Cratebase.Api.Features.Admin;
using Cratebase.Api.Features.ArtistRelations;
using Cratebase.Api.Features.Auth;
using Cratebase.Api.Features.Credits;
using Cratebase.Api.Features.Labels;
using Cratebase.Api.Features.OwnedItems;
using Cratebase.Api.Features.Releases;
using Cratebase.Api.Features.TrackRelations;
using Cratebase.Api.Features.Tracks;

namespace Cratebase.Api.Features;

public static class CratebaseEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapCratebaseEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        _ = endpoints.MapAuthEndpoints();
        _ = endpoints.MapAdminUsersEndpoints();
        _ = endpoints.MapArtistsEndpoints();
        _ = endpoints.MapLabelsEndpoints();
        _ = endpoints.MapTracksEndpoints();
        _ = endpoints.MapReleasesEndpoints();
        _ = endpoints.MapOwnedItemsEndpoints();
        _ = endpoints.MapCreditsEndpoints();
        _ = endpoints.MapArtistRelationsEndpoints();
        _ = endpoints.MapTrackRelationsEndpoints();

        return endpoints;
    }
}
