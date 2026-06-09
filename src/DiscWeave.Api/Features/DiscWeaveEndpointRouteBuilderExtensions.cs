using DiscWeave.Api.Features.Artists;
using DiscWeave.Api.Features.Admin;
using DiscWeave.Api.Features.ArtistRelations;
using DiscWeave.Api.Features.Auth;
using DiscWeave.Api.Features.CatalogGraph;
using DiscWeave.Api.Features.CatalogLinks;
using DiscWeave.Api.Features.CatalogQuality;
using DiscWeave.Api.Features.Credits;
using DiscWeave.Api.Features.Exports;
using DiscWeave.Api.Features.ExternalMetadata;
using DiscWeave.Api.Features.Imports;
using DiscWeave.Api.Features.Labels;
using DiscWeave.Api.Features.OwnedItems;
using DiscWeave.Api.Features.Playlists;
using DiscWeave.Api.Features.Ratings;
using DiscWeave.Api.Features.Releases;
using DiscWeave.Api.Features.Search;
using DiscWeave.Api.Features.Settings;
using DiscWeave.Api.Features.TrackRelations;
using DiscWeave.Api.Features.Tracks;

namespace DiscWeave.Api.Features;

public static class DiscWeaveEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapDiscWeaveEndpoints(this IEndpointRouteBuilder endpoints)
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
        _ = endpoints.MapExternalMetadataReleaseEndpoints();
        _ = endpoints.MapExternalMetadataArtistEndpoints();
        _ = endpoints.MapExternalMetadataTrackEndpoints();
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
