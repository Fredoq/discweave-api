using System.Globalization;
using System.Text.RegularExpressions;
using Cratebase.Domain.Collection;
using Cratebase.Domain.Imports;
using Cratebase.Importing;

namespace Cratebase.Api.Features.Imports;

public static partial class ReleaseImportScanService
{
    private const long MaxCoverArtifactSizeBytes = 10 * 1024 * 1024;

    private static CoverSelection SelectCover(string releaseRootRelativePath, IReadOnlyList<DesktopScanFile> coverFiles)
    {
        DesktopScanFile[] candidates =
        [
            .. coverFiles.Where(file => string.Equals(DirectoryRelativePath(file.RelativePath), releaseRootRelativePath, StringComparison.OrdinalIgnoreCase))
        ];
        if (candidates.Length == 0)
        {
            return new CoverSelection(null, null, []);
        }

        DesktopScanFile[] priority =
        [
            .. candidates.Where(file => IsNamed(file.RelativePath, "cover")),
            .. candidates.Where(file => IsNamed(file.RelativePath, "folder")),
            .. candidates.Where(file => IsNamed(file.RelativePath, "front")),
            .. candidates.Where(file => Path.GetFileName(file.RelativePath).StartsWith("AlbumArt", StringComparison.OrdinalIgnoreCase) &&
                Path.GetFileName(file.RelativePath).Contains("Large", StringComparison.OrdinalIgnoreCase)),
            .. candidates.OrderByDescending(file => file.Request.SizeBytes)
        ];
        DesktopScanFile selected = priority.DistinctBy(file => file.FilePath, StringComparer.OrdinalIgnoreCase).First();

        List<ImportReviewIssue> issues = candidates.Length > 1
            ? [new ImportReviewIssue("import.cover_multiple_candidates", "Multiple cover image candidates were found")]
            : [];

        CoverArtifactPayload? artifact = CreateCoverArtifact(selected);
        if (selected.Request.CoverArtifact is not null && artifact is null)
        {
            issues.Add(new ImportReviewIssue("release_import.cover_too_large", "Selected cover image is too large to attach to the import draft"));
        }

        return new CoverSelection(selected, artifact, issues);
    }

    private static CoverArtifactPayload? CreateCoverArtifact(DesktopScanFile selected)
    {
        DesktopCoverArtifactRequest? artifact = selected.Request.CoverArtifact;
        if (artifact is null || artifact.SizeBytes > MaxCoverArtifactSizeBytes)
        {
            return null;
        }

        string extension = NormalizeExtension(artifact.Extension);
        return new CoverArtifactPayload(
            selected.FilePath,
            string.IsNullOrWhiteSpace(artifact.FileName) ? Path.GetFileName(selected.FilePath) : artifact.FileName.Trim(),
            extension,
            string.IsNullOrWhiteSpace(artifact.ContentType) ? ReleaseImportFileRules.CoverContentType(extension) : artifact.ContentType.Trim(),
            artifact.SizeBytes,
            artifact.ContentBase64);
    }

    private static bool TryGetAudioFormat(DesktopFolderScanFileRequest file, out AudioFileFormat format)
    {
        string? code = TrimOrNull(file.Format);
        if (code is not null)
        {
            switch (code.ToLowerInvariant())
            {
                case "flac":
                    format = AudioFileFormat.Flac;
                    return true;
                case "mp3":
                    format = AudioFileFormat.Mp3;
                    return true;
                case "wav":
                    format = AudioFileFormat.Wav;
                    return true;
                case "ogg":
                    format = AudioFileFormat.Ogg;
                    return true;
                case "m4a":
                    format = AudioFileFormat.M4a;
                    return true;
                default:
                    break;
            }
        }

        string path = !string.IsNullOrWhiteSpace(file.FilePath) ? file.FilePath : file.RelativePath;
        if (ReleaseImportFileRules.IsSupportedAudio(path))
        {
            format = ReleaseImportFileRules.FormatFromPath(path);
            return true;
        }

        format = default;
        return false;
    }

    private static bool IsSupportedCover(DesktopFolderScanFileRequest file)
    {
        string path = !string.IsNullOrWhiteSpace(file.FilePath) ? file.FilePath : file.RelativePath;
        return ReleaseImportFileRules.IsSupportedCover(path) ||
            (file.CoverArtifact is { Extension: { } extension } && ReleaseImportFileRules.SupportedCoverExtensions.Contains(NormalizeExtension(extension)));
    }

    private static string ReleaseRootFor(string audioRelativePath, IReadOnlyList<DesktopScanFile> audioFiles)
    {
        string audioDirectory = DirectoryRelativePath(audioRelativePath);
        if (string.IsNullOrWhiteSpace(audioDirectory))
        {
            return string.Empty;
        }

        string parent = ParentRelativePath(audioDirectory);
        return IsDiscDirectory(LastSegment(audioDirectory)) && ParentContainsOnlyDiscAudioDirectories(parent, audioFiles)
            ? parent
            : audioDirectory;
    }

