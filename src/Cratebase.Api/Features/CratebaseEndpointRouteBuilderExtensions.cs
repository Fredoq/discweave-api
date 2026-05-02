using Cratebase.Api.Features.Artists;
using Cratebase.Api.Features.Core;

namespace Cratebase.Api.Features;

public static class CratebaseEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapCratebaseEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        _ = endpoints.MapArtistsEndpoints();
        _ = endpoints.MapCoreCatalogEndpoints();

        return endpoints;
    }
}
