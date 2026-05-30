namespace DiscWeave.Domain.SharedKernel.Ids;

public readonly record struct TrackRelationId(Guid Value)
{
    public static TrackRelationId New()
    {
        return new TrackRelationId(Guid.CreateVersion7());
    }

    public override string ToString()
    {
        return Value.ToString();
    }
}
