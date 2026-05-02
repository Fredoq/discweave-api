namespace Cratebase.Application.Errors;

public sealed class ResourceHasDependentsException : Exception
{
    public ResourceHasDependentsException(Exception innerException)
        : base("Resource has dependent data", innerException)
    {
    }
}
