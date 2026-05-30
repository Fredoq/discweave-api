namespace DiscWeave.Domain.SharedKernel.Ids;

public readonly record struct PlaylistId(Guid Value)
{
    public static PlaylistId New()
    {
        return new PlaylistId(Guid.CreateVersion7());
    }

    public override string ToString()
    {
        return Value.ToString();
    }
}
