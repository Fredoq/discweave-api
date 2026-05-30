namespace DiscWeave.Domain.SharedKernel.Ids;

public readonly record struct TrackId(Guid Value)
{
    public static TrackId New()
    {
        return new TrackId(Guid.CreateVersion7());
    }

    public override string ToString()
    {
        return Value.ToString();
    }
}
