namespace Cratebase.Seeding;

public sealed class SeedCommand
{
    public SeedCommand(
        string connectionString,
        string email,
        string password,
        LargeCollectionSeedOptions options,
        SeedVerificationOptions? verification = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        ArgumentNullException.ThrowIfNull(options);

        ConnectionString = connectionString;
        Email = email;
        Password = password;
        Options = options;
        Verification = verification ?? SeedVerificationOptions.None;
    }

    public string ConnectionString { get; }

    public string Email { get; }

    public string Password { get; }

    public LargeCollectionSeedOptions Options { get; }

    public SeedVerificationOptions Verification { get; }

    public bool VerifySearch => Verification.VerifySearch;

    public int SearchBudgetMilliseconds => Verification.SearchBudgetMilliseconds;

    public bool VerifyPerformance => Verification.VerifyPerformance;

    public int PerformanceBudgetMilliseconds => Verification.PerformanceBudgetMilliseconds;
}
