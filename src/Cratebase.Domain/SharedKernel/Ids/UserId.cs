namespace Cratebase.Domain.SharedKernel.Ids;

public readonly record struct UserId
{
    public UserId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("User id cannot be empty", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public static UserId New()
    {
        return new UserId(Guid.CreateVersion7());
    }

    public override string ToString()
    {
        return Value.ToString();
    }
}
