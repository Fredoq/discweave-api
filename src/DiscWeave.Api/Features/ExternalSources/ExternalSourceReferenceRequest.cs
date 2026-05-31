namespace DiscWeave.Api.Features.ExternalSources;

public sealed record ExternalSourceReferenceRequest
{
    public string? ProviderName { get; init; }

    public string? ResourceType { get; init; }

    public string? ExternalId { get; init; }

    public string? SourceUrl { get; init; }

    public DateTimeOffset? AppliedAt { get; init; }
}
