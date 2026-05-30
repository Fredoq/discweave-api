namespace DiscWeave.Application.Errors;

public sealed class ReferencedResourceMissingException : Exception
{
    public ReferencedResourceMissingException(Exception innerException)
        : base("Referenced resource does not exist", innerException)
    {
    }
}
