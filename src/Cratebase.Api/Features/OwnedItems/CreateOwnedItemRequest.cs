namespace Cratebase.Api.Features.OwnedItems;

public sealed record CreateOwnedItemRequest(
    string TargetType,
    Guid TargetId,
    string Status,
    MediumRequest Medium,
    string? Condition,
    string? StorageLocation);
