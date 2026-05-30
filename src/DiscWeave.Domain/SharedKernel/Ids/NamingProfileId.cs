namespace DiscWeave.Domain.SharedKernel.Ids;

public readonly record struct NamingProfileId(Guid Value)
{
    public static NamingProfileId New()
    {
        return new NamingProfileId(Guid.CreateVersion7());
    }

    public override string ToString()
    {
        return Value.ToString();
    }
}
