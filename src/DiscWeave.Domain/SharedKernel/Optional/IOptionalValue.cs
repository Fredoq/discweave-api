namespace DiscWeave.Domain.SharedKernel.Optional;

public interface IOptionalValue<T>
    where T : notnull
{
    bool HasValue { get; }

    TResult Match<TResult>(Func<T, TResult> whenPresent, Func<TResult> whenMissing)
        where TResult : notnull;
}
