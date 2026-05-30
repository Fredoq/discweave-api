using System.Globalization;
using DiscWeave.Api.Auth;
using DiscWeave.Api.Http;
using DiscWeave.Application.Search;
using Microsoft.Extensions.Primitives;

namespace DiscWeave.Api.Features.Search;

public static class SearchEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapSearchEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder group = endpoints.MapGroup("/api/search")
            .WithTags("Search")
            .RequireAuthorization(DiscWeaveAuthorizationPolicies.CollectionMember);
        _ = group.MapGet("", SearchAsync).WithName("SearchCollection");

        return endpoints;
    }

    private static async Task<IResult> SearchAsync(
        HttpRequest request,
        ICollectionSearchQueries searchQueries,
        CancellationToken cancellationToken)
    {
        ParsedSearchRequest parsedRequest = ParseRequest(request);
        if (parsedRequest.Error is not null)
        {
            return parsedRequest.Error;
        }

        CollectionSearchQuery searchQuery = parsedRequest.Query;
        if (!searchQuery.HasCriteria)
        {
            return EndpointErrors.BadRequest("search.criteria_required", "Search query, filter, or saved view is required");
        }

        if (!Pagination.TryNormalize(parsedRequest.Limit, parsedRequest.Offset, out int normalizedLimit, out int normalizedOffset, out IResult error))
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

    private static ParsedSearchRequest ParseRequest(HttpRequest request)
    {
        IQueryCollection values = request.Query;
        string normalizedQuery = string.IsNullOrWhiteSpace(QueryValue(values, "query"))
            ? QueryValue(values, "q")?.Trim() ?? string.Empty
            : QueryValue(values, "query")!.Trim();

        IResult? parseError = null;
        ParsedSearchRequest? parsedRequest = null;

        if (!TryReadGuid(values, "labelId", out Guid? labelId))
        {
            parseError = EndpointErrors.BadRequest("search.label_id_invalid", "Search label id must be a valid GUID");
        }
        else if (!TryReadInt(values, "limit", out int? limit))
        {
            parseError = EndpointErrors.BadRequest("search.limit_invalid", "Search limit must be an integer");
        }
        else if (!TryReadInt(values, "offset", out int? offset))
        {
            parseError = EndpointErrors.BadRequest("search.offset_invalid", "Search offset must be an integer");
        }
        else if (!SearchRequestValidation.TryNormalize(
            QueryValue(values, "entityType"),
            QueryValue(values, "status"),
            QueryValue(values, "savedView"),
            out string? entityType,
            out string? status,
            out string? savedView,
            out IResult? validationError))
        {
            parseError = validationError;
        }
        else
        {
            parsedRequest = new ParsedSearchRequest(
                new CollectionSearchQuery(
                    normalizedQuery,
                    entityType,
                    QueryValue(values, "role"),
                    QueryValue(values, "media"),
                    status,
                    labelId,
                    QueryValue(values, "tag"),
                    savedView,
                    0,
                    0),
                limit,
                offset,
                null);
        }

        return parsedRequest ?? ParsedSearchRequest.WithError(parseError!);
    }

    private static string? QueryValue(IQueryCollection values, string name)
    {
        return values.TryGetValue(name, out StringValues value) ? value.ToString() : null;
    }

    private static bool TryReadGuid(IQueryCollection values, string name, out Guid? value)
    {
        string? raw = QueryValue(values, name);
        if (string.IsNullOrWhiteSpace(raw))
        {
            value = null;
            return true;
        }

        if (Guid.TryParse(raw.Trim(), out Guid parsed))
        {
            value = parsed;
            return true;
        }

        value = null;
        return false;
    }

    private static bool TryReadInt(IQueryCollection values, string name, out int? value)
    {
        string? raw = QueryValue(values, name);
        if (string.IsNullOrWhiteSpace(raw))
        {
            value = null;
            return true;
        }

        if (int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
        {
            value = parsed;
            return true;
        }

        value = null;
        return false;
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

    private sealed record ParsedSearchRequest(
        CollectionSearchQuery Query,
        int? Limit,
        int? Offset,
        IResult? Error)
    {
        public static ParsedSearchRequest WithError(IResult error)
        {
            return new ParsedSearchRequest(new CollectionSearchQuery(string.Empty, null, null, null, null, null, null, null, 0, 0), null, null, error);
        }
    }
}
