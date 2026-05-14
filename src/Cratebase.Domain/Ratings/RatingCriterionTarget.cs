using Cratebase.Domain.SharedKernel.Validation;

namespace Cratebase.Domain.Ratings;

public sealed class RatingCriterionTarget
{
    private RatingCriterionTarget()
    {
    }

    private RatingCriterionTarget(RatingTargetType targetType)
    {
        TargetType = Guard.DefinedEnum(targetType, nameof(targetType), "rating_target.type_invalid");
    }

    public RatingTargetType TargetType { get; private set; }

    public static RatingCriterionTarget Create(RatingTargetType targetType)
    {
        return new RatingCriterionTarget(targetType);
    }
}
