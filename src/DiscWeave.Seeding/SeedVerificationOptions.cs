namespace DiscWeave.Seeding;

public sealed class SeedVerificationOptions
{
    public const int DefaultBudgetMilliseconds = 250;

    public static SeedVerificationOptions None { get; } = new();

    public SeedVerificationOptions(
        bool verifySearch = false,
        int searchBudgetMilliseconds = DefaultBudgetMilliseconds,
        bool verifyPerformance = false,
        int performanceBudgetMilliseconds = DefaultBudgetMilliseconds)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(searchBudgetMilliseconds, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(performanceBudgetMilliseconds, 1);

        VerifySearch = verifySearch;
        SearchBudgetMilliseconds = searchBudgetMilliseconds;
        VerifyPerformance = verifyPerformance;
        PerformanceBudgetMilliseconds = performanceBudgetMilliseconds;
    }

    public bool VerifySearch { get; }

    public int SearchBudgetMilliseconds { get; }

    public bool VerifyPerformance { get; }

    public int PerformanceBudgetMilliseconds { get; }
}
