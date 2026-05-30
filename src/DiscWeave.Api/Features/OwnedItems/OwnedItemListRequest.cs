namespace DiscWeave.Api.Features.OwnedItems;

public sealed class OwnedItemListRequest
{
    public string? Status { get; init; }

    public string? Medium { get; init; }

    public string? Condition { get; init; }

    public string? StorageLocation { get; init; }

    public string? InventoryView { get; init; }

    public int? Limit { get; init; }

    public int? Offset { get; init; }
}
