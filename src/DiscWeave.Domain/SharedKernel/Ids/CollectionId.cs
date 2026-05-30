namespace DiscWeave.Domain.SharedKernel.Ids;

public readonly record struct CollectionId
{
    public CollectionId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Collection id cannot be empty", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public static CollectionId New()
    {
        return new CollectionId(Guid.CreateVersion7());
    }

    public override string ToString()
    {
        return Value.ToString();
    }
}
