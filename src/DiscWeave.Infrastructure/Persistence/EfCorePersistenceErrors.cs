namespace DiscWeave.Infrastructure.Persistence;

internal static class EfCorePersistenceErrors
{
    public static bool IsRequiredRelationshipConflict(InvalidOperationException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception.Message.Contains("association between entity types", StringComparison.OrdinalIgnoreCase) &&
            exception.Message.Contains("has been severed", StringComparison.OrdinalIgnoreCase);
    }
}
