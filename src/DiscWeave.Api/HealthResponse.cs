namespace DiscWeave.Api;

internal sealed class HealthResponse
{
    public required string Service { get; init; }

    public required string Status { get; init; }
}
