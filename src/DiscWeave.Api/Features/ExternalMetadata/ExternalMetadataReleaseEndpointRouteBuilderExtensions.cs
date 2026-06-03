using System.Globalization;
using DiscWeave.Api.Auth;
using DiscWeave.Api.Http;
using DiscWeave.Application.ExternalMetadata;
using Microsoft.Extensions.Primitives;

namespace DiscWeave.Api.Features.ExternalMetadata;

public static class ExternalMetadataReleaseEndpointRouteBuilderExtensions
{
    private const int DefaultLimit = 25;
    private const int MaximumLimit = 100;

    public static IEndpointRouteBuilder MapExternalMetadataReleaseEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder group = endpoints.MapGroup("/api/external-metadata/discogs/releases")
            .WithTags("External Metadata")
            .RequireAuthorization(DiscWeaveAuthorizationPolicies.CollectionMember);
        _ = group.MapGet("", SearchReleasesAsync).WithName("SearchExternalDiscogsReleases");
        _ = group.MapGet("/{externalId}", GetReleaseAsync).WithName("GetExternalDiscogsRelease");

        return endpoints;
    }

    private static async Task<IResult> SearchReleasesAsync(
        HttpRequest request,
        IExternalMetadataProvider provider,
        CancellationToken cancellationToken)
    {
        ParsedReleaseSearchRequest parsedRequest = ParseReleaseSearchRequest(request);
        if (parsedRequest.Error is not null)
        {
            return parsedRequest.Error;
        }

        ExternalMetadataResult<ExternalMetadataSearchResult<ExternalMetadataReleaseCandidate>> result =
            await provider.SearchReleasesAsync(parsedRequest.Query, cancellationToken);
        return result.IsSuccess
            ? Results.Ok(new ExternalMetadataSearchResponse<ExternalMetadataReleaseCandidateResponse>(
            [.. result.Value.Items.Select(ToCandidateResponse)],
            parsedRequest.Query.Limit,
            result.Value.Total ?? result.Value.Items.Count))
            : ExternalMetadataEndpointErrors.ToHttpResult(result.Error);
    }

    private static async Task<IResult> GetReleaseAsync(
        string externalId,
        IExternalMetadataProvider provider,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(externalId))
        {
            return EndpointErrors.BadRequest("external_metadata.release.external_id_invalid", "External release id is required");
        }

        ExternalMetadataResult<ExternalMetadataReleaseDetail> result =
            await provider.GetReleaseAsync(new ExternalMetadataLookupQuery(externalId.Trim()), cancellationToken);

        return result.IsSuccess
            ? Results.Ok(ToDetailResponse(result.Value))
            : ExternalMetadataEndpointErrors.ToHttpResult(result.Error);
    }

    private static ParsedReleaseSearchRequest ParseReleaseSearchRequest(HttpRequest request)
    {
        IQueryCollection values = request.Query;
        string? query = PreferQuery(values, "query", "q");

        if (!TryReadInt(values, "year", out int? year))
        {
            return ParsedReleaseSearchRequest.WithError(
                EndpointErrors.BadRequest("external_metadata.release.year_invalid", "Release search year must be a positive integer"));
        }

        if (year is <= 0)
        {
            return ParsedReleaseSearchRequest.WithError(
                EndpointErrors.BadRequest("external_metadata.release.year_invalid", "Release search year must be a positive integer"));
        }

        if (!TryReadInt(values, "limit", out int? limit) || limit is < 1 or > MaximumLimit)
        {
            return ParsedReleaseSearchRequest.WithError(
                EndpointErrors.BadRequest("external_metadata.release.limit_invalid", "Release search limit must be between 1 and 100"));
        }

        string? artist = QueryValue(values, "artist")?.Trim();
        string? title = QueryValue(values, "title")?.Trim();
        string? barcode = QueryValue(values, "barcode")?.Trim();
        string? catalogNumber = QueryValue(values, "catalogNumber")?.Trim();
        return IsBlank(query) && IsBlank(artist) && IsBlank(title) && IsBlank(barcode) && IsBlank(catalogNumber)
            ? ParsedReleaseSearchRequest.WithError(
                EndpointErrors.BadRequest("external_metadata.release.criteria_required", "Release search criteria are required"))
            : new ParsedReleaseSearchRequest(
            new ExternalMetadataReleaseSearchQuery(
                TrimOrNull(query),
                TrimOrNull(artist),
                TrimOrNull(title),
                year,
                TrimOrNull(barcode),
                TrimOrNull(catalogNumber),
                limit ?? DefaultLimit),
            null);
    }

    private static ExternalMetadataReleaseCandidateResponse ToCandidateResponse(ExternalMetadataReleaseCandidate candidate)
    {
        return new ExternalMetadataReleaseCandidateResponse(
            candidate.Source,
            candidate.Title,
            candidate.Artists,
            candidate.Year,
            candidate.Labels,
            candidate.Formats,
            candidate.CatalogNumber,
            candidate.Barcodes);
    }

    private static ExternalMetadataReleaseDetailResponse ToDetailResponse(ExternalMetadataReleaseDetail detail)
    {
        ExternalMetadataReleaseTrackResponse[] tracklist = [.. detail.Tracklist.Select(ToTrackResponse)];
        ExternalMetadataReleaseIdentifierResponse[] identifiers = [.. detail.Identifiers.Select(ToIdentifierResponse)];
        string[] barcodes = [.. detail.Identifiers
            .Where(identifier => string.Equals(identifier.Type, "Barcode", StringComparison.OrdinalIgnoreCase))
            .Select(identifier => identifier.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())];
        ExternalMetadataReleaseCreditResponse[] credits = [.. detail.Credits.Select(ToCreditResponse)];

        return new ExternalMetadataReleaseDetailResponse(
            detail.Source,
            detail.Title,
            detail.Artists,
            detail.Year,
            detail.Labels,
            detail.Formats,
            tracklist,
            identifiers,
            barcodes,
            detail.CatalogNumber,
            credits,
            ToDraftResponse(detail));
    }

    private static ExternalMetadataReleaseTrackResponse ToTrackResponse(ExternalMetadataReleaseTrack track)
    {
        return new ExternalMetadataReleaseTrackResponse(
            track.Title,
            track.Position,
            ToDurationSeconds(track.Duration),
            track.Artists);
    }

    private static ExternalMetadataReleaseIdentifierResponse ToIdentifierResponse(ExternalMetadataIdentifier identifier)
    {
        return new ExternalMetadataReleaseIdentifierResponse(identifier.Type, identifier.Value);
    }

    private static ExternalMetadataReleaseCreditResponse ToCreditResponse(ExternalMetadataReleaseCredit credit)
    {
        return new ExternalMetadataReleaseCreditResponse(credit.Name, credit.Role, credit.TrackTitle, credit.TrackPosition);
    }

    private static ExternalMetadataReleaseDraftResponse ToDraftResponse(ExternalMetadataReleaseDetail detail)
    {
        return new ExternalMetadataReleaseDraftResponse(
            detail.Title,
            detail.Type,
            detail.Genres,
            detail.Year ?? detail.ReleaseDate?.Year,
            detail.ReleaseDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            [.. detail.Artists.Select(artist => new ExternalMetadataReleaseDraftArtistCreditResponse(artist, "mainArtist"))],
            DraftLabels(detail),
            [.. detail.Tracklist.Select((track, index) => ToDraftTrackResponse(detail, track, index + 1))],
            [new ExternalMetadataDraftExternalSourceResponse(
                detail.Source.ProviderName,
                detail.Source.ResourceType,
                detail.Source.ExternalId,
                detail.Source.SourceUrl)]);
    }

    private static IReadOnlyList<ExternalMetadataReleaseDraftLabelResponse> DraftLabels(ExternalMetadataReleaseDetail detail)
    {
        return detail.LabelDetails.Count > 0
            ? [.. detail.LabelDetails.Select(label => new ExternalMetadataReleaseDraftLabelResponse(
                label.Name,
                label.CatalogNumber,
                string.IsNullOrWhiteSpace(label.CatalogNumber)))]
            : [.. detail.Labels.Select((label, index) => new ExternalMetadataReleaseDraftLabelResponse(
                label,
                index == 0 ? detail.CatalogNumber : null,
                index != 0 || string.IsNullOrWhiteSpace(detail.CatalogNumber)))];
    }

    private static ExternalMetadataReleaseDraftTrackResponse ToDraftTrackResponse(
        ExternalMetadataReleaseDetail detail,
        ExternalMetadataReleaseTrack track,
        int position)
    {
        return new ExternalMetadataReleaseDraftTrackResponse(
            track.Title,
            position,
            ToDurationSeconds(track.Duration),
            DraftTrackCredits(detail, track));
    }

    private static ExternalMetadataReleaseDraftArtistCreditResponse[] DraftTrackCredits(
        ExternalMetadataReleaseDetail detail,
        ExternalMetadataReleaseTrack track)
    {
        IEnumerable<ExternalMetadataReleaseDraftArtistCreditResponse> mainArtists = track.Artists
            .Where(artist => !string.IsNullOrWhiteSpace(artist))
            .Select(artist => new ExternalMetadataReleaseDraftArtistCreditResponse(artist, "mainArtist"));
        IEnumerable<ExternalMetadataReleaseDraftArtistCreditResponse> trackCredits = detail.Credits
            .Where(credit => TrackCreditMatches(credit, track))
            .Where(credit => !string.IsNullOrWhiteSpace(credit.Name))
            .Select(credit => new ExternalMetadataReleaseDraftArtistCreditResponse(credit.Name, credit.Role));

        return [.. mainArtists.Concat(trackCredits)];
    }

    private static bool TrackCreditMatches(ExternalMetadataReleaseCredit credit, ExternalMetadataReleaseTrack track)
    {
        return string.Equals(credit.TrackPosition, track.Position, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(credit.TrackTitle, track.Title, StringComparison.OrdinalIgnoreCase);
    }

    private static int? ToDurationSeconds(TimeSpan? duration)
    {
        return duration is null ? null : (int)duration.Value.TotalSeconds;
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

        bool parsed = int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out int result);
        value = parsed ? result : null;

        return parsed;
    }

    private static string? TrimOrNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static bool IsBlank(string? value)
    {
        return string.IsNullOrWhiteSpace(value);
    }

    private sealed record ParsedReleaseSearchRequest(ExternalMetadataReleaseSearchQuery Query, IResult? Error)
    {
        public static ParsedReleaseSearchRequest WithError(IResult error)
        {
            return new ParsedReleaseSearchRequest(new ExternalMetadataReleaseSearchQuery(), error);
        }
    }
}
