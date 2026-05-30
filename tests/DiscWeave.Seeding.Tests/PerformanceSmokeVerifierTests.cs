using System.Globalization;
using DiscWeave.Domain.Collection;
using DiscWeave.Domain.Ratings;
using DiscWeave.Domain.Settings;
using DiscWeave.Domain.SharedKernel.Ids;
using DiscWeave.Infrastructure.Identity;
using DiscWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DiscWeave.Seeding.Tests;

public sealed class PerformanceSmokeVerifierTests : IClassFixture<PostgresFixture>
{
    private static readonly string[] ExpectedProbeNames =
    [
        "release list",
        "search",
        "relations",
        "playlists",
        "import deduplication",
        "export read"
    ];

    private readonly PostgresFixture _postgres;

    public PerformanceSmokeVerifierTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact(DisplayName = "Performance smoke verifier reports every large collection probe")]
    public async Task PerformanceSmokeVerifierReportsEveryLargeCollectionProbe()
    {
        await using DiscWeaveDbContext context = await CreateMigratedContextAsync();
        var collectionId = CollectionId.New();
        await AddCollectionAsync(context, collectionId);
        await AddSeedDataAsync(context, collectionId);

        using var passOutput = new StringWriter(CultureInfo.InvariantCulture);
        await PerformanceSmokeVerifier.VerifyAsync(
            context,
            collectionId,
            TimeSpan.FromDays(1),
            passOutput,
            CancellationToken.None);

        AssertProbeOutput(passOutput.ToString(), "PASS");

        using var warnOutput = new StringWriter(CultureInfo.InvariantCulture);
        await PerformanceSmokeVerifier.VerifyAsync(
            context,
            collectionId,
            TimeSpan.FromTicks(-1),
            warnOutput,
            CancellationToken.None);

        AssertProbeOutput(warnOutput.ToString(), "WARN");
    }

    [Fact(DisplayName = "Performance smoke verifier fails when a required probe has no data")]
    public async Task PerformanceSmokeVerifierFailsWhenARequiredProbeHasNoData()
    {
        await using DiscWeaveDbContext context = await CreateMigratedContextAsync();
        var collectionId = CollectionId.New();
        await AddCollectionAsync(context, collectionId);
        using var output = new StringWriter(CultureInfo.InvariantCulture);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            PerformanceSmokeVerifier.VerifyAsync(
                context,
                collectionId,
                TimeSpan.FromDays(1),
                output,
                CancellationToken.None));

        Assert.Contains("release list", exception.Message, StringComparison.Ordinal);
        Assert.Contains("FAIL performance smoke release list returned no results", output.ToString(), StringComparison.Ordinal);
    }

    private static void AssertProbeOutput(string output, string expectedStatus)
    {
        string[] lines = output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(ExpectedProbeNames.Length, lines.Length);
        Assert.All(ExpectedProbeNames, name =>
            Assert.Contains($"{expectedStatus} performance smoke {name} ", output, StringComparison.Ordinal));
    }

    private static async Task AddCollectionAsync(DiscWeaveDbContext context, CollectionId collectionId)
    {
        var ownerUserId = UserId.New();
        string email = $"{ownerUserId.Value:N}@example.com";
        var user = new DiscWeaveUser
        {
            Id = ownerUserId.Value,
            Email = email,
            UserName = email
        };

        _ = context.Users.Add(user);
        _ = await context.SaveChangesAsync();

        _ = context.MusicCollections.Add(MusicCollection.Create(collectionId, ownerUserId, "Performance smoke collection"));
        context.CollectionDictionaryEntries.AddRange(CollectionDictionaryDefaults.CreateEntries(collectionId));
        context.RatingCriteria.AddRange(RatingCriterionDefaults.CreateCriteria(collectionId));
        _ = await context.SaveChangesAsync();

        user.DefaultCollectionId = collectionId;
        _ = await context.SaveChangesAsync();
    }

    private static async Task AddSeedDataAsync(DiscWeaveDbContext context, CollectionId collectionId)
    {
        LargeCollectionSeedData data = LargeCollectionSeedGenerator.Generate(
            collectionId,
            new LargeCollectionSeedOptions(24, 4, 12, 2));

        context.Artists.AddRange(data.Artists);
        context.Labels.AddRange(data.Labels);
        context.Tracks.AddRange(data.Tracks);
        context.Releases.AddRange(data.Releases);
        context.OwnedItems.AddRange(data.OwnedItems);
        context.Credits.AddRange(data.Credits);
        context.ArtistRelations.AddRange(data.ArtistRelations);
        context.TrackRelations.AddRange(data.TrackRelations);
        context.Playlists.AddRange(data.Playlists);

        _ = await context.SaveChangesAsync();
    }

    private async Task<DiscWeaveDbContext> CreateMigratedContextAsync()
    {
        string connectionString = await _postgres.CreateDatabaseAsync();
        DiscWeaveDbContext context = new(CreateOptions(connectionString));
        await context.Database.MigrateAsync();

        return context;
    }

    private static DbContextOptions<DiscWeaveDbContext> CreateOptions(string connectionString)
    {
        return new DbContextOptionsBuilder<DiscWeaveDbContext>()
            .UseNpgsql(connectionString)
            .Options;
    }
}
