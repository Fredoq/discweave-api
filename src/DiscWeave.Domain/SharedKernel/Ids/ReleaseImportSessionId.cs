namespace DiscWeave.Domain.SharedKernel.Ids;

public readonly record struct ReleaseImportSessionId(Guid Value)
{
    public static ReleaseImportSessionId New()
    {
        return new ReleaseImportSessionId(Guid.CreateVersion7());
    }

    public override string ToString()
    {
        return Value.ToString();
    }
}
