namespace DiscWeave.Domain.SharedKernel.Ids;

public readonly record struct CreditId(Guid Value)
{
    public static CreditId New()
    {
        return new CreditId(Guid.CreateVersion7());
    }

    public override string ToString()
    {
        return Value.ToString();
    }
}
