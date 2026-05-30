namespace DiscWeave.Domain.SharedKernel.Ids;

public readonly record struct LabelId(Guid Value)
{
    public static LabelId New()
    {
        return new LabelId(Guid.CreateVersion7());
    }

    public override string ToString()
    {
        return Value.ToString();
    }
}
