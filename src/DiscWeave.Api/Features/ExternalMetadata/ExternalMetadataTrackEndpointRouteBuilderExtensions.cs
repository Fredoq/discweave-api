using DiscWeave.Api.Auth;
using DiscWeave.Api.Http;
using DiscWeave.Application.ExternalMetadata;
using Microsoft.Extensions.Primitives;

namespace DiscWeave.Api.Features.ExternalMetadata;

public static class ExternalMetadataTrackEndpointRouteBuilderExtensions
{
    private const int DefaultLimit = 25;
    private const int MaximumLimit = 100;

    public static IEndpointRouteBuilder MapExternalMetadataTrackEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder group = endpoints.MapGroup("/api/external-metadata/discogs/tracks")
            .WithTags("External Metadata")
            .RequireAuthorization(DiscWeaveAuthorizationPolicies.CollectionMember);
        _ = group.MapGet("", SearchTracksAsync).WithName("SearchExternalDiscogsTracks");
        _ = group.MapGet("/{externalId}", GetTrackAsync).WithName("GetExternalDiscogsTrack");

        return endpoints;
    }

    private static async Task<IResult> SearchTracksAsync(
        HttpRequest request,
        IExternalMetadataProvider provider,
        CancellationToken cancellationToken)
    {
        ParsedTrackSearchRequest parsedRequest = ParseTrackSearchRequest(request);
        if (parsedRequest.Error is not null)
        {
            return parsedRequest.Error;
        }

        ExternalMetadataResult<ExternalMetadataSearchResult<ExternalMetadataTrackCandidate>> result =
            await provider.SearchTracksAsync(parsedRequest.Query, cancellationToken);

        return result.IsSuccess
            ? Results.Ok(new ExternalMetadataSearchResponse<ExternalMetadataTrackCandidateResponse>(
                [.. result.Value.Items.Select(ToCandidateResponse)],
                parsedRequest.Query.Limit,
                result.Value.Total ?? result.Value.Items.Count))
            : ExternalMetadataEndpointErrors.ToHttpResult(result.Error);
    }

    private static async Task<IResult> GetTrackAsync(
        string externalId,
        IExternalMetadataProvider provider,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(externalId))
        {
            return EndpointErrors.BadRequest("external_metadata.track.external_id_invalid", "External track id is required");
        }

        ExternalMetadataResult<ExternalMetadataTrackDetail> result =
            await provider.GetTrackAsync(new ExternalMetadataLookupQuery(externalId.Trim()), cancellationToken);

        return result.IsSuccess
            ? Results.Ok(ToDetailResponse(result.Value))
            : ExternalMetadataEndpointErrors.ToHttpResult(result.Error);
    }

    private static ParsedTrackSearchRequest ParseTrackSearchRequest(HttpRequest request)
    {
        IQueryCollection values = request.Query;
        string? title = TrimOrNull(QueryValue(values, "title"));
        if (string.IsNullOrWhiteSpace(title))
        {
            return ParsedTrackSearchRequest.WithError(
                EndpointErrors.BadRequest("external_metadata.track.criteria_required", "Track search title is required"));
        }

        if (!TryReadInt(values, "year", out int? year) || year is <= 0)
        {
            return ParsedTrackSearchRequest.WithError(
                EndpointErrors.BadRequest("external_metadata.track.year_invalid", "Track search year must be a positive integer"));
        }

        bool hasValidLimit = TryReadInt(values, "limit", out int? limit);
        return !hasValidLimit || limit is < 1 or > MaximumLimit
            ? ParsedTrackSearchRequest.WithError(
                EndpointErrors.BadRequest("external_metadata.track.limit_invalid", "Track search limit must be between 1 and 100"))
            : new ParsedTrackSearchRequest(
            new ExternalMetadataTrackSearchQuery(
                title,
                TrimOrNull(QueryValue(values, "artist")),
                TrimOrNull(QueryValue(values, "releaseTitle")),
                year,
                TrimOrNull(QueryValue(values, "barcode")),
                TrimOrNull(QueryValue(values, "catalogNumber")),
                limit ?? DefaultLimit),
            null);
    }

    private static ExternalMetadataTrackCandidateResponse ToCandidateResponse(ExternalMetadataTrackCandidate candidate)
    {
        return new ExternalMetadataTrackCandidateResponse(
            candidate.Source,
            candidate.Title,
            candidate.Position,
            ToDurationSeconds(candidate.Duration),
            candidate.Artists,
            ToReleaseContextResponse(candidate.Release));
    }

    private static ExternalMetadataTrackDetailResponse ToDetailResponse(ExternalMetadataTrackDetail detail)
    {
        return new ExternalMetadataTrackDetailResponse(
            detail.Source,
            detail.Title,
            detail.Position,
            ToDurationSeconds(detail.Duration),
            detail.Artists,
            [.. detail.Credits.Select(credit => new ExternalMetadataTrackCreditResponse(credit.Name, credit.Role))],
            ToReleaseContextResponse(detail.Release),
            new ExternalMetadataTrackDraftResponse(
                detail.Title,
                ToDurationSeconds(detail.Duration),
                DraftTrackCredits(detail),
                [new ExternalMetadataDraftExternalSourceResponse(
                    detail.Source.ProviderName,
                    detail.Source.ResourceType,
                    detail.Source.ExternalId,
                    detail.Source.SourceUrl)]));
    }

    private static ExternalMetadataReleaseDraftArtistCreditResponse[] DraftTrackCredits(ExternalMetadataTrackDetail detail)
    {
        IEnumerable<ExternalMetadataReleaseDraftArtistCreditResponse> mainArtists = detail.Artists
            .Where(artist => !string.IsNullOrWhiteSpace(artist))
            .Select(artist => new ExternalMetadataReleaseDraftArtistCreditResponse(artist, "mainArtist"));
        IEnumerable<ExternalMetadataReleaseDraftArtistCreditResponse> trackCredits = detail.Credits
            .Where(credit => !string.IsNullOrWhiteSpace(credit.Name))
            .Select(credit => new ExternalMetadataReleaseDraftArtistCreditResponse(credit.Name, credit.Role));

        return [.. mainArtists.Concat(trackCredits)];
    }

    private static ExternalMetadataTrackReleaseContextResponse ToReleaseContextResponse(ExternalMetadataReleaseContext release)
    {
        return new ExternalMetadataTrackReleaseContextResponse(release.Source, release.Title, release.Year, release.Artists);
    }

    private static int? ToDurationSeconds(TimeSpan? duration)
    {
        return duration is null ? null : (int)duration.Value.TotalSeconds;
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

    private sealed record ParsedTrackSearchRequest(ExternalMetadataTrackSearchQuery Query, IResult? Error)
    {
        public static ParsedTrackSearchRequest WithError(IResult error)
        {
            return new ParsedTrackSearchRequest(new ExternalMetadataTrackSearchQuery(), error);
        }
    }
}
