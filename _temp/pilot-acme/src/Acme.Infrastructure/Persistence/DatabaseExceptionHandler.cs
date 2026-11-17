using Acme.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Acme.Infrastructure.Persistence;

public static class DatabaseExceptionHandler
{
    public static void Handle(DbUpdateException exception)
    {
        if (exception.InnerException is not PostgresException postgresException) return;

        switch (postgresException.SqlState)
        {
            case PostgresErrorCodes.UniqueViolation:
                throw new DuplicateException(postgresException.ConstraintName, exception);
            case PostgresErrorCodes.ForeignKeyViolation:
                throw new ReferenceViolationException(postgresException.ConstraintName, exception);
            case PostgresErrorCodes.CheckViolation:
                throw new InvalidParameterException(postgresException.ConstraintName, "a check constraint rejected the value", exception);
            case PostgresErrorCodes.NotNullViolation:
                throw new InvalidParameterException(postgresException.ColumnName, "a value is required", exception);
        }
    }
}
