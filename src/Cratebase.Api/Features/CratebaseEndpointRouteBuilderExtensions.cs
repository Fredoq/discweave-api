using Cratebase.Api.Features.Artists;
using Cratebase.Api.Features.Admin;
using Cratebase.Api.Features.ArtistRelations;
using Cratebase.Api.Features.Auth;
using Cratebase.Api.Features.CatalogGraph;
using Cratebase.Api.Features.CatalogLinks;
using Cratebase.Api.Features.CatalogQuality;
using Cratebase.Api.Features.Credits;
using Cratebase.Api.Features.Exports;
using Cratebase.Api.Features.Imports;
using Cratebase.Api.Features.Labels;
using Cratebase.Api.Features.OwnedItems;
using Cratebase.Api.Features.Playlists;
using Cratebase.Api.Features.Ratings;
using Cratebase.Api.Features.Releases;
using Cratebase.Api.Features.Search;
using Cratebase.Api.Features.Settings;
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
        _ = endpoints.MapAdminInvitesEndpoints();
        _ = endpoints.MapArtistsEndpoints();
        _ = endpoints.MapLabelsEndpoints();
        _ = endpoints.MapTracksEndpoints();
        _ = endpoints.MapReleasesEndpoints();
        _ = endpoints.MapOwnedItemsEndpoints();
        _ = endpoints.MapPlaylistsEndpoints();
        _ = endpoints.MapCreditsEndpoints();
        _ = endpoints.MapArtistRelationsEndpoints();
        _ = endpoints.MapTrackRelationsEndpoints();
        _ = endpoints.MapSearchEndpoints();
        _ = endpoints.MapCatalogGraphEndpoints();
        _ = endpoints.MapCatalogLinksEndpoints();
        _ = endpoints.MapCatalogQualityEndpoints();
        _ = endpoints.MapExportsEndpoints();
        _ = endpoints.MapReleaseImportsEndpoints();
        _ = endpoints.MapSettingsDictionariesEndpoints();
        _ = endpoints.MapSettingsImportPatternsEndpoints();
        _ = endpoints.MapSettingsNamingProfilesEndpoints();
        _ = endpoints.MapSettingsTagRoleMappingsEndpoints();
        _ = endpoints.MapRatingCriteriaEndpoints();
        _ = endpoints.MapRatingsEndpoints();

        return endpoints;
    }
}
