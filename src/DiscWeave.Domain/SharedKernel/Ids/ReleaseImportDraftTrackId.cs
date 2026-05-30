namespace DiscWeave.Domain.SharedKernel.Ids;

public readonly record struct ReleaseImportDraftTrackId(Guid Value)
{
    public static ReleaseImportDraftTrackId New()
    {
        return new ReleaseImportDraftTrackId(Guid.CreateVersion7());
    }

    public override string ToString()
    {
        return Value.ToString();
    }
}
