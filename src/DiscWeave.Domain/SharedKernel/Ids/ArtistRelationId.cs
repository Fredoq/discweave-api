namespace DiscWeave.Domain.SharedKernel.Ids;

public readonly record struct ArtistRelationId(Guid Value)
{
    public static ArtistRelationId New()
    {
        return new ArtistRelationId(Guid.CreateVersion7());
    }

    public override string ToString()
    {
        return Value.ToString();
    }
}
