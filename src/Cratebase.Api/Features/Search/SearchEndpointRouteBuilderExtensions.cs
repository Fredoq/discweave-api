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
        string? entityType,
        string? role,
        string? media,
        string? status,
        Guid? labelId,
        string? tag,
        string? savedView,
        int? limit,
        int? offset,
        ICollectionSearchQueries searchQueries,
        CancellationToken cancellationToken)
    {
        string normalizedQuery = string.IsNullOrWhiteSpace(query) ? q?.Trim() ?? string.Empty : query.Trim();
        var searchQuery = new CollectionSearchQuery(
            normalizedQuery,
            entityType,
            role,
            media,
            status,
            labelId,
            tag,
            savedView,
            0,
            0);
        if (!searchQuery.HasCriteria)
        {
            return EndpointErrors.BadRequest("search.criteria_required", "Search query, filter, or saved view is required");
        }

        if (!Pagination.TryNormalize(limit, offset, out int normalizedLimit, out int normalizedOffset, out IResult error))
        {
            return error;
        }

        CollectionSearchResult result = await searchQueries.SearchAsync(
            searchQuery with { Limit = normalizedLimit, Offset = normalizedOffset },
            cancellationToken);

        return Results.Ok(new ListResponse<SearchResultResponse>(
            [.. result.Items.Select(ToResponse)],
            result.Limit,
            result.Offset,
            result.Total));
    }

    private static SearchResultResponse ToResponse(SearchResultReadModel result)
    {
        var facets = new SearchResultFacetsResponse(
            result.Facets.Roles,
            result.Facets.Media,
            result.Facets.Statuses,
            result.Facets.Tags,
            result.Facets.LabelId,
            result.Facets.CollectorSignals);

        return new SearchResultResponse(
            result.Id,
            result.Type,
            result.Title,
            result.Subtitle,
            result.Summary,
            result.MatchedFields,
            result.Snippets,
            facets,
            result.Rank);
    }
}
