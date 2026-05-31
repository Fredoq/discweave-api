namespace DiscWeave.Infrastructure.ExternalMetadata.Discogs;

public sealed class DiscogsOptions
{
    public bool Enabled { get; init; }

    public string BaseUrl { get; init; } = "https://api.discogs.com";

    public string UserAgent { get; init; } = "DiscWeave/0.1 (+https://discweave.example.com)";

    public int TimeoutSeconds { get; init; } = 10;

    public string? AccessToken { get; init; }
}
