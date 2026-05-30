namespace DiscWeave.Domain.SharedKernel.Ids;

public readonly record struct ReleaseId(Guid Value)
{
    public static ReleaseId New()
    {
        return new ReleaseId(Guid.CreateVersion7());
    }

    public override string ToString()
    {
        return Value.ToString();
    }
}
