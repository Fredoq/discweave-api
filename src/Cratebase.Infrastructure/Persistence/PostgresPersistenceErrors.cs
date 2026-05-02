using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Cratebase.Infrastructure.Persistence;

internal static class PostgresPersistenceErrors
{
    public static bool IsForeignKeyViolation(DbUpdateException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        Exception? current = exception;
        while (current is not null)
        {
            if (current is PostgresException postgresException &&
                (postgresException.SqlState == PostgresErrorCodes.ForeignKeyViolation ||
                    postgresException.MessageText.Contains("foreign key", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            current = current.InnerException;
        }

        return exception.ToString().Contains("foreign key", StringComparison.OrdinalIgnoreCase);
    }
}
