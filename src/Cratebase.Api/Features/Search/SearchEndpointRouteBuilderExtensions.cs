using System.Diagnostics.CodeAnalysis;
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
        [AsParameters] SearchRequest request,
        ICollectionSearchQueries searchQueries,
        CancellationToken cancellationToken)
    {
        string normalizedQuery = string.IsNullOrWhiteSpace(request.Query)
            ? request.Q?.Trim() ?? string.Empty
            : request.Query.Trim();
        var searchQuery = new CollectionSearchQuery(
            normalizedQuery,
            request.EntityType,
            request.Role,
            request.Media,
            request.Status,
            request.LabelId,
            request.Tag,
            request.SavedView,
            0,
            0);
        if (!searchQuery.HasCriteria)
        {
            return EndpointErrors.BadRequest("search.criteria_required", "Search query, filter, or saved view is required");
        }

        if (!Pagination.TryNormalize(request.Limit, request.Offset, out int normalizedLimit, out int normalizedOffset, out IResult error))
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

    [SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "ASP.NET Core minimal API parameter binding creates this type at runtime.")]
    private sealed record SearchRequest
    {
        public string? Query { get; init; }

        public string? Q { get; init; }

        public string? EntityType { get; init; }

        public string? Role { get; init; }

        public string? Media { get; init; }

        public string? Status { get; init; }

        public Guid? LabelId { get; init; }

        public string? Tag { get; init; }

        public string? SavedView { get; init; }

        public int? Limit { get; init; }

        public int? Offset { get; init; }
    }
}
