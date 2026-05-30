namespace DiscWeave.Domain.SharedKernel.Ids;

public readonly record struct ReleaseImportDraftId(Guid Value)
{
    public static ReleaseImportDraftId New()
    {
        return new ReleaseImportDraftId(Guid.CreateVersion7());
    }

    public override string ToString()
    {
        return Value.ToString();
    }
}
