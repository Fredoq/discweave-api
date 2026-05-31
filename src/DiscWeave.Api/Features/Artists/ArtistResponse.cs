using DiscWeave.Api.Features.ExternalSources;

namespace DiscWeave.Api.Features.Artists;

public sealed record ArtistResponse(
    Guid Id,
    string Type,
    string Name,
    IReadOnlyList<ExternalSourceReferenceResponse>? ExternalSources);
