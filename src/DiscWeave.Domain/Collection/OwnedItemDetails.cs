using DiscWeave.Domain.SharedKernel.Optional;

namespace DiscWeave.Domain.Collection;

public sealed record OwnedItemDetails
{
    private OwnedItemDetails(IOptionalValue<ItemCondition>? condition, IOptionalValue<StorageLocation>? storageLocation)
    {
        Condition = condition ?? Optional.Missing<ItemCondition>();
        StorageLocation = storageLocation ?? Optional.Missing<StorageLocation>();
    }

    public IOptionalValue<ItemCondition> Condition { get; }

    public IOptionalValue<StorageLocation> StorageLocation { get; }

    public static OwnedItemDetails Empty { get; } = new(
        Optional.Missing<ItemCondition>(),
        Optional.Missing<StorageLocation>());

    public OwnedItemDetails WithCondition(ItemCondition condition)
    {
        return new OwnedItemDetails(Optional.From(condition), StorageLocation);
    }

    public OwnedItemDetails WithStorageLocation(StorageLocation storageLocation)
    {
        ArgumentNullException.ThrowIfNull(storageLocation);

        return new OwnedItemDetails(Condition, Optional.From(storageLocation));
    }
}
