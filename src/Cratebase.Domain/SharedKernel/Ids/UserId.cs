namespace Cratebase.Domain.SharedKernel.Ids;

public readonly record struct UserId(Guid Value)
{
    public static UserId New()
    {
        return new UserId(Guid.CreateVersion7());
    }

    public override string ToString()
    {
        return Value.ToString();
    }
}
