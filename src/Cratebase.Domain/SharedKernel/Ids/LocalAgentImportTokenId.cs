namespace Cratebase.Domain.SharedKernel.Ids;

public readonly record struct LocalAgentImportTokenId(Guid Value)
{
    public static LocalAgentImportTokenId New()
    {
        return new LocalAgentImportTokenId(Guid.CreateVersion7());
    }

    public override string ToString()
    {
        return Value.ToString();
    }
}
