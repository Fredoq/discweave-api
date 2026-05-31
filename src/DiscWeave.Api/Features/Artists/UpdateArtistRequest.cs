using DiscWeave.Api.Features.ExternalSources;

namespace DiscWeave.Api.Features.Artists;

public sealed record UpdateArtistRequest(string Name)
{
    public IReadOnlyList<ExternalSourceReferenceRequest>? ExternalSources { get; init; }
}
