using Cratebase.Application.Errors;
using Cratebase.Domain.Catalog;
using Cratebase.Domain.Collection;
using Cratebase.Domain.Credits;
using Cratebase.Domain.Relations;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Infrastructure.Tests;

public sealed class CratebaseDbContextCollectionBoundaryTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _postgres;

    public CratebaseDbContextCollectionBoundaryTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact(DisplayName = "Cross-collection release track references fail")]
    public async Task Cross_collection_release_track_references_fail()
    {
        await AssertForeignKeyViolationAsync(async context =>
        {
            var releaseCollectionId = CollectionId.New();
            var trackCollectionId = CollectionId.New();
            await TestCollectionFactory.AddCollectionAsync(context, releaseCollectionId);
            await TestCollectionFactory.AddCollectionAsync(context, trackCollectionId);
            var track = Track.Create(trackCollectionId, TrackId.New(), "Confusion");
            Release release = Release.Create(releaseCollectionId, ReleaseId.New(), "Confusion")
                .WithTrack(ReleaseTrack.Create(track.Id, TrackPosition.FromNumber(1)));

            _ = context.Tracks.Add(track);
            _ = context.Releases.Add(release);
            await Task.CompletedTask;
        });
    }

    [Fact(DisplayName = "Cross-collection credit references fail")]
    public async Task Cross_collection_credit_references_fail()
    {
        await AssertForeignKeyViolationAsync(async context =>
        {
            var targetCollectionId = CollectionId.New();
            var artistCollectionId = CollectionId.New();
            await TestCollectionFactory.AddCollectionAsync(context, targetCollectionId);
            await TestCollectionFactory.AddCollectionAsync(context, artistCollectionId);
            Artist artist = Person.Create(artistCollectionId, ArtistId.New(), "Arthur Baker");
            var release = Release.Create(targetCollectionId, ReleaseId.New(), "Confusion");

            _ = context.Artists.Add(artist);
            _ = context.Releases.Add(release);
            _ = await context.SaveChangesAsync();
            _ = context.Credits.Add(Credit.Create(targetCollectionId, CreditId.New(), CreditContributor.FromArtist(artist), CreditTarget.ForRelease(release.Id), CreditRole.Producer));
        });
    }

    [Fact(DisplayName = "Cross-collection relation references fail")]
    public async Task Cross_collection_relation_references_fail()
    {
        await AssertForeignKeyViolationAsync(async context =>
        {
            var relationCollectionId = CollectionId.New();
            var targetCollectionId = CollectionId.New();
            await TestCollectionFactory.AddCollectionAsync(context, relationCollectionId);
            await TestCollectionFactory.AddCollectionAsync(context, targetCollectionId);
            Artist source = Person.Create(relationCollectionId, ArtistId.New(), "Arthur Baker");
            Artist target = Person.Create(targetCollectionId, ArtistId.New(), "Arthur Baker Alias");

            _ = context.Artists.Add(source);
            _ = context.Artists.Add(target);
            _ = await context.SaveChangesAsync();
            _ = context.ArtistRelations.Add(ArtistRelation.Create(ArtistRelationId.New(), relationCollectionId, source.Id, target.Id, ArtistRelationType.Alias));
        });
    }

    [Fact(DisplayName = "Cross-collection owned item references fail")]
    public async Task Cross_collection_owned_item_references_fail()
    {
        await AssertForeignKeyViolationAsync(async context =>
        {
            var itemCollectionId = CollectionId.New();
            var releaseCollectionId = CollectionId.New();
            await TestCollectionFactory.AddCollectionAsync(context, itemCollectionId);
            await TestCollectionFactory.AddCollectionAsync(context, releaseCollectionId);
            var release = Release.Create(releaseCollectionId, ReleaseId.New(), "Confusion");

            _ = context.Releases.Add(release);
            _ = await context.SaveChangesAsync();
            _ = context.OwnedItems.Add(OwnedItem.Create(itemCollectionId, OwnedItemId.New(), OwnedItemTarget.ForRelease(release.Id), OwnershipStatus.Owned, VinylRecord.Create("12-inch")));
        });
    }

    [Fact(DisplayName = "Duplicate digital import identity is unique per collection")]
    public async Task Duplicate_digital_import_identity_is_unique_per_collection()
    {
        await using CratebaseDbContext context = await CreateMigratedContextAsync();
        var firstCollectionId = CollectionId.New();
        var secondCollectionId = CollectionId.New();
        await TestCollectionFactory.AddCollectionAsync(context, firstCollectionId);
        await TestCollectionFactory.AddCollectionAsync(context, secondCollectionId);
        var firstRelease = Release.Create(firstCollectionId, ReleaseId.New(), "Confusion");
        var secondRelease = Release.Create(secondCollectionId, ReleaseId.New(), "Confusion");

        _ = context.Releases.Add(firstRelease);
        _ = context.Releases.Add(secondRelease);
        _ = await context.SaveChangesAsync();

        _ = context.OwnedItems.Add(CreateDigitalOwnedItem(firstCollectionId, firstRelease.Id));
        _ = context.OwnedItems.Add(CreateDigitalOwnedItem(secondCollectionId, secondRelease.Id));
        _ = await context.SaveChangesAsync();

        _ = context.OwnedItems.Add(CreateDigitalOwnedItem(firstCollectionId, firstRelease.Id));
        ResourceConflictException exception = await Assert.ThrowsAsync<ResourceConflictException>(() => context.SaveChangesAsync());
        Assert.Equal(ResourceConflictException.IntegrityConstraint, exception.Conflict);
    }

    private async Task AssertForeignKeyViolationAsync(Func<CratebaseDbContext, Task> arrangeAsync)
    {
        await using CratebaseDbContext context = await CreateMigratedContextAsync();
        await arrangeAsync(context);

        _ = await Assert.ThrowsAsync<ReferencedResourceMissingException>(() => context.SaveChangesAsync());
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

    private static OwnedItem CreateDigitalOwnedItem(CollectionId collectionId, ReleaseId releaseId)
    {
        return OwnedItem.Create(
            collectionId,
            OwnedItemId.New(),
            OwnedItemTarget.ForRelease(releaseId),
            OwnershipStatus.Owned,
            DigitalFile.Create(
                FilePath.FromAbsolutePath("/music/New Order/Confusion.flac"),
                AudioFileFormat.Flac,
                FileImportIdentity.Create(
                    FilePath.FromAbsolutePath("/music/New Order/Confusion.flac"),
                    123_456,
                    DateTimeOffset.UnixEpoch,
                    "abcdef")));
    }
}
