namespace Cratebase.Application.Errors;

public sealed class ResourceConflictException : Exception
{
    public const string RatingCriterionCode = "rating_criterion.code";
    public const string RatingValueTarget = "rating_value.target";

    public ResourceConflictException(string conflict, Exception innerException)
        : base("Resource already exists", innerException)
    {
        Conflict = conflict;
    }

    public string Conflict { get; }
}