    private static bool ParentContainsOnlyDiscAudioDirectories(string parent, IReadOnlyList<DesktopScanFile> audioFiles)
    {
        string[] childDirectories =
        [
            .. audioFiles
                .Select(file => ImmediateChildDirectory(parent, DirectoryRelativePath(file.RelativePath)))
                .Where(child => !string.IsNullOrWhiteSpace(child))
                .Distinct(StringComparer.OrdinalIgnoreCase)
        ];

        return childDirectories.Length > 0 && childDirectories.All(child => IsDiscDirectory(LastSegment(child)));
    }

    private static string ImmediateChildDirectory(string parent, string directory)
    {
        if (string.IsNullOrWhiteSpace(parent))
        {
            return FirstSegment(directory);
        }

        if (!directory.StartsWith(parent + "/", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        string remainder = directory[(parent.Length + 1)..];
        string first = FirstSegment(remainder);
        return string.IsNullOrWhiteSpace(first) ? string.Empty : $"{parent}/{first}";
    }

    private static ImportDateResult ParseReleaseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new ImportDateResult(null, null, []);
        }

        string trimmed = value.Trim();
        int? leadingYear = trimmed.Length >= 4 && int.TryParse(trimmed[..4], CultureInfo.InvariantCulture, out int year)
            ? year
            : null;

        if (leadingYear is { } partialYear && trimmed.EndsWith("-00-00", StringComparison.Ordinal))
        {
            return new ImportDateResult(
                null,
                partialYear,
                [new ImportReviewIssue(ImportIssueCodes.PartialReleaseDate, "Release date has unknown month or day")]);
        }

        DateOnly? parsedReleaseDate = null;
        int? parsedYear = leadingYear;
        IReadOnlyList<ImportReviewIssue> issues = [];

        if (DateOnly.TryParseExact(trimmed, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly releaseDate))
        {
            parsedReleaseDate = releaseDate;
            parsedYear = releaseDate.Year;
        }
        else if (trimmed.Length != 4 || leadingYear is null)
        {
            issues = [new ImportReviewIssue(ImportIssueCodes.InvalidReleaseDate, "Release date could not be parsed", ImportReviewSeverity.Error)];
        }

        return new ImportDateResult(parsedReleaseDate, parsedYear, issues);
    }

    private static IReadOnlyList<string> CleanNames(IReadOnlyList<string>? values)
    {
        return values is null
            ? []
            :
            [
                .. values
                    .Select(TrimOrNull)
                    .Where(value => value is not null)
                    .Select(value => value!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
            ];
    }

    private static string RequiredFilePath(string sourceRoot, string filePath, string relativePath)
    {
        return string.IsNullOrWhiteSpace(filePath)
            ? Path.Combine(sourceRoot, relativePath)
            : filePath.Trim();
    }

    private static string DirectoryRelativePath(string relativePath)
    {
        string? directory = Path.GetDirectoryName(relativePath.Replace('\\', Path.DirectorySeparatorChar));
        return NormalizeRelativePath(directory ?? string.Empty);
    }

    private static string ParentRelativePath(string relativePath)
    {
        string? parent = Path.GetDirectoryName(relativePath.Replace('\\', Path.DirectorySeparatorChar));
        return NormalizeRelativePath(parent ?? string.Empty);
    }

    private static string NormalizeRelativePath(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().Replace('\\', '/').Trim('/');
    }

    private static string LastSegment(string relativePath)
    {
        string normalized = NormalizeRelativePath(relativePath);
        int index = normalized.LastIndexOf('/');
        return index < 0 ? normalized : normalized[(index + 1)..];
    }

    private static string FirstSegment(string relativePath)
    {
        string normalized = NormalizeRelativePath(relativePath);
        int index = normalized.IndexOf('/');
        return index < 0 ? normalized : normalized[..index];
    }

    private static bool IsHiddenPath(string relativePath)
    {
        return NormalizeRelativePath(relativePath)
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => segment.Length > 0 && segment[0] == '.');
    }

    private static bool IsNamed(string relativePath, string stem)
    {
        return string.Equals(Path.GetFileNameWithoutExtension(relativePath), stem, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return string.Empty;
        }

        string trimmed = extension.Trim().ToLowerInvariant();
        return trimmed.StartsWith('.') ? trimmed : "." + trimmed;
    }

    private static bool IsDiscDirectory(string directoryName)
    {
        return DiscDirectoryRegex().IsMatch(directoryName);
    }

    [GeneratedRegex("^(cd|disc|disk)\\s*\\d+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DiscDirectoryRegex();

    private static string? TrimOrNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? NormalizeContentHash(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
    }

    private sealed record DesktopScanFile(
        DesktopFolderScanFileRequest Request,
        string RelativePath,
        string FilePath,
        AudioFileFormat? AudioFormat);

    private sealed record CoverSelection(
        DesktopScanFile? File,
        CoverArtifactPayload? Artifact,
        IReadOnlyList<ImportReviewIssue> Issues);

    private sealed record ImportDateResult(DateOnly? ReleaseDate, int? Year, IReadOnlyList<ImportReviewIssue> Issues);
}
