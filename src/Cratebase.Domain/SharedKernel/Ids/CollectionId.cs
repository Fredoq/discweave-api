namespace Cratebase.Domain.SharedKernel.Ids;

public readonly record struct CollectionId(Guid Value)
{
    public static CollectionId New()
    {
        return new CollectionId(Guid.CreateVersion7());
    }

    public override string ToString()
    {
        return Value.ToString();
    }
}
