using System.Text.RegularExpressions;

namespace Cratebase.Importing;

public static partial class ReleaseFolderFileScanner
{
    public static ReleaseFolderFileScan Scan(string rootPath)
    {
        var root = new DirectoryInfo(rootPath);
        if (!root.Exists)
        {
            throw new DirectoryNotFoundException(root.FullName);
        }

        FileInfo[] files = [.. root
            .EnumerateFiles("*", SearchOption.AllDirectories)
            .Where(file => !IsHiddenOrSystem(file))];
        FileInfo[] audioFiles = [.. files.Where(file => ReleaseImportFileRules.IsSupportedAudio(file.FullName))];
        int ignoredFileCount = files.Count(file => !ReleaseImportFileRules.IsSupportedAudio(file.FullName) && !ReleaseImportFileRules.IsSupportedCover(file.FullName));

        ReleaseFileGroup[] groups = [.. audioFiles
            .GroupBy(file => ReleaseRootFor(file.Directory ?? root, root).FullName, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var releaseRoot = new DirectoryInfo(group.Key);
                return new ReleaseFileGroup(
                    releaseRoot,
                    [.. group.OrderBy(file => file.FullName, StringComparer.OrdinalIgnoreCase)]);
            })
            .OrderBy(group => group.ReleaseRoot.FullName, StringComparer.OrdinalIgnoreCase)];

        return new ReleaseFolderFileScan(groups, ignoredFileCount);
    }

    private static DirectoryInfo ReleaseRootFor(DirectoryInfo audioDirectory, DirectoryInfo scanRoot)
    {
        DirectoryInfo parent = audioDirectory.Parent ?? audioDirectory;
        return IsDiscDirectory(audioDirectory) &&
            parent.FullName.StartsWith(scanRoot.FullName, StringComparison.OrdinalIgnoreCase) &&
            ParentContainsOnlyDiscAudioDirectories(parent)
            ? parent
            : audioDirectory;
    }

    private static bool ParentContainsOnlyDiscAudioDirectories(DirectoryInfo parent)
    {
        DirectoryInfo[] audioChildDirectories = [.. parent
            .EnumerateDirectories()
            .Where(directory => !IsHiddenOrSystem(directory))
            .Where(directory => directory.EnumerateFiles("*", SearchOption.AllDirectories).Any(file => ReleaseImportFileRules.IsSupportedAudio(file.FullName)))];

        return audioChildDirectories.Length > 0 && audioChildDirectories.All(IsDiscDirectory);
    }

    private static bool IsHiddenOrSystem(FileSystemInfo info)
    {
        return info.Name.StartsWith('.') ||
            info.Attributes.HasFlag(FileAttributes.Hidden) ||
            info.Attributes.HasFlag(FileAttributes.System);
    }

    private static bool IsDiscDirectory(DirectoryInfo directory)
    {
        return DiscDirectoryRegex().IsMatch(directory.Name);
    }

    [GeneratedRegex("^(cd|disc|disk)\\s*\\d+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DiscDirectoryRegex();
}

public sealed record ReleaseFolderFileScan(IReadOnlyList<ReleaseFileGroup> Groups, int IgnoredFileCount);

public sealed record ReleaseFileGroup(DirectoryInfo ReleaseRoot, IReadOnlyList<FileInfo> AudioFiles);
