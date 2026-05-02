namespace Cratebase.Application.Persistence;

public sealed class PersistenceConflictException : Exception
{
    public PersistenceConflictException(PersistenceConflictKind kind, Exception innerException)
        : base("Persistence conflict occurred", innerException)
    {
        Kind = kind;
    }

    public PersistenceConflictKind Kind { get; }
}
