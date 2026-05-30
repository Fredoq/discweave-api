namespace DiscWeave.Domain.SharedKernel.Optional;

public sealed record MissingOptionalValue<T> : IOptionalValue<T>
    where T : notnull
{
    public bool HasValue => false;

    public TResult Match<TResult>(Func<T, TResult> whenPresent, Func<TResult> whenMissing)
        where TResult : notnull
    {
        ArgumentNullException.ThrowIfNull(whenPresent);
        ArgumentNullException.ThrowIfNull(whenMissing);

        return whenMissing();
    }
}
