namespace DiscWeave.Api.Features.OwnedItems;

public sealed record OwnedItemTargetResponse(
    string Type,
    Guid Id,
    string Title,
    string? Subtitle,
    Guid? ReleaseId,
    string? ReleaseTitle);
