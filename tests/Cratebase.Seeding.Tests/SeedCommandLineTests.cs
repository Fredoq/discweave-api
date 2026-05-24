namespace Cratebase.Seeding.Tests;

public sealed class SeedCommandLineTests
{
    [Fact(DisplayName = "Seed command line parses explicit connection and scale options")]
    public void SeedCommandLineParsesExplicitConnectionAndScaleOptions()
    {
        string[] args =
        [
            "--connection-string", "Host=localhost;Database=cratebase",
            "--email", "load-test@example.test",
            "--password", "SeedPassword1!",
            "--artists", "24",
            "--labels", "5",
            "--releases", "20",
            "--tracks-per-release", "6"
        ];

        SeedCommand command = SeedCommandLine.Parse(args, static _ => null);

        Assert.Equal("Host=localhost;Database=cratebase", command.ConnectionString);
        Assert.Equal("load-test@example.test", command.Email);
        Assert.Equal("SeedPassword1!", command.Password);
        Assert.Equal(24, command.Options.ArtistCount);
        Assert.Equal(5, command.Options.LabelCount);
        Assert.Equal(20, command.Options.ReleaseCount);
        Assert.Equal(6, command.Options.TracksPerRelease);
    }

    [Theory(DisplayName = "Seed command line uses connection string from environment")]
    [InlineData("ConnectionStrings__Cratebase", "Host=env;Database=cratebase")]
    [InlineData("CRATEBASE_CONNECTION_STRING", "Host=legacy;Database=cratebase")]
    public void SeedCommandLineUsesConnectionStringFromEnvironment(string variableName, string connectionString)
    {
        SeedCommand command = SeedCommandLine.Parse(
            [],
            key => key == variableName ? $" {connectionString} " : null);

        Assert.Equal(connectionString, command.ConnectionString);
    }

    [Fact(DisplayName = "Seed command line rejects missing connection string")]
    public void SeedCommandLineRejectsMissingConnectionString()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            SeedCommandLine.Parse([], static _ => null));

        Assert.Contains("Connection string is required", exception.Message, StringComparison.Ordinal);
    }

    [Theory(DisplayName = "Seed command line rejects non-numeric scale options")]
    [InlineData("--artists")]
    [InlineData("--labels")]
    [InlineData("--releases")]
    [InlineData("--tracks-per-release")]
    public void SeedCommandLineRejectsNonNumericScaleOptions(string optionName)
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            SeedCommandLine.Parse(["--connection-string", "Host=localhost", optionName, "many"], static _ => null));

        Assert.Contains($"{optionName} must be an integer", exception.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "Seed command line rejects unknown options")]
    public void SeedCommandLineRejectsUnknownOptions()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            SeedCommandLine.Parse(["--connection-string", "Host=localhost", "--unknown"], static _ => null));

        Assert.Contains("Unknown seed option: --unknown", exception.Message, StringComparison.Ordinal);
    }

    [Theory(DisplayName = "Seed command line rejects missing option values")]
    [InlineData("--connection-string")]
    [InlineData("--email")]
    [InlineData("--password")]
    [InlineData("--artists")]
    public void SeedCommandLineRejectsMissingOptionValues(string optionName)
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            SeedCommandLine.Parse([optionName], static _ => null));

        Assert.Contains($"Seed option {optionName} requires a value", exception.Message, StringComparison.Ordinal);
    }
}
