using Cratebase.Application.Persistence;
using Cratebase.Domain.Catalog;
using Cratebase.Domain.Collection;
using Cratebase.Domain.Credits;
using Cratebase.Domain.Ratings;
using Cratebase.Domain.Relations;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Infrastructure.Tests;

public sealed class CratebaseDbContextRatingTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _postgres;

    public CratebaseDbContextRatingTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact(DisplayName = "The context persists rating criteria and rating values")]
    public async Task The_context_persists_rating_criteria_and_rating_values()
    {
        await using CratebaseDbContext context = await CreateMigratedContextAsync();
        var collectionId = CollectionId.New();
        await TestCollectionFactory.AddCollectionAsync(context, collectionId);
        RatingCriterion criterion = await context.RatingCriteria.SingleAsync(entity =>
            entity.CollectionId == collectionId && entity.Code == RatingCriterionDefaults.OverallCode);
        RatingCriterionId criterionId = criterion.Id;
        var release = Release.Create(collectionId, ReleaseId.New(), "Power, Corruption & Lies");
        var track = Track.Create(collectionId, TrackId.New(), "Age of Consent");
        var releaseRating = RatingValue.Create(
            collectionId,
            RatingValueId.New(),
            criterionId,
            RatingTarget.ForRelease(release.Id),
            Rating.FromValue(9));
        var trackRating = RatingValue.Create(
            collectionId,
            RatingValueId.New(),
            criterionId,
            RatingTarget.ForTrack(track.Id),
            Rating.FromValue(10));

        _ = context.Releases.Add(release);
        _ = context.Tracks.Add(track);
        context.RatingValues.AddRange(releaseRating, trackRating);
        _ = await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        RatingCriterion actualCriterion = await context.RatingCriteria.SingleAsync(entity => entity.Id == criterionId);
        RatingValue[] actualRatings = [.. (await context.RatingValues.ToArrayAsync()).OrderBy(value => value.Rating.Value)];

        Assert.True(actualCriterion.IsProtected);
        Assert.Contains(actualCriterion.TargetTypes, targetType => targetType == RatingTargetType.Release);
        Assert.Contains(actualCriterion.TargetTypes, targetType => targetType == RatingTargetType.Track);
        Assert.Equal(9, actualRatings[0].Rating.Value);
        Assert.Equal(release.Id, Assert.IsType<ReleaseRatingTarget>(actualRatings[0].Target).ReleaseId);
        Assert.Equal(10, actualRatings[1].Rating.Value);
        Assert.Equal(track.Id, Assert.IsType<TrackRatingTarget>(actualRatings[1].Target).TrackId);
    }

    [Fact(DisplayName = "The unit of work returns repositories for current aggregate roots")]
    public async Task The_unit_of_work_returns_repositories_for_current_aggregate_roots()
    {
        await using CratebaseDbContext context = new(CreateOptions("Host=localhost;Database=cratebase;Username=cratebase;Password=cratebase"));

        Assert.Same(context, ((IUnitOfWork)context).GetRepository<Artist, ArtistId>());
        Assert.Same(context, ((IUnitOfWork)context).GetRepository<Label, LabelId>());
        Assert.Same(context, ((IUnitOfWork)context).GetRepository<Release, ReleaseId>());
        Assert.Same(context, ((IUnitOfWork)context).GetRepository<Track, TrackId>());
        Assert.Same(context, ((IUnitOfWork)context).GetRepository<OwnedItem, OwnedItemId>());
        Assert.Same(context, ((IUnitOfWork)context).GetRepository<Credit, CreditId>());
        Assert.Same(context, ((IUnitOfWork)context).GetRepository<ArtistRelation, ArtistRelationId>());
        Assert.Same(context, ((IUnitOfWork)context).GetRepository<TrackRelation, TrackRelationId>());
        Assert.Same(context, ((IUnitOfWork)context).GetRepository<RatingCriterion, RatingCriterionId>());
        Assert.Same(context, ((IUnitOfWork)context).GetRepository<RatingValue, RatingValueId>());
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
