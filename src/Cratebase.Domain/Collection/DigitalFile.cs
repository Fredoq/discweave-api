using Cratebase.Domain.SharedKernel.Errors;
using Cratebase.Domain.SharedKernel.Optional;
using Cratebase.Domain.SharedKernel.Validation;

namespace Cratebase.Domain.Collection;

public sealed record DigitalFile : IMedium
{
    private DigitalFile(FilePath path, AudioFileFormat format, IOptionalValue<FileImportIdentity> importIdentity)
    {
        Path = path;
        Format = format;
        ImportIdentity = importIdentity;
    }

    public FilePath Path { get; }

    public AudioFileFormat Format { get; }

    public IOptionalValue<FileImportIdentity> ImportIdentity { get; }

    public string Description => "digital file";

    public static DigitalFile Create(FilePath path, AudioFileFormat format)
    {
        ArgumentNullException.ThrowIfNull(path);

        return new DigitalFile(
            path,
            Guard.DefinedEnum(format, nameof(format), "digital_file.format_invalid"),
            Optional.Missing<FileImportIdentity>());
    }

    public static DigitalFile Create(FilePath path, AudioFileFormat format, FileImportIdentity importIdentity)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(importIdentity);
        AudioFileFormat validatedFormat = Guard.DefinedEnum(format, nameof(format), "digital_file.format_invalid");

        return importIdentity.Path != path
            ? throw new DomainException("digital_file.import_identity_path_mismatch", "Digital file import identity path must match the file path")
            : new DigitalFile(path, validatedFormat, Optional.From(importIdentity));
    }
}
