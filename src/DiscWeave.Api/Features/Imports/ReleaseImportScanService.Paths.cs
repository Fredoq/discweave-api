using System.Text.RegularExpressions;

namespace DiscWeave.Api.Features.Imports;

public static partial class ReleaseImportScanService
{
    private static string ReleaseRootFor(string audioRelativePath, IReadOnlyList<DesktopScanFile> audioFiles)
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
                return ParentContainsOnlyDiscAudioDirectories(discParent, audioFiles)
                    ? discParent
                    : sideParent;
            }

            return ParentContainsOnlySideAudioDirectories(sideParent, audioFiles)
                ? sideParent
                : audioDirectory;
        }

        string parent = ParentRelativePath(audioDirectory);
        return IsDiscDirectory(lastSegment) && ParentContainsOnlyDiscAudioDirectories(parent, audioFiles)
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

    private static bool ParentContainsOnlySideAudioDirectories(string parent, IReadOnlyList<DesktopScanFile> audioFiles)
    {
        string[] childDirectories =
        [
            .. audioFiles
                .Select(file => ImmediateChildDirectory(parent, DirectoryRelativePath(file.RelativePath)))
                .Where(child => !string.IsNullOrWhiteSpace(child))
                .Distinct(StringComparer.OrdinalIgnoreCase)
        ];

        return childDirectories.Length > 0 && childDirectories.All(child => IsSideDirectory(LastSegment(child)));
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
}
