namespace DiscWeave.Infrastructure.ExternalMetadata.Discogs;

public sealed class DiscogsOptions
{
    public bool Enabled { get; init; }

    public string BaseUrl { get; init; } = "https://api.discogs.com";

    public string UserAgent { get; init; } = "DiscWeave/0.1 (+https://github.com/Fredoq/discweave-api)";

    public int TimeoutSeconds { get; init; } = 10;

    public string? AccessToken { get; init; }
}

internal static class DiscogsOptionsValidator
{
    public static bool IsValid(DiscogsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return !options.Enabled || (
            Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _) &&
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
