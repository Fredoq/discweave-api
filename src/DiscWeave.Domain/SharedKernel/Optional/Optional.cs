namespace DiscWeave.Domain.SharedKernel.Optional;

public static class Optional
{
    public static IOptionalValue<T> Missing<T>()
        where T : notnull
    {
        return new MissingOptionalValue<T>();
    }

    public static IOptionalValue<T> From<T>(T value)
        where T : notnull
    {
        return new PresentOptionalValue<T>(value);
    }
}
