using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Cratebase.Infrastructure.Persistence;

internal static class PostgresPersistenceErrors
{
    private const string RestrictViolationSqlState = "23001";

    public static bool IsReferencedResourceMissing(DbUpdateException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return FindPostgresException(exception)?.SqlState == PostgresErrorCodes.ForeignKeyViolation;
    }

    public static bool IsResourceHasDependents(DbUpdateException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return FindPostgresException(exception)?.SqlState == RestrictViolationSqlState;
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
