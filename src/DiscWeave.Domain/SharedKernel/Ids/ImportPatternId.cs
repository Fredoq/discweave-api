namespace DiscWeave.Domain.SharedKernel.Ids;

public readonly record struct ImportPatternId(Guid Value)
{
    public static ImportPatternId New()
    {
        return new ImportPatternId(Guid.CreateVersion7());
    }

    public override string ToString()
    {
        return Value.ToString();
    }
}
