using DiscWeave.Domain.SharedKernel.Errors;
using DiscWeave.Domain.SharedKernel.Validation;

namespace DiscWeave.Domain.Collection;

public sealed record FilePath
{
    private FilePath(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static FilePath FromAbsolutePath(string path)
    {
        string value = Guard.RequiredText(path, nameof(path), "file_path.path_required");

        return !IsAbsolute(value)
            ? throw new DomainException("file_path.not_absolute", "File path must be absolute")
            : new FilePath(value);
    }

    private static bool IsAbsolute(string value)
    {
        return Path.IsPathFullyQualified(value)
            || value.StartsWith('/')
            || value.StartsWith('\\')
            || IsWindowsDriveAbsolutePath(value);
    }

    private static bool IsWindowsDriveAbsolutePath(string value)
    {
        return value.Length >= 3
            && char.IsAsciiLetter(value[0])
            && value[1] == ':'
            && value[2] is '\\' or '/';
    }

    public override string ToString()
    {
        return Value;
    }
}
