namespace DiscWeave.Infrastructure.ExternalMetadata.Discogs;

internal static class DiscogsOptionsValidator
{
    public static bool IsValid(DiscogsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return !options.Enabled || (
            Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out Uri? baseUrl) &&
            string.Equals(baseUrl.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
            options.TimeoutSeconds is >= 1 and <= 60 &&
            !string.IsNullOrWhiteSpace(options.AccessToken) &&
            CanParseUserAgent(options.UserAgent));
    }

    public static bool CanParseUserAgent(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return false;
        }

        using var request = new HttpRequestMessage();
        return request.Headers.UserAgent.TryParseAdd(userAgent);
    }
}
