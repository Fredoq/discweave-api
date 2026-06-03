using DiscWeave.Api.Auth;
using DiscWeave.Api.Http;
using DiscWeave.Application.ExternalMetadata;
using Microsoft.Extensions.Primitives;

namespace DiscWeave.Api.Features.ExternalMetadata;

public static class ExternalMetadataArtistEndpointRouteBuilderExtensions
{
    private const int DefaultLimit = 25;
    private const int MaximumLimit = 100;

    public static IEndpointRouteBuilder MapExternalMetadataArtistEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder group = endpoints.MapGroup("/api/external-metadata/discogs/artists")
            .WithTags("External Metadata")
            .RequireAuthorization(DiscWeaveAuthorizationPolicies.CollectionMember);
        _ = group.MapGet("", SearchArtistsAsync).WithName("SearchExternalDiscogsArtists");
        _ = group.MapGet("/{externalId}", GetArtistAsync).WithName("GetExternalDiscogsArtist");

        return endpoints;
    }

    private static async Task<IResult> SearchArtistsAsync(
        HttpRequest request,
        IExternalMetadataProvider provider,
        CancellationToken cancellationToken)
    {
        ParsedArtistSearchRequest parsedRequest = ParseArtistSearchRequest(request);
        if (parsedRequest.Error is not null)
        {
            return parsedRequest.Error;
        }

        ExternalMetadataResult<ExternalMetadataSearchResult<ExternalMetadataArtistCandidate>> result =
            await provider.SearchArtistsAsync(parsedRequest.Query, cancellationToken);

        return result.IsSuccess
            ? Results.Ok(new ExternalMetadataSearchResponse<ExternalMetadataArtistCandidateResponse>(
                [.. result.Value.Items.Select(ToCandidateResponse)],
                parsedRequest.Query.Limit,
                result.Value.Total ?? result.Value.Items.Count))
            : ExternalMetadataEndpointErrors.ToHttpResult(result.Error);
    }

    private static async Task<IResult> GetArtistAsync(
        string externalId,
        IExternalMetadataProvider provider,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(externalId))
        {
            return EndpointErrors.BadRequest("external_metadata.artist.external_id_invalid", "External artist id is required");
        }

        ExternalMetadataResult<ExternalMetadataArtistDetail> result =
            await provider.GetArtistAsync(new ExternalMetadataLookupQuery(externalId.Trim()), cancellationToken);

        return result.IsSuccess
            ? Results.Ok(ToDetailResponse(result.Value))
            : ExternalMetadataEndpointErrors.ToHttpResult(result.Error);
    }

    private static ParsedArtistSearchRequest ParseArtistSearchRequest(HttpRequest request)
    {
        IQueryCollection values = request.Query;
        string? query = TrimOrNull(PreferQuery(values, "query", "q"));

        if (string.IsNullOrWhiteSpace(query))
        {
            return ParsedArtistSearchRequest.WithError(
                EndpointErrors.BadRequest("external_metadata.artist.criteria_required", "Artist search criteria are required"));
        }

        bool hasValidLimit = TryReadInt(values, "limit", out int? limit);
        return !hasValidLimit || limit is < 1 or > MaximumLimit
            ? ParsedArtistSearchRequest.WithError(
                EndpointErrors.BadRequest("external_metadata.artist.limit_invalid", "Artist search limit must be between 1 and 100"))
            : new ParsedArtistSearchRequest(new ExternalMetadataArtistSearchQuery(query, limit ?? DefaultLimit), null);
    }

    private static ExternalMetadataArtistCandidateResponse ToCandidateResponse(ExternalMetadataArtistCandidate candidate)
    {
        return new ExternalMetadataArtistCandidateResponse(
            candidate.Source,
            candidate.Name,
            candidate.Profile,
            candidate.NameVariations);
    }

    private static ExternalMetadataArtistDetailResponse ToDetailResponse(ExternalMetadataArtistDetail detail)
    {
        return new ExternalMetadataArtistDetailResponse(
            detail.Source,
            detail.Name,
            detail.Profile,
            detail.Aliases,
            detail.Members,
            detail.NameVariations,
            new ExternalMetadataArtistDraftResponse(
                detail.Name,
                [new ExternalMetadataDraftExternalSourceResponse(
                    detail.Source.ProviderName,
                    detail.Source.ResourceType,
                    detail.Source.ExternalId,
                    detail.Source.SourceUrl)]));
    }

    private static string? PreferQuery(IQueryCollection values, string preferredName, string fallbackName)
    {
        string? preferred = QueryValue(values, preferredName);
        return string.IsNullOrWhiteSpace(preferred) ? QueryValue(values, fallbackName) : preferred;
    }

    private static string? QueryValue(IQueryCollection values, string name)
    {
        return values.TryGetValue(name, out StringValues value) ? value.ToString() : null;
    }

    private static bool TryReadInt(IQueryCollection values, string name, out int? value)
    {
        string? raw = QueryValue(values, name);
        if (string.IsNullOrWhiteSpace(raw))
        {
            value = null;
            return true;
        }

        bool parsed = int.TryParse(raw, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int result);
        value = parsed ? result : null;

        return parsed;
    }

    private static string? TrimOrNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private sealed record ParsedArtistSearchRequest(ExternalMetadataArtistSearchQuery Query, IResult? Error)
    {
        public static ParsedArtistSearchRequest WithError(IResult error)
        {
            return new ParsedArtistSearchRequest(new ExternalMetadataArtistSearchQuery(), error);
        }
    }
}
