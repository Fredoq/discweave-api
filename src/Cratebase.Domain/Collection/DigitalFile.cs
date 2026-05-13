using Cratebase.Domain.SharedKernel.Errors;
using Cratebase.Domain.SharedKernel.Optional;
using Cratebase.Domain.SharedKernel.Validation;

namespace Cratebase.Domain.Collection;

public sealed record DigitalFile : IMedium
{
    private DigitalFile(string code, FilePath path, AudioFileFormat format, IOptionalValue<FileImportIdentity> importIdentity)
    {
        Code = Guard.RequiredText(code, nameof(code), "medium.type_required");
        Path = path;
        Format = format;
        ImportIdentity = importIdentity;
    }

    public string Code { get; }

    public FilePath Path { get; }

    public AudioFileFormat Format { get; }

    public IOptionalValue<FileImportIdentity> ImportIdentity { get; }

    public string Description => "digital file";

    public static DigitalFile Create(FilePath path, AudioFileFormat format)
    {
        ArgumentNullException.ThrowIfNull(path);

        return Create("digital", path, format);
    }

    public static DigitalFile Create(string code, FilePath path, AudioFileFormat format)
    {
        ArgumentNullException.ThrowIfNull(path);

        return new DigitalFile(
            code,
            path,
            Guard.DefinedEnum(format, nameof(format), "digital_file.format_invalid"),
            Optional.Missing<FileImportIdentity>());
    }

    public static DigitalFile Create(FilePath path, AudioFileFormat format, FileImportIdentity importIdentity)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(importIdentity);

        return Create("digital", path, format, importIdentity);
    }

    public static DigitalFile Create(string code, FilePath path, AudioFileFormat format, FileImportIdentity importIdentity)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(importIdentity);
        AudioFileFormat validatedFormat = Guard.DefinedEnum(format, nameof(format), "digital_file.format_invalid");

        return importIdentity.Path != path
            ? throw new DomainException("digital_file.import_identity_path_mismatch", "Digital file import identity path must match the file path")
            : new DigitalFile(code, path, validatedFormat, Optional.From(importIdentity));
    }
}
