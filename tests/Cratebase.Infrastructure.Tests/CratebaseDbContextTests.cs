using Cratebase.Application.Persistence;
using Cratebase.Domain.Catalog;
using Cratebase.Domain.Collection;
using Cratebase.Domain.Credits;
using Cratebase.Domain.Relations;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Domain.SharedKernel.Optional;
using Cratebase.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cratebase.Infrastructure.Tests;

public sealed class CratebaseDbContextTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _postgres;

    public CratebaseDbContextTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact(DisplayName = "The initial migration creates the Postgres schema")]
    public async Task The_initial_migration_creates_the_postgres_schema()
    {
        await using CratebaseDbContext context = await CreateMigratedContextAsync();

        string[] migrations = [.. await context.Database.GetAppliedMigrationsAsync()];
        string[] artistColumns = [.. await ReadColumnNamesAsync(context, "artists")];
        string[] artistRelationColumns = [.. await ReadColumnNamesAsync(context, "artist_relations")];
        string[] releaseColumns = [.. await ReadColumnNamesAsync(context, "releases")];
        string[] trackColumns = [.. await ReadColumnNamesAsync(context, "tracks")];
        string[] ownedItemColumns = [.. await ReadColumnNamesAsync(context, "owned_items")];
        string[] creditColumns = [.. await ReadColumnNamesAsync(context, "credits")];
        string[] ratingCriterionColumns = [.. await ReadColumnNamesAsync(context, "rating_criteria")];
        string[] ratingValueColumns = [.. await ReadColumnNamesAsync(context, "rating_values")];
        string[] tableNames = [.. await ReadTableNamesAsync(context)];
        string[] jsonbColumns = [.. await ReadJsonbColumnNamesAsync(context)];

        Assert.Contains(migrations, migration => migration.EndsWith("_Initial", StringComparison.Ordinal));
        Assert.Empty(jsonbColumns);
        Assert.Contains("id", artistColumns);
        Assert.Contains("artist_id", artistColumns);
        Assert.DoesNotContain("database_id", artistColumns);
        Assert.DoesNotContain("public_id", artistColumns);
        Assert.Contains("title", releaseColumns);
        Assert.Contains("release_year", releaseColumns);
        Assert.Contains("cover_image_metadata", releaseColumns);
        Assert.DoesNotContain("cover_image_path", releaseColumns);
        Assert.DoesNotContain("rating", releaseColumns);
        Assert.Contains("duration_ticks", trackColumns);
        Assert.DoesNotContain("rating", trackColumns);
        Assert.Contains("medium_type", ownedItemColumns);
        Assert.Contains("ownership_status", ownedItemColumns);
        Assert.Contains("contributor_artist_id", creditColumns);
        Assert.Contains("target_release_id", creditColumns);
        Assert.Contains("rating_criterion_id", ratingCriterionColumns);
        Assert.Contains("code", ratingCriterionColumns);
        Assert.Contains("rating_value_id", ratingValueColumns);
        Assert.Contains("criterion_id", ratingValueColumns);
        Assert.Contains("target_track_id", ratingValueColumns);
        Assert.Contains("source_artist_id", artistRelationColumns);
        Assert.Contains("target_artist_id", artistRelationColumns);
        Assert.DoesNotContain("source_artist_fk", artistRelationColumns);
        Assert.DoesNotContain("target_artist_fk", artistRelationColumns);
        Assert.Contains("release_tracks", tableNames);
        Assert.Contains("release_genres", tableNames);
        Assert.Contains("release_tags", tableNames);
        Assert.Contains("track_genres", tableNames);
        Assert.Contains("track_tags", tableNames);
        Assert.Contains("rating_criteria", tableNames);
        Assert.Contains("rating_criterion_targets", tableNames);
        Assert.Contains("rating_values", tableNames);
    }

    [Fact(DisplayName = "The context persists catalog aggregates")]
    public async Task The_context_persists_catalog_aggregates()
    {
        await using CratebaseDbContext context = await CreateMigratedContextAsync();
        var collectionId = CollectionId.New();
        await TestCollectionFactory.AddCollectionAsync(context, collectionId);
        var labelId = LabelId.New();
        Track track = Track.Create(collectionId, TrackId.New(), "Age of Consent")
            .WithDuration(TimeSpan.FromSeconds(316))
            .WithCataloging(Cataloging.Empty.WithGenre(Genre.FromName("Post-punk")).WithTag(Tag.FromName("opener")));
        Release release = Release.Create(collectionId, ReleaseId.New(), "Power, Corruption & Lies")
            .WithSummary(
                ReleaseSummary.Create("Power, Corruption & Lies")
                    .WithMetadata(
                        ReleaseMetadata.Empty
                            .WithType(ReleaseType.Album)
                            .WithLabel(labelId)
                            .WithReleaseYear(1983)
                            .WithReleaseDate(new DateOnly(1983, 5, 2))
                            .WithCoverImage(CoverImage.FromLocalUpload(
                                "collections/releases/new-order-power-corruption-lies.webp",
                                "image/webp",
                                "Power Corruption Lies.webp",
                                4_096))))
            .WithTrack(ReleaseTrack.Create(track.Id, TrackPosition.FromNumber(1, "1", "A"), "Age of Consent"))
            .WithCataloging(Cataloging.Empty.WithGenre(Genre.FromName("Post-punk")).WithTag(Tag.FromName("factory")));

        _ = context.Artists.Add(Person.Create(collectionId, ArtistId.New(), "Bernard Sumner"));
        _ = context.Artists.Add(Group.Create(collectionId, ArtistId.New(), "New Order"));
        _ = context.Labels.Add(Label.Create(collectionId, labelId, "Factory"));
        _ = context.Tracks.Add(track);
        _ = context.Releases.Add(release);
        _ = await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        Release actualRelease = await context.Releases.SingleAsync(entity => entity.Id == release.Id);
        Track actualTrack = await context.Tracks.SingleAsync(entity => entity.Id == track.Id);
        Artist[] artists = [.. await context.Artists.OrderBy(artist => artist.Name).ToArrayAsync()];

        _ = Assert.IsType<Person>(artists[0]);
        _ = Assert.IsType<Group>(artists[1]);
        Assert.Equal("Power, Corruption & Lies", actualRelease.Summary.Title);
        Assert.Equal("album", actualRelease.Summary.Metadata.Type);
        Assert.Equal(labelId, Assert.IsType<PresentOptionalValue<LabelId>>(actualRelease.Summary.Metadata.LabelId).Value);
        Assert.Equal(1983, Assert.IsType<PresentOptionalValue<int>>(actualRelease.Summary.Metadata.Year).Value);
        Assert.Equal(new DateOnly(1983, 5, 2), Assert.IsType<PresentOptionalValue<DateOnly>>(actualRelease.Summary.Metadata.ReleaseDate).Value);
        Assert.Equal(
            CoverImage.FromLocalUpload("collections/releases/new-order-power-corruption-lies.webp", "image/webp", "Power Corruption Lies.webp", 4_096),
            Assert.IsType<PresentOptionalValue<CoverImage>>(actualRelease.Summary.Metadata.CoverImage).Value);
        _ = Assert.Single(actualRelease.Tracklist);
        Assert.Contains(actualRelease.Cataloging.Genres, genre => genre.Name == "Post-punk");
        Assert.Contains(actualRelease.Cataloging.Tags, tag => tag.Name == "factory");
        Assert.Contains(actualTrack.Cataloging.Genres, genre => genre.Name == "Post-punk");
        Assert.Contains(actualTrack.Cataloging.Tags, tag => tag.Name == "opener");
    }

    [Fact(DisplayName = "The context persists collection, credits, and relations")]
    public async Task The_context_persists_collection_credits_and_relations()
    {
        await using CratebaseDbContext context = await CreateMigratedContextAsync();
        var collectionId = CollectionId.New();
        await TestCollectionFactory.AddCollectionAsync(context, collectionId);
        Artist artist = Person.Create(collectionId, ArtistId.New(), "Arthur Baker");
        Artist alias = Person.Create(collectionId, ArtistId.New(), "Arthur Baker Alias");
        var sourceTrack = Track.Create(collectionId, TrackId.New(), "Confusion Instrumental");
        var targetTrack = Track.Create(collectionId, TrackId.New(), "Confusion");
        var releaseId = ReleaseId.New();
        var release = Release.Create(collectionId, releaseId, "Confusion");
        OwnedItem releaseItem = OwnedItem.Create(collectionId,
                OwnedItemId.New(),
                OwnedItemTarget.ForRelease(releaseId),
                OwnershipStatus.NeedsDigitization,
                VinylRecord.Create("12-inch"))
            .WithCondition(ItemCondition.VeryGoodPlus)
            .WithStorageLocation(StorageLocation.FromName("Shelf A"));
        var digitalItem = OwnedItem.Create(collectionId,
            OwnedItemId.New(),
            OwnedItemTarget.ForTrack(targetTrack.Id),
            OwnershipStatus.Owned,
            DigitalFile.Create(
                FilePath.FromAbsolutePath("/music/New Order/Confusion.flac"),
                AudioFileFormat.Flac,
                FileImportIdentity.Create(
                    FilePath.FromAbsolutePath("/music/New Order/Confusion.flac"),
                    123_456,
                    DateTimeOffset.UnixEpoch,
                    "abcdef")));

        _ = context.Artists.Add(artist);
        _ = context.Artists.Add(alias);
        _ = context.Tracks.Add(sourceTrack);
        _ = context.Tracks.Add(targetTrack);
        _ = context.Releases.Add(release);
        _ = await context.SaveChangesAsync();
        _ = context.OwnedItems.Add(releaseItem);
        _ = context.OwnedItems.Add(digitalItem);
        _ = context.OwnedItems.Add(OwnedItem.Create(collectionId, OwnedItemId.New(), OwnedItemTarget.ForRelease(releaseId), OwnershipStatus.Owned, CompactDisc.Create(1)));
        _ = context.OwnedItems.Add(OwnedItem.Create(collectionId, OwnedItemId.New(), OwnedItemTarget.ForRelease(releaseId), OwnershipStatus.Wanted, CassetteTape.Create("Chrome")));
        _ = context.OwnedItems.Add(OwnedItem.Create(collectionId, OwnedItemId.New(), OwnedItemTarget.ForRelease(releaseId), OwnershipStatus.Sold, OtherMedium.Create("DAT")));
        _ = context.Credits.Add(Credit.Create(collectionId, CreditId.New(), CreditContributor.FromArtist(artist), CreditTarget.ForRelease(releaseId), CreditRole.Producer));
        _ = context.Credits.Add(Credit.Create(collectionId, CreditId.New(), CreditContributor.FromArtist(artist), CreditTarget.ForTrack(targetTrack.Id), CreditRole.Remixer));
        _ = context.ArtistRelations.Add(ArtistRelation.Create(ArtistRelationId.New(), collectionId, alias.Id, artist.Id, ArtistRelationType.Alias, ArtistRelationPeriod.StartingAt(1983)));
        _ = context.TrackRelations.Add(TrackRelation.Create(TrackRelationId.New(), collectionId, sourceTrack.Id, targetTrack.Id, TrackRelationType.RemixOf));
        _ = await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        OwnedItem actualDigitalItem = await context.OwnedItems.SingleAsync(entity => entity.Id == digitalItem.Id);
        Credit[] credits = [.. await context.Credits.OrderBy(credit => credit.Role).ToArrayAsync()];
        ArtistRelation artistRelation = await context.ArtistRelations.SingleAsync();

        _ = Assert.IsType<TrackOwnedItemTarget>(actualDigitalItem.Target);
        _ = Assert.IsType<DigitalFile>(actualDigitalItem.Holding.Medium);
        Assert.Contains(await context.OwnedItems.ToArrayAsync(), item => item.Holding.Medium is VinylRecord);
        Assert.Contains(await context.OwnedItems.ToArrayAsync(), item => item.Holding.Medium is CompactDisc);
        Assert.Contains(await context.OwnedItems.ToArrayAsync(), item => item.Holding.Medium is CassetteTape);
        Assert.Contains(await context.OwnedItems.ToArrayAsync(), item => item.Holding.Medium is OtherMedium);
        _ = Assert.IsType<ReleaseCreditTarget>(credits[0].Target);
        _ = Assert.IsType<TrackCreditTarget>(credits[1].Target);
        Assert.Equal(1983, Assert.IsType<PresentOptionalValue<ArtistRelationPeriod>>(artistRelation.Period).Value.StartYear.Match(year => year, () => 0));
        Assert.Equal(targetTrack.Id, (await context.TrackRelations.SingleAsync()).TargetTrackId);
    }

    [Fact(DisplayName = "Dependency injection resolves one scoped context for the unit of work interface")]
    public void Dependency_injection_resolves_one_scoped_context_for_the_unit_of_work_interface()
    {
        Dictionary<string, string?> configurationValues = new()
        {
            ["ConnectionStrings:Cratebase"] = "Host=localhost;Database=cratebase;Username=cratebase;Password=cratebase"
        };
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationValues)
            .Build();
        ServiceCollection services = [];
        _ = DependencyInjection.AddCratebaseInfrastructure(services, configuration);

        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        using IServiceScope scope = serviceProvider.CreateScope();
        CratebaseDbContext context = scope.ServiceProvider.GetRequiredService<CratebaseDbContext>();
        IUnitOfWork unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        Assert.Same(context, unitOfWork);
    }

    [Fact(DisplayName = "Repositories use public identifiers for command lookup")]
    public async Task Repositories_use_public_identifiers_for_command_lookup()
    {
        await using CratebaseDbContext context = await CreateMigratedContextAsync();
        IRepository<Label, LabelId> labels = ((IUnitOfWork)context).GetRepository<Label, LabelId>();
        var collectionId = CollectionId.New();
        await TestCollectionFactory.AddCollectionAsync(context, collectionId);
        var labelId = LabelId.New();
        var label = Label.Create(collectionId, labelId, "Factory");

        labels.Add(label);
        _ = await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        Label? actualLabel = await labels.TryFindAsync(labelId);
        Assert.NotNull(actualLabel);
        Assert.Equal(labelId, actualLabel.Id);

        await labels.DeleteAsync(labelId);
        _ = await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        Assert.Null(await labels.TryFindAsync(labelId));
    }

    private static async Task<IReadOnlyList<string>> ReadColumnNamesAsync(CratebaseDbContext context, string tableName)
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

    private static async Task<IReadOnlyList<string>> ReadJsonbColumnNamesAsync(CratebaseDbContext context)
    {
        FormattableString sql = $"""
            SELECT table_name || '.' || column_name
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND udt_name = 'jsonb'
            ORDER BY table_name, column_name
            """;

        return await context.Database.SqlQuery<string>(sql).ToArrayAsync();
    }

    private static async Task<IReadOnlyList<string>> ReadTableNamesAsync(CratebaseDbContext context)
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

    private async Task<CratebaseDbContext> CreateMigratedContextAsync()
    {
        string connectionString = await _postgres.CreateDatabaseAsync();
        CratebaseDbContext context = new(CreateOptions(connectionString));
        await context.Database.MigrateAsync();

        return context;
    }

    private static DbContextOptions<CratebaseDbContext> CreateOptions(string connectionString)
    {
        return new DbContextOptionsBuilder<CratebaseDbContext>()
            .UseNpgsql(connectionString)
            .Options;
    }
}
