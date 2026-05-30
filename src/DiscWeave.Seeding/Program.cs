using DiscWeave.Infrastructure;
using DiscWeave.Infrastructure.Identity;
using DiscWeave.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DiscWeave.Seeding;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Contains("--help", StringComparer.Ordinal))
        {
            WriteUsage();
            return 0;
        }

        SeedCommand command = SeedCommandLine.Parse(args, Environment.GetEnvironmentVariable);
        using ServiceProvider provider = CreateProvider(command.ConnectionString);
        using IServiceScope scope = provider.CreateScope();
        DiscWeaveDbContext context = scope.ServiceProvider.GetRequiredService<DiscWeaveDbContext>();
        await context.Database.MigrateAsync();

        LargeCollectionSeedResult result = await LargeCollectionDatabaseSeeder.SeedAsync(
            context,
            scope.ServiceProvider.GetRequiredService<UserManager<DiscWeaveUser>>(),
            scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>(),
            command,
            CancellationToken.None);

        WriteResult(result);
        if (command.VerifySearch)
        {
            await SearchSmokeVerifier.VerifyAsync(
                context,
                result.CollectionId,
                TimeSpan.FromMilliseconds(command.SearchBudgetMilliseconds),
                Console.Out,
                CancellationToken.None);
        }

        if (command.VerifyPerformance)
        {
            await PerformanceSmokeVerifier.VerifyAsync(
                context,
                result.CollectionId,
                TimeSpan.FromMilliseconds(command.PerformanceBudgetMilliseconds),
                Console.Out,
                CancellationToken.None);
        }

        return 0;
    }

    private static ServiceProvider CreateProvider(string connectionString)
    {
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DiscWeave"] = connectionString
        };
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        var services = new ServiceCollection();
        _ = services.AddDataProtection();
        _ = services.AddLogging();
        _ = services.AddDiscWeaveInfrastructure(configuration);

        return services.BuildServiceProvider();
    }

    private static void WriteResult(LargeCollectionSeedResult result)
    {
        if (!result.WasSeeded || result.Data is null)
        {
            Console.WriteLine($"Seed collection already has catalog data: {result.Email} ({result.CollectionId})");
            return;
        }

        LargeCollectionSeedData data = result.Data;
        Console.WriteLine($"Seeded collection: {result.Email} ({result.CollectionId})");
        Console.WriteLine($"Artists: {data.Artists.Count}");
        Console.WriteLine($"Labels: {data.Labels.Count}");
        Console.WriteLine($"Releases: {data.Releases.Count}");
        Console.WriteLine($"Tracks: {data.Tracks.Count}");
        Console.WriteLine($"Owned items: {data.OwnedItems.Count}");
        Console.WriteLine($"Credits: {data.Credits.Count}");
        Console.WriteLine($"Artist relations: {data.ArtistRelations.Count}");
        Console.WriteLine($"Track relations: {data.TrackRelations.Count}");
        Console.WriteLine($"Playlists: {data.Playlists.Count}");
    }

    private static void WriteUsage()
    {
        Console.WriteLine("Usage: dotnet run --project src/DiscWeave.Seeding -- --connection-string <postgres> [options]");
        Console.WriteLine("Options:");
        Console.WriteLine("  --email <email>                 Seed user email. Default: seed@discweave.local");
        Console.WriteLine("  --password <password>           Seed user password. Default: SeedPassword1!");
        Console.WriteLine("  --artists <count>               Artist count. Default: 1200");
        Console.WriteLine("  --labels <count>                Label count. Default: 120");
        Console.WriteLine("  --releases <count>              Release count. Default: 1500");
        Console.WriteLine("  --tracks-per-release <count>    Tracks per release. Default: 8");
        Console.WriteLine("  --verify-search                 Run search v1 smoke probes after seeding.");
        Console.WriteLine("  --search-budget-ms <ms>         Search smoke warning budget. Default: 250");
        Console.WriteLine("  --verify-performance            Run large-collection performance smoke probes after seeding.");
        Console.WriteLine("  --performance-budget-ms <ms>    Performance smoke warning budget per probe. Default: 250");
    }
}
