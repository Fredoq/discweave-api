using DiscWeave.Domain.SharedKernel.Errors;

namespace DiscWeave.Domain.SharedKernel.Validation;

internal static class Guard
{
    public static string RequiredText(string value, string fieldName, string code)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new DomainException(code, $"{fieldName} is required")
            : value.Trim();
    }

    public static int Positive(int value, string fieldName, string code)
    {
        return value <= 0
            ? throw new DomainException(code, $"{fieldName} must be positive")
            : value;
    }

    public static long Positive(long value, string fieldName, string code)
    {
        return value <= 0
            ? throw new DomainException(code, $"{fieldName} must be positive")
            : value;
    }

    public static TimeSpan Positive(TimeSpan value, string fieldName, string code)
    {
        return value <= TimeSpan.Zero
            ? throw new DomainException(code, $"{fieldName} must be positive")
            : value;
    }

    public static TEnum DefinedEnum<TEnum>(TEnum value, string fieldName, string code)
        where TEnum : struct, Enum
    {
        return Enum.IsDefined(value)
            ? value
            : throw new DomainException(code, $"{fieldName} is invalid");
    }
}
