using Cratebase.Domain.SharedKernel.Errors;
using Cratebase.Domain.SharedKernel.Validation;

namespace Cratebase.Domain.Ratings;

public static class RatingTargetTypeCodes
{
    public static string ToCode(RatingTargetType targetType)
    {
        return Guard.DefinedEnum(targetType, nameof(targetType), "rating_target.type_invalid") switch
        {
            RatingTargetType.Artist => "artist",
            RatingTargetType.Release => "release",
            RatingTargetType.Track => "track",
            RatingTargetType.Label => "label",
            _ => throw new ArgumentOutOfRangeException(nameof(targetType), targetType, "Rating target type is not supported")
        };
    }

    public static RatingTargetType FromCode(string code)
    {
        return Guard.RequiredText(code, nameof(code), "rating_target.type_required") switch
        {
            "artist" => RatingTargetType.Artist,
            "release" => RatingTargetType.Release,
            "track" => RatingTargetType.Track,
            "label" => RatingTargetType.Label,
            _ => throw new DomainException("rating_target.type_invalid", "Rating target type is invalid")
        };
    }
}
