namespace DiscWeave.Api.Tests.Architecture;

internal static class DirectoryInfoExtensions
{
    public static string ToRelativePath(this DirectoryInfo directory, FileInfo file)
    {
        return Path.GetRelativePath(directory.FullName, file.FullName);
    }
}
