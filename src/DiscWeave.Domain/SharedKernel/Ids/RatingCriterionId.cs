namespace DiscWeave.Domain.SharedKernel.Ids;

public readonly record struct RatingCriterionId(Guid Value)
{
    public static RatingCriterionId New()
    {
        return new RatingCriterionId(Guid.CreateVersion7());
    }

    public override string ToString()
    {
        return Value.ToString();
    }
}
