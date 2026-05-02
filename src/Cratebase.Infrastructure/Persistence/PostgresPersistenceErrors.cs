using Microsoft.EntityFrameworkCore;
using Npgsql;
using Cratebase.Application.Persistence;

namespace Cratebase.Infrastructure.Persistence;

internal static class PostgresPersistenceErrors
{
    private const string RestrictViolationSqlState = "23001";

    public static bool IsReferentialIntegrityViolation(DbUpdateException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return GetReferentialIntegrityConstraintName(exception) is not null;
    }

    public static string? GetReferentialIntegrityConstraintName(DbUpdateException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        Exception? current = exception;
        while (current is not null)
        {
            if (current is PostgresException postgresException &&
                IsReferentialIntegritySqlState(postgresException.SqlState))
            {
                return postgresException.ConstraintName ?? string.Empty;
            }

            current = current.InnerException;
        }

        return exception.Message.Contains("foreign key", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : null;
    }

    public static PersistenceConflictKind GetReferentialIntegrityKind(DbUpdateException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        Exception? current = exception;
        while (current is not null)
        {
            if (current is PostgresException postgresException)
            {
                return postgresException.SqlState == RestrictViolationSqlState
                    ? PersistenceConflictKind.ReferentialIntegrityViolation
                    : PersistenceConflictKind.ForeignKeyViolation;
            }

            current = current.InnerException;
        }

        return PersistenceConflictKind.ReferentialIntegrityViolation;
    }

    private static bool IsReferentialIntegritySqlState(string sqlState)
    {
        return sqlState is PostgresErrorCodes.ForeignKeyViolation or RestrictViolationSqlState;
    }
}
