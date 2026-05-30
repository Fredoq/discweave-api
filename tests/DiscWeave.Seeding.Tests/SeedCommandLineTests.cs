namespace DiscWeave.Seeding.Tests;

public sealed class SeedCommandLineTests
{
    [Fact(DisplayName = "Seed command line parses explicit connection and scale options")]
    public void SeedCommandLineParsesExplicitConnectionAndScaleOptions()
    {
        string[] args =
        [
            "--connection-string", "Host=localhost;Database=discweave",
            "--email", "load-test@example.test",
            "--password", "SeedPassword1!",
            "--artists", "24",
            "--labels", "5",
            "--releases", "20",
            "--tracks-per-release", "6"
        ];

        SeedCommand command = SeedCommandLine.Parse(args, static _ => null);

        Assert.Equal("Host=localhost;Database=discweave", command.ConnectionString);
        Assert.Equal("load-test@example.test", command.Email);
        Assert.Equal("SeedPassword1!", command.Password);
        Assert.Equal(24, command.Options.ArtistCount);
        Assert.Equal(5, command.Options.LabelCount);
        Assert.Equal(20, command.Options.ReleaseCount);
        Assert.Equal(6, command.Options.TracksPerRelease);
    }

    [Fact(DisplayName = "Seed command line parses search verification options")]
    public void SeedCommandLineParsesSearchVerificationOptions()
    {
        string[] args =
        [
            "--connection-string", "Host=localhost;Database=discweave",
            "--verify-search",
            "--search-budget-ms", "500"
        ];

        SeedCommand command = SeedCommandLine.Parse(args, static _ => null);

        Assert.True(command.VerifySearch);
        Assert.Equal(500, command.SearchBudgetMilliseconds);
    }

    [Fact(DisplayName = "Seed command line parses performance verification options")]
    public void SeedCommandLineParsesPerformanceVerificationOptions()
    {
        string[] args =
        [
            "--connection-string", "Host=localhost;Database=discweave",
            "--verify-performance",
            "--performance-budget-ms", "750"
        ];

        SeedCommand command = SeedCommandLine.Parse(args, static _ => null);

        Assert.True(command.VerifyPerformance);
        Assert.Equal(750, command.PerformanceBudgetMilliseconds);
    }

    [Fact(DisplayName = "Seed command line uses the default search verification budget")]
    public void SeedCommandLineUsesTheDefaultSearchVerificationBudget()
    {
        SeedCommand command = SeedCommandLine.Parse(
            ["--connection-string", "Host=localhost;Database=discweave", "--verify-search"],
            static _ => null);

        Assert.True(command.VerifySearch);
        Assert.Equal(250, command.SearchBudgetMilliseconds);
    }

    [Fact(DisplayName = "Seed command line uses the default performance verification budget")]
    public void SeedCommandLineUsesTheDefaultPerformanceVerificationBudget()
    {
        SeedCommand command = SeedCommandLine.Parse(
            ["--connection-string", "Host=localhost;Database=discweave", "--verify-performance"],
            static _ => null);

        Assert.True(command.VerifyPerformance);
        Assert.Equal(250, command.PerformanceBudgetMilliseconds);
    }

    [Theory(DisplayName = "Seed command line uses connection string from environment")]
    [InlineData("ConnectionStrings__DiscWeave", "Host=env;Database=discweave")]
    [InlineData("DISCWEAVE_CONNECTION_STRING", "Host=legacy;Database=discweave")]
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

    [Theory(DisplayName = "Seed command line rejects invalid search verification budgets")]
    [InlineData("soon")]
    [InlineData("0")]
    public void SeedCommandLineRejectsInvalidSearchVerificationBudgets(string budget)
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            SeedCommandLine.Parse(
                ["--connection-string", "Host=localhost", "--verify-search", "--search-budget-ms", budget],
                static _ => null));

        Assert.Contains("--search-budget-ms", exception.Message, StringComparison.Ordinal);
    }

    [Theory(DisplayName = "Seed command line rejects invalid performance verification budgets")]
    [InlineData("soon")]
    [InlineData("0")]
    public void SeedCommandLineRejectsInvalidPerformanceVerificationBudgets(string budget)
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            SeedCommandLine.Parse(
                ["--connection-string", "Host=localhost", "--verify-performance", "--performance-budget-ms", budget],
                static _ => null));

        Assert.Contains("--performance-budget-ms", exception.Message, StringComparison.Ordinal);
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
