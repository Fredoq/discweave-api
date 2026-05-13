namespace Cratebase.Domain.SharedKernel.Ids;

public readonly record struct CollectionDictionaryEntryId(Guid Value)
{
    public static CollectionDictionaryEntryId New()
    {
        return new CollectionDictionaryEntryId(Guid.CreateVersion7());
    }
}
