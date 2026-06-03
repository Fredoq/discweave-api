using System.Globalization;
using System.Text.Json;
using DiscWeave.Application.ExternalMetadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DiscWeave.Infrastructure.ExternalMetadata.Discogs;

public sealed partial class DiscogsExternalMetadataProvider : IExternalMetadataProvider
{
    private const string ProviderNameValue = "discogs";
    private const string Attribution = "Data provided by Discogs.";
    private static readonly Dictionary<string, string> EmptyParameters = new(StringComparer.Ordinal);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly ILogger<DiscogsExternalMetadataProvider> _logger;
    private readonly DiscogsOptions _options;

    public DiscogsExternalMetadataProvider(HttpClient httpClient, IOptions<DiscogsOptions> options)
        : this(httpClient, options, NullLogger<DiscogsExternalMetadataProvider>.Instance)
    {
    }

    [ActivatorUtilitiesConstructor]
    public DiscogsExternalMetadataProvider(
        HttpClient httpClient,
        IOptions<DiscogsOptions> options,
        ILogger<DiscogsExternalMetadataProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;
    }

    public string ProviderName => ProviderNameValue;

    public async Task<ExternalMetadataResult<ExternalMetadataSearchResult<ExternalMetadataReleaseCandidate>>> SearchReleasesAsync(
        ExternalMetadataReleaseSearchQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        ExternalMetadataError? configurationError = TryValidateConfiguration();
        if (configurationError is not null)
        {
            return new ExternalMetadataResult<ExternalMetadataSearchResult<ExternalMetadataReleaseCandidate>>(configurationError);
        }

        Dictionary<string, string> parameters = SearchParameters(query.Limit, "release");
        Add(parameters, "q", query.Query);
        Add(parameters, "artist", query.Artist);
        Add(parameters, "release_title", query.Title);
        Add(parameters, "year", query.Year?.ToString(CultureInfo.InvariantCulture));
        Add(parameters, "barcode", query.Barcode);
        Add(parameters, "catno", query.CatalogNumber);

        ExternalMetadataResult<DiscogsSearchResponse> response = await SendAsync<DiscogsSearchResponse>(
            "/database/search",
            parameters,
            cancellationToken);
        if (!response.IsSuccess)
        {
            return new ExternalMetadataResult<ExternalMetadataSearchResult<ExternalMetadataReleaseCandidate>>(response.Error);
        }

        ExternalMetadataReleaseCandidate[] candidates =
        [
            .. response.Value.Results
                .Where(result => string.Equals(result.Type, "release", StringComparison.OrdinalIgnoreCase))
                .Select(MapReleaseCandidate)
        ];

        return new ExternalMetadataResult<ExternalMetadataSearchResult<ExternalMetadataReleaseCandidate>>(
            new ExternalMetadataSearchResult<ExternalMetadataReleaseCandidate>(candidates, response.Value.Pagination?.Items));
    }

    public async Task<ExternalMetadataResult<ExternalMetadataReleaseDetail>> GetReleaseAsync(
        ExternalMetadataLookupQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        ExternalMetadataError? configurationError = TryValidateConfiguration();
        if (configurationError is not null)
        {
            return new ExternalMetadataResult<ExternalMetadataReleaseDetail>(configurationError);
        }

        ExternalMetadataResult<DiscogsReleaseDetailResponse> response = await SendAsync<DiscogsReleaseDetailResponse>(
            $"/releases/{Uri.EscapeDataString(query.ExternalId)}",
            EmptyParameters,
            cancellationToken);

        return response.IsSuccess
            ? new ExternalMetadataResult<ExternalMetadataReleaseDetail>(MapReleaseDetail(response.Value))
            : new ExternalMetadataResult<ExternalMetadataReleaseDetail>(response.Error);
    }

    public async Task<ExternalMetadataResult<ExternalMetadataSearchResult<ExternalMetadataArtistCandidate>>> SearchArtistsAsync(
        ExternalMetadataArtistSearchQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        ExternalMetadataError? configurationError = TryValidateConfiguration();
        if (configurationError is not null)
        {
            return new ExternalMetadataResult<ExternalMetadataSearchResult<ExternalMetadataArtistCandidate>>(configurationError);
        }

        Dictionary<string, string> parameters = SearchParameters(query.Limit, "artist");
        Add(parameters, "q", query.Query);

        ExternalMetadataResult<DiscogsSearchResponse> response = await SendAsync<DiscogsSearchResponse>(
            "/database/search",
            parameters,
            cancellationToken);
        if (!response.IsSuccess)
        {
            return new ExternalMetadataResult<ExternalMetadataSearchResult<ExternalMetadataArtistCandidate>>(response.Error);
        }

        ExternalMetadataArtistCandidate[] candidates =
        [
            .. response.Value.Results
                .Where(result => string.Equals(result.Type, "artist", StringComparison.OrdinalIgnoreCase))
                .Select(MapArtistCandidate)
        ];

        return new ExternalMetadataResult<ExternalMetadataSearchResult<ExternalMetadataArtistCandidate>>(
            new ExternalMetadataSearchResult<ExternalMetadataArtistCandidate>(candidates, response.Value.Pagination?.Items));
    }

    public async Task<ExternalMetadataResult<ExternalMetadataArtistDetail>> GetArtistAsync(
        ExternalMetadataLookupQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        ExternalMetadataError? configurationError = TryValidateConfiguration();
        if (configurationError is not null)
        {
            return new ExternalMetadataResult<ExternalMetadataArtistDetail>(configurationError);
        }

        ExternalMetadataResult<DiscogsArtistDetailResponse> response = await SendAsync<DiscogsArtistDetailResponse>(
            $"/artists/{Uri.EscapeDataString(query.ExternalId)}",
            EmptyParameters,
            cancellationToken);

        return response.IsSuccess
            ? new ExternalMetadataResult<ExternalMetadataArtistDetail>(MapArtistDetail(response.Value))
            : new ExternalMetadataResult<ExternalMetadataArtistDetail>(response.Error);
    }

    private ExternalMetadataError? TryValidateConfiguration()
    {
        return !_options.Enabled
            ? Disabled()
            : (string.IsNullOrWhiteSpace(_options.AccessToken) ||
                string.IsNullOrWhiteSpace(_options.UserAgent) ||
                !Uri.TryCreate(_options.BaseUrl, UriKind.Absolute, out _))
                ? NotConfigured()
                : null;
    }

    private static Uri BuildUri(string path, Dictionary<string, string> parameters)
    {
        string relativePath = path.StartsWith('/') ? path[1..] : path;
        if (parameters.Count == 0)
        {
            return new Uri(relativePath, UriKind.Relative);
        }

        string query = string.Join(
            "&",
            parameters.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));

        return new Uri($"{relativePath}?{query}", UriKind.Relative);
    }

    private static Dictionary<string, string> SearchParameters(int limit, string type)
    {
        Dictionary<string, string> parameters = new(StringComparer.Ordinal)
        {
            ["type"] = type,
            ["per_page"] = Math.Clamp(limit, 1, 100).ToString(CultureInfo.InvariantCulture)
        };

        return parameters;
    }

    private static void Add(Dictionary<string, string> parameters, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            parameters[name] = value.Trim();
        }
    }
}
