namespace DiscWeave.Domain.SharedKernel.Ids;

public readonly record struct OwnedItemId(Guid Value)
{
    public static OwnedItemId New()
    {
        return new OwnedItemId(Guid.CreateVersion7());
    }

    public override string ToString()
    {
        return Value.ToString();
    }
}
