using Cratebase.Api.Auth;
using Cratebase.Api.Http;
using Cratebase.Application.Search;

namespace Cratebase.Api.Features.Search;

public static class SearchEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapSearchEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder group = endpoints.MapGroup("/api/search")
            .WithTags("Search")
            .RequireAuthorization(CratebaseAuthorizationPolicies.CollectionMember);
        _ = group.MapGet("", SearchAsync).WithName("SearchCollection");

        return endpoints;
    }

    private static async Task<IResult> SearchAsync(
        string? query,
        string? q,
        int? limit,
        int? offset,
        ICollectionSearchQueries searchQueries,
        CancellationToken cancellationToken)
    {
        string normalizedQuery = string.IsNullOrWhiteSpace(query) ? q?.Trim() ?? string.Empty : query.Trim();
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return EndpointErrors.BadRequest("search.query_required", "Search query is required");
        }

        if (!Pagination.TryNormalize(limit, offset, out int normalizedLimit, out int normalizedOffset, out IResult error))
        {
            return error;
        }

        CollectionSearchResult result = await searchQueries.SearchAsync(
            new CollectionSearchQuery(normalizedQuery, normalizedLimit, normalizedOffset),
            cancellationToken);

        return Results.Ok(new ListResponse<SearchResultResponse>(
            [.. result.Items.Select(ToResponse)],
            result.Limit,
            result.Offset,
            result.Total));
    }

    private static SearchResultResponse ToResponse(SearchResultReadModel result)
    {
        return new SearchResultResponse(result.Id, result.Type, result.Title, result.Subtitle, result.MatchedFields);
    }
}
