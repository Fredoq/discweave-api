using Cratebase.Api.Features.OwnedItems;

namespace Cratebase.Api.Features.Releases;

public sealed record ReleaseOwnedCopyRequest
{
    public string Status { get; init; } = string.Empty;

    public MediumRequest Medium { get; init; } = null!;

    public string? Condition { get; init; }

    public string? StorageLocation { get; init; }
}
