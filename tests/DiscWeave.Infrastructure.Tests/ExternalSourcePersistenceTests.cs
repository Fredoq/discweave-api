using DiscWeave.Domain.Catalog;
using DiscWeave.Domain.SharedKernel.Ids;
using DiscWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DiscWeave.Infrastructure.Tests;

public sealed class ExternalSourcePersistenceTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact(DisplayName = "The migration creates external source provenance tables")]
    public async Task The_migration_creates_external_source_provenance_tables()
    {
        await using DiscWeaveDbContext context = await CreateMigratedContextAsync();

        string[] tableNames = [.. await ReadTableNamesAsync(context)];
        string[] releaseExternalSourceColumns = [.. await ReadColumnNamesAsync(context, "release_external_sources")];

        Assert.Contains("artist_external_sources", tableNames);
        Assert.Contains("release_external_sources", tableNames);
        Assert.Contains("track_external_sources", tableNames);
        Assert.Contains("provider_name", releaseExternalSourceColumns);
        Assert.Contains("resource_type", releaseExternalSourceColumns);
        Assert.Contains("external_id", releaseExternalSourceColumns);
        Assert.Contains("source_url", releaseExternalSourceColumns);
        Assert.Contains("applied_at", releaseExternalSourceColumns);
        Assert.Contains("collection_id", releaseExternalSourceColumns);
        Assert.Contains("release_id", releaseExternalSourceColumns);
    }

    [Fact(DisplayName = "External source references persist with catalog records")]
    public async Task External_source_references_persist_with_catalog_records()
    {
        await using DiscWeaveDbContext context = await CreateMigratedContextAsync();
        var collectionId = CollectionId.New();
        await TestCollectionFactory.AddCollectionAsync(context, collectionId);
        var artist = Group.Create(collectionId, ArtistId.New(), "New Order");
        var release = Release.Create(collectionId, ReleaseId.New(), "Blue Monday");
        var track = Track.Create(collectionId, TrackId.New(), "Blue Monday");
        artist.ReplaceExternalSources([ExternalSource("artist", "5876")]);
        release.ReplaceExternalSources([ExternalSource("release", "249504")]);
        track.ReplaceExternalSources([ExternalSource("track", "249504-A")]);

        _ = context.Artists.Add(artist);
        _ = context.Releases.Add(release);
        _ = context.Tracks.Add(track);
        _ = await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        Artist actualArtist = await context.Artists.SingleAsync(entity => entity.Id == artist.Id);
        Release actualRelease = await context.Releases.SingleAsync(entity => entity.Id == release.Id);
        Track actualTrack = await context.Tracks.SingleAsync(entity => entity.Id == track.Id);

        Assert.Equal("5876", Assert.Single(actualArtist.ExternalSources).ExternalId);
        Assert.Equal("249504", Assert.Single(actualRelease.ExternalSources).ExternalId);
        Assert.Equal("249504-A", Assert.Single(actualTrack.ExternalSources).ExternalId);
    }

    private static ExternalSourceReference ExternalSource(string resourceType, string externalId)
    {
        return ExternalSourceReference.Create(
            "discogs",
            resourceType,
            externalId,
            $"https://www.discogs.com/{resourceType}/{externalId}",
            new DateTimeOffset(2026, 5, 31, 12, 0, 0, TimeSpan.Zero));
    }

    private static async Task<IReadOnlyList<string>> ReadColumnNamesAsync(DiscWeaveDbContext context, string tableName)
    {
        FormattableString sql = $"""
            SELECT column_name
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name = {tableName}
            ORDER BY ordinal_position
            """;

        return await context.Database.SqlQuery<string>(sql).ToArrayAsync();
    }

    private static async Task<IReadOnlyList<string>> ReadTableNamesAsync(DiscWeaveDbContext context)
    {
        FormattableString sql = $"""
            SELECT table_name
            FROM information_schema.tables
            WHERE table_schema = 'public'
              AND table_type = 'BASE TABLE'
            ORDER BY table_name
            """;

        return await context.Database.SqlQuery<string>(sql).ToArrayAsync();
    }

    private async Task<DiscWeaveDbContext> CreateMigratedContextAsync()
    {
        string connectionString = await postgres.CreateDatabaseAsync();
        var context = new DiscWeaveDbContext(CreateOptions(connectionString));
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
