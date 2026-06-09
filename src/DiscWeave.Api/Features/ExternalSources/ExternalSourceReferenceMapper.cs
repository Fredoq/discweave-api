using DiscWeave.Domain.Catalog;

namespace DiscWeave.Api.Features.ExternalSources;

internal static class ExternalSourceReferenceMapper
{
    public static IReadOnlyList<ExternalSourceReference> FromRequests(
        IReadOnlyList<ExternalSourceReferenceRequest>? requests,
        DateTimeOffset defaultAppliedAt)
    {
        return requests is null
            ? []
            : [.. requests.Select(request => ExternalSourceReference.Create(
                request.ProviderName ?? string.Empty,
                request.ResourceType ?? string.Empty,
                request.ExternalId ?? string.Empty,
                request.SourceUrl ?? string.Empty,
                request.AppliedAt ?? defaultAppliedAt))];
    }

    public static IReadOnlyList<ExternalSourceReference> FromResponses(
        IReadOnlyList<ExternalSourceReferenceResponse>? responses)
    {
        return responses is null
            ? []
            : [.. responses.Select(response => ExternalSourceReference.Create(
                response.ProviderName,
                response.ResourceType,
                response.ExternalId,
                response.SourceUrl,
                response.AppliedAt))];
    }

    public static IReadOnlyList<ExternalSourceReferenceResponse> ToResponses(
        IReadOnlyList<ExternalSourceReference> sources)
    {
        return
        [
            .. sources
                .OrderBy(source => source.ProviderName, StringComparer.Ordinal)
                .ThenBy(source => source.ResourceType, StringComparer.Ordinal)
                .ThenBy(source => source.ExternalId, StringComparer.Ordinal)
                .Select(source => new ExternalSourceReferenceResponse(
                    source.ProviderName,
                    source.ResourceType,
                    source.ExternalId,
                    source.SourceUrl,
                    source.AppliedAt))
        ];
    }
}
