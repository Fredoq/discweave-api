using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DiscWeave.Infrastructure.Persistence;

internal static class PostgresPersistenceErrors
{
    private const string IntegrityConstraintSqlStateClass = "23";
    private const string RestrictViolationSqlState = "23001";
    private const string RatingValueTargetUniqueIndexPrefix = "IX_rating_values_collection_id_criterion_id_target_type_targe";

    public static bool IsReferencedResourceMissing(DbUpdateException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return FindPostgresException(exception)?.SqlState == PostgresErrorCodes.ForeignKeyViolation;
    }

    public static bool IsUniqueConstraintViolation(DbUpdateException exception, string constraintName)
    {
        ArgumentNullException.ThrowIfNull(exception);

        PostgresException? postgresException = FindPostgresException(exception);

        return postgresException?.SqlState == PostgresErrorCodes.UniqueViolation
            && string.Equals(postgresException.ConstraintName, constraintName, StringComparison.Ordinal);
    }

    public static bool IsRatingValueTargetConflict(DbUpdateException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        PostgresException? postgresException = FindPostgresException(exception);

        return postgresException?.SqlState == PostgresErrorCodes.UniqueViolation
            && postgresException.ConstraintName?.StartsWith(RatingValueTargetUniqueIndexPrefix, StringComparison.Ordinal) == true;
    }

    public static bool IsResourceHasDependents(DbUpdateException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return FindPostgresException(exception)?.SqlState == RestrictViolationSqlState;
    }

    public static bool IsIntegrityConstraintViolation(DbUpdateException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return FindPostgresException(exception)?.SqlState.StartsWith(IntegrityConstraintSqlStateClass, StringComparison.Ordinal) == true;
    }

    private static PostgresException? FindPostgresException(DbUpdateException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        Exception? current = exception;
        while (current is not null)
        {
            if (current is PostgresException postgresException)
            {
                return postgresException;
            }

            current = current.InnerException;
        }

        return null;
    }
}
