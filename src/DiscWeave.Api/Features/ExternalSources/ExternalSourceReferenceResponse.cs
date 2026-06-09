namespace DiscWeave.Api.Features.ExternalSources;

public sealed record ExternalSourceReferenceResponse(
    string ProviderName,
    string ResourceType,
    string ExternalId,
    string SourceUrl,
    DateTimeOffset AppliedAt);
