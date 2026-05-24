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
}
