namespace Cratebase.Domain.SharedKernel.Ids;

public readonly record struct RatingValueId(Guid Value)
{
    public static RatingValueId New()
    {
        return new RatingValueId(Guid.CreateVersion7());
    }

    public override string ToString()
    {
        return Value.ToString();
    }
}
