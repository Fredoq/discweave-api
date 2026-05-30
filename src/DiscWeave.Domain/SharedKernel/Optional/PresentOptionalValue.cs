namespace DiscWeave.Domain.SharedKernel.Optional;

public sealed record PresentOptionalValue<T> : IOptionalValue<T>
    where T : notnull
{
    public PresentOptionalValue(T value)
    {
        ArgumentNullException.ThrowIfNull(value);

        Value = value;
    }

    public bool HasValue => true;

    public T Value { get; }

    public TResult Match<TResult>(Func<T, TResult> whenPresent, Func<TResult> whenMissing)
        where TResult : notnull
    {
        ArgumentNullException.ThrowIfNull(whenPresent);
        ArgumentNullException.ThrowIfNull(whenMissing);

        return whenPresent(Value);
    }
}
