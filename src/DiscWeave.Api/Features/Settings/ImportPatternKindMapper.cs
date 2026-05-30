using DiscWeave.Domain.Imports;
using DiscWeave.Domain.SharedKernel.Errors;

namespace DiscWeave.Api.Features.Settings;

internal static class ImportPatternKindMapper
{
    public static ImportPatternKind Parse(string? kind)
    {
        return string.IsNullOrWhiteSpace(kind)
            ? throw new DomainException("import_pattern.kind_required", "Import pattern kind is required")
            : kind.Trim() switch
            {
                "releaseFolder" => ImportPatternKind.ReleaseFolder,
                "trackFile" => ImportPatternKind.TrackFile,
                _ => throw new DomainException("import_pattern.kind_invalid", "Import pattern kind is invalid")
            };
    }

    public static string ToCode(ImportPatternKind kind)
    {
        return kind switch
        {
            ImportPatternKind.ReleaseFolder => "releaseFolder",
            ImportPatternKind.TrackFile => "trackFile",
            _ => throw new InvalidOperationException("Import pattern kind is not supported")
        };
    }
}
