namespace Cratebase.Api.Features.OwnedItems;

public sealed record OwnedItemResponse(
    Guid Id,
    string TargetType,
    Guid TargetId,
    OwnedItemTargetResponse? Target,
    string Status,
    MediumResponse Medium,
    string? Condition,
    string? StorageLocation,
    IReadOnlyList<string> InventorySignals);
