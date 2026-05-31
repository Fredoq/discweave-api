using DiscWeave.Api.Features.ExternalSources;

namespace DiscWeave.Api.Features.Artists;

public sealed record CreateArtistRequest(string Type, string Name)
{
    public IReadOnlyList<ExternalSourceReferenceRequest>? ExternalSources { get; init; }
}
