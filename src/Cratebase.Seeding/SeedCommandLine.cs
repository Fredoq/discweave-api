using System.Globalization;

namespace Cratebase.Seeding;

public static class SeedCommandLine
{
    private const string DefaultEmail = "seed@cratebase.local";
    private const string DefaultPassword = "SeedPassword1!";

    public static SeedCommand Parse(IReadOnlyList<string> args, Func<string, string?> environment)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(environment);

        string? connectionString = null;
        string email = DefaultEmail;
        string password = DefaultPassword;
        int artistCount = LargeCollectionSeedOptions.DefaultArtistCount;
        int labelCount = LargeCollectionSeedOptions.DefaultLabelCount;
        int releaseCount = LargeCollectionSeedOptions.DefaultReleaseCount;
        int tracksPerRelease = LargeCollectionSeedOptions.DefaultTracksPerRelease;
        bool verifySearch = false;
        int searchBudgetMilliseconds = SeedVerificationOptions.DefaultBudgetMilliseconds;
        bool verifyPerformance = false;
        int performanceBudgetMilliseconds = SeedVerificationOptions.DefaultBudgetMilliseconds;

        for (int index = 0; index < args.Count; index++)
        {
            string argument = args[index];
            switch (argument)
            {
                case "--connection-string":
                    connectionString = RequiredValue(args, ref index, argument);
                    break;
                case "--email":
                    email = RequiredValue(args, ref index, argument);
                    break;
                case "--password":
                    password = RequiredValue(args, ref index, argument);
                    break;
                case "--artists":
                    artistCount = ParseInt(RequiredValue(args, ref index, argument), argument);
                    break;
                case "--labels":
                    labelCount = ParseInt(RequiredValue(args, ref index, argument), argument);
                    break;
                case "--releases":
                    releaseCount = ParseInt(RequiredValue(args, ref index, argument), argument);
                    break;
                case "--tracks-per-release":
                    tracksPerRelease = ParseInt(RequiredValue(args, ref index, argument), argument);
                    break;
                case "--verify-search":
                    verifySearch = true;
                    break;
                case "--search-budget-ms":
                    searchBudgetMilliseconds = ParsePositiveInt(RequiredValue(args, ref index, argument), argument);
                    break;
                case "--verify-performance":
                    verifyPerformance = true;
                    break;
                case "--performance-budget-ms":
                    performanceBudgetMilliseconds = ParsePositiveInt(RequiredValue(args, ref index, argument), argument);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown seed option: {argument}");
            }
        }

        connectionString = FirstText(
            connectionString,
            environment("ConnectionStrings__Cratebase"),
            environment("CRATEBASE_CONNECTION_STRING"));

        return new SeedCommand(
            string.IsNullOrWhiteSpace(connectionString)
                ? throw new InvalidOperationException("Connection string is required. Pass --connection-string or set ConnectionStrings__Cratebase")
                : connectionString,
            email,
            password,
            new LargeCollectionSeedOptions(artistCount, labelCount, releaseCount, tracksPerRelease),
            new SeedVerificationOptions(
                verifySearch,
                searchBudgetMilliseconds,
                verifyPerformance,
                performanceBudgetMilliseconds));
    }

    private static string RequiredValue(IReadOnlyList<string> args, ref int index, string argument)
    {
        int valueIndex = index + 1;
        if (valueIndex >= args.Count || args[valueIndex].StartsWith("--", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Seed option {argument} requires a value");
        }

        index = valueIndex;
        return args[valueIndex];
    }

    private static int ParseInt(string value, string argument)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : throw new InvalidOperationException($"Seed option {argument} must be an integer");
    }

    private static int ParsePositiveInt(string value, string argument)
    {
        int parsed = ParseInt(value, argument);
        return parsed > 0
            ? parsed
            : throw new InvalidOperationException($"Seed option {argument} must be greater than zero");
    }

    private static string? FirstText(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
    }
}
