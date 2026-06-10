using System.Text.RegularExpressions;

namespace DiscWeave.Api.Features.Imports;

public static partial class ReleaseImportScanService
{
    private static Dictionary<string, DirectoryFacts> BuildDirectoryFacts(IReadOnlyList<DesktopScanFile> audioFiles)
    {
        var childDirectoriesByParent = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (DesktopScanFile file in audioFiles)
        {
            string directory = DirectoryRelativePath(file.RelativePath);
            foreach (DirectoryChild relationship in ParentChildDirectories(directory))
            {
                if (!childDirectoriesByParent.TryGetValue(relationship.Parent, out HashSet<string>? childDirectories))
                {
                    childDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    childDirectoriesByParent.Add(relationship.Parent, childDirectories);
                }

                _ = childDirectories.Add(relationship.Child);
            }
        }

        return childDirectoriesByParent.ToDictionary(
            item => item.Key,
            item => ToDirectoryFacts(item.Value),
            StringComparer.OrdinalIgnoreCase);
    }

    private static DirectoryFacts ToDirectoryFacts(HashSet<string> childDirectories)
    {
        return new DirectoryFacts(
            childDirectories.Count > 0 && childDirectories.All(child => IsDiscDirectory(LastSegment(child))),
            childDirectories.Count > 0 && childDirectories.All(child => IsSideDirectory(LastSegment(child))));
    }

    private static IEnumerable<DirectoryChild> ParentChildDirectories(string directory)
    {
        string normalized = NormalizeRelativePath(directory);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            yield break;
        }

        string parent = string.Empty;
        foreach (string segment in normalized.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            string child = string.IsNullOrWhiteSpace(parent) ? segment : $"{parent}/{segment}";
            yield return new DirectoryChild(parent, child);
            parent = child;
        }
    }

    private static string ReleaseRootFor(string audioRelativePath, Dictionary<string, DirectoryFacts> directoryFacts)
    {
        string audioDirectory = DirectoryRelativePath(audioRelativePath);
        if (string.IsNullOrWhiteSpace(audioDirectory))
        {
            return string.Empty;
        }

        string lastSegment = LastSegment(audioDirectory);
        if (IsSideDirectory(lastSegment))
        {
            string sideParent = ParentRelativePath(audioDirectory);
            if (string.IsNullOrWhiteSpace(sideParent))
            {
                return string.Empty;
            }

            string sideParentLastSegment = LastSegment(sideParent);
            if (IsDiscDirectory(sideParentLastSegment))
            {
                string discParent = ParentRelativePath(sideParent);
                return ParentContainsOnlyDiscAudioDirectories(discParent, directoryFacts)
                    ? discParent
                    : sideParent;
            }

            return ParentContainsOnlySideAudioDirectories(sideParent, directoryFacts)
                ? sideParent
                : audioDirectory;
        }

        string parent = ParentRelativePath(audioDirectory);
        return IsDiscDirectory(lastSegment) && ParentContainsOnlyDiscAudioDirectories(parent, directoryFacts)
            ? parent
            : audioDirectory;
    }

    private static bool ParentContainsOnlyDiscAudioDirectories(string parent, Dictionary<string, DirectoryFacts> directoryFacts)
    {
        return directoryFacts.TryGetValue(parent, out DirectoryFacts? facts) && facts.ContainsOnlyDiscAudioDirectories;
    }

    private static bool ParentContainsOnlySideAudioDirectories(string parent, Dictionary<string, DirectoryFacts> directoryFacts)
    {
        return directoryFacts.TryGetValue(parent, out DirectoryFacts? facts) && facts.ContainsOnlySideAudioDirectories;
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

    private static bool IsHiddenPath(string relativePath)
    {
        return NormalizeRelativePath(relativePath)
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => segment.Length > 0 && segment[0] == '.');
    }

    private static bool IsDiscDirectory(string directoryName)
    {
        return DiscDirectoryRegex().IsMatch(directoryName);
    }

    private static bool IsSideDirectory(string directoryName)
    {
        return SideDirectoryRegex().IsMatch(directoryName);
    }

    private static string? SideFromDirectory(string directoryName)
    {
        Match match = SideDirectoryRegex().Match(directoryName);
        return match.Success ? match.Groups["side"].Value.Trim() : null;
    }

    [GeneratedRegex("^(cd|disc|disk)\\s*\\d+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DiscDirectoryRegex();

    [GeneratedRegex("^side\\s+(?<side>[A-Za-z0-9]+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SideDirectoryRegex();

    private sealed record DirectoryFacts(bool ContainsOnlyDiscAudioDirectories, bool ContainsOnlySideAudioDirectories);

    private readonly record struct DirectoryChild(string Parent, string Child);
}
