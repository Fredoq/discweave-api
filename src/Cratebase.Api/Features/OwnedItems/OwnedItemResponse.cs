namespace Cratebase.Api.Features.OwnedItems;

public sealed record OwnedItemResponse(
    Guid Id,
    string TargetType,
    Guid TargetId,
    string Status,
    MediumResponse Medium,
    string? Condition,
    string? StorageLocation);
