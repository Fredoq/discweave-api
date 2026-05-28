namespace Cratebase.Seeding;

public sealed class SeedCommand
{
    public SeedCommand(
        string connectionString,
        string email,
        string password,
        LargeCollectionSeedOptions options,
        bool verifySearch = false,
        int searchBudgetMilliseconds = 250,
        bool verifyPerformance = false,
        int performanceBudgetMilliseconds = 250)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfLessThan(searchBudgetMilliseconds, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(performanceBudgetMilliseconds, 1);

        ConnectionString = connectionString;
        Email = email;
        Password = password;
        Options = options;
        VerifySearch = verifySearch;
        SearchBudgetMilliseconds = searchBudgetMilliseconds;
        VerifyPerformance = verifyPerformance;
        PerformanceBudgetMilliseconds = performanceBudgetMilliseconds;
    }

    public string ConnectionString { get; }

    public string Email { get; }

    public string Password { get; }

    public LargeCollectionSeedOptions Options { get; }

    public bool VerifySearch { get; }

    public int SearchBudgetMilliseconds { get; }

    public bool VerifyPerformance { get; }

    public int PerformanceBudgetMilliseconds { get; }
}
