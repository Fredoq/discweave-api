namespace Cratebase.Application.Persistence;

public sealed class PersistenceConflictException : Exception
{
    public PersistenceConflictException(PersistenceConflictKind kind, string? constraintName, Exception innerException)
        : base("Persistence conflict occurred", innerException)
    {
        Kind = kind;
        ConstraintName = constraintName;
    }

    public PersistenceConflictException(PersistenceConflictKind kind, Exception innerException)
        : this(kind, null, innerException)
    {
    }

    public PersistenceConflictKind Kind { get; }

    public string? ConstraintName { get; }
}
