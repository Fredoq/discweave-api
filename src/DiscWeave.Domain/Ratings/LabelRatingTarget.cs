using DiscWeave.Domain.SharedKernel.Ids;

namespace DiscWeave.Domain.Ratings;

public sealed class LabelRatingTarget : RatingTarget
{
    private LabelRatingTarget(LabelId labelId)
    {
        LabelId = labelId;
    }

    public override RatingTargetType Type => RatingTargetType.Label;

    public LabelId LabelId { get; }

    public static LabelRatingTarget Create(LabelId labelId)
    {
        return new LabelRatingTarget(labelId);
    }
}
