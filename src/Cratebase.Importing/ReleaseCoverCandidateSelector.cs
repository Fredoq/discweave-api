using Cratebase.Domain.Imports;

namespace Cratebase.Importing;

public static class ReleaseCoverCandidateSelector
{
    public static CoverCandidateResult Select(DirectoryInfo releaseDirectory)
    {
        FileInfo[] candidates = [.. releaseDirectory
            .EnumerateFiles("*", SearchOption.TopDirectoryOnly)
            .Where(file => ReleaseImportFileRules.IsSupportedCover(file.FullName))];
        if (candidates.Length == 0)
        {
            return new CoverCandidateResult(null, []);
        }

        FileInfo[] priority =
        [
            .. candidates.Where(file => IsNamed(file, "cover")),
            .. candidates.Where(file => IsNamed(file, "folder")),
            .. candidates.Where(file => IsNamed(file, "front")),
            .. candidates.Where(file => file.Name.StartsWith("AlbumArt", StringComparison.OrdinalIgnoreCase) &&
                file.Name.Contains("Large", StringComparison.OrdinalIgnoreCase)),
            .. candidates.OrderByDescending(file => file.Length)
        ];
        FileInfo selected = priority.DistinctBy(file => file.FullName, StringComparer.OrdinalIgnoreCase).First();
        IReadOnlyList<ImportReviewIssue> issues = candidates.Length > 1
            ? [new ImportReviewIssue("import.cover_multiple_candidates", "Multiple cover image candidates were found")]
            : [];

        return new CoverCandidateResult(selected, issues);
    }

    private static bool IsNamed(FileInfo file, string stem)
    {
        return string.Equals(Path.GetFileNameWithoutExtension(file.Name), stem, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record CoverCandidateResult(FileInfo? File, IReadOnlyList<ImportReviewIssue> Issues);
