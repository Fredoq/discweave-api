namespace DiscWeave.Api.Tests.Architecture;

public sealed class ApiBoundaryTests
{
    [Theory(DisplayName = "API source files do not depend on database provider exception types")]
    [InlineData("DbUpdateException")]
    [InlineData("Npgsql")]
    [InlineData("PostgresException")]
    public void ApiSourceFilesDoNotDependOnDatabaseProviderExceptionTypes(string forbiddenToken)
    {
        DirectoryInfo apiRoot = new(Path.Combine(RepositoryRoot.Find().FullName, "src", "DiscWeave.Api"));
        FileInfo[] sourceFiles =
        [
            .. apiRoot
                .EnumerateFiles("*.cs", SearchOption.AllDirectories)
                .Where(file => !file.FullName.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                .Where(file => !file.FullName.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        ];
        string[] offenders =
        [
            .. sourceFiles
                .Where(file => File.ReadAllText(file.FullName).Contains(forbiddenToken, StringComparison.Ordinal))
                .Select(apiRoot.ToRelativePath)
                .Order(StringComparer.Ordinal)
        ];

        Assert.Empty(offenders);
    }

    [Theory(DisplayName = "Application source files do not expose database conflict terminology")]
    [InlineData("DbUpdateException")]
    [InlineData("Npgsql")]
    [InlineData("PostgresException")]
    [InlineData("ForeignKeyViolation")]
    [InlineData("ReferentialIntegrityViolation")]
    public void ApplicationSourceFilesDoNotExposeDatabaseConflictTerminology(string forbiddenToken)
    {
        DirectoryInfo applicationRoot = new(Path.Combine(RepositoryRoot.Find().FullName, "src", "DiscWeave.Application"));
        FileInfo[] sourceFiles =
        [
            .. applicationRoot
                .EnumerateFiles("*.cs", SearchOption.AllDirectories)
                .Where(file => !file.FullName.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                .Where(file => !file.FullName.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        ];
        string[] offenders =
        [
            .. sourceFiles
                .Where(file => File.ReadAllText(file.FullName).Contains(forbiddenToken, StringComparison.Ordinal))
                .Select(applicationRoot.ToRelativePath)
                .Order(StringComparer.Ordinal)
        ];

        Assert.Empty(offenders);
    }
}
