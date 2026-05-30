using DiscWeave.Api.Features.OwnedItems;

namespace DiscWeave.Api.Features.Releases;

public sealed record ReleaseOwnedCopyRequest
{
    public string Status { get; init; } = string.Empty;

    public required MediumRequest Medium { get; init; }

    public string? Condition { get; init; }

    public string? StorageLocation { get; init; }
}
