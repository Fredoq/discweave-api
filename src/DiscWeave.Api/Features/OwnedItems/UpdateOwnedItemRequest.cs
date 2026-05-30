namespace DiscWeave.Api.Features.OwnedItems;

public sealed record UpdateOwnedItemRequest(
    string Status,
    string? Condition,
    string? StorageLocation,
    MediumRequest? Medium,
    string? TargetType,
    Guid? TargetId);
