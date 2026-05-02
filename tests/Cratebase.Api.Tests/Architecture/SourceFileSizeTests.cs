namespace Cratebase.Api.Tests.Architecture;

public sealed class SourceFileSizeTests
{
    private const int MaximumManualSourceFileLines = 300;

    [Fact(DisplayName = "Manually maintained C# source files stay under the size limit")]
    public void ManuallyMaintainedCSharpSourceFilesStayUnderTheSizeLimit()
    {
        DirectoryInfo repositoryRoot = FindRepositoryRoot();
        FileInfo[] oversizedFiles =
        [
            .. repositoryRoot
            .EnumerateFiles("*.cs", SearchOption.AllDirectories)
            .Where(IsManualSourceFile)
            .Where(file => File.ReadLines(file.FullName).Count() > MaximumManualSourceFileLines)
            .OrderBy(file => file.FullName, StringComparer.Ordinal)
        ];

        Assert.Empty(oversizedFiles.Select(repositoryRoot.ToRelativePath));
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Cratebase.slnx")))
            {
                return current;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Repository root was not found");
    }

    private static bool IsManualSourceFile(FileInfo file)
    {
        string fullName = file.FullName;

        return !fullName.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
            !fullName.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
            !fullName.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
            !file.Name.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase) &&
            !file.Name.EndsWith(".generated.cs", StringComparison.OrdinalIgnoreCase) &&
            !file.Name.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase);
    }
}
