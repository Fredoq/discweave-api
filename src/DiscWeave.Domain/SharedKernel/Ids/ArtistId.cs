namespace DiscWeave.Domain.SharedKernel.Ids;

public readonly record struct ArtistId(Guid Value)
{
    public static ArtistId New()
    {
        return new ArtistId(Guid.CreateVersion7());
    }

    public override string ToString()
    {
        return Value.ToString();
    }
}
