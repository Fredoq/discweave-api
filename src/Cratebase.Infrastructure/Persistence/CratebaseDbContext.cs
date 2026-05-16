using Cratebase.Application.Errors;
using Cratebase.Application.Persistence;
using Cratebase.Application.Security;
using Cratebase.Domain.Catalog;
using Cratebase.Domain.Collection;
using Cratebase.Domain.Credits;
using Cratebase.Domain.Imports;
using Cratebase.Domain.Ratings;
using Cratebase.Domain.Relations;
using Cratebase.Domain.Settings;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Infrastructure.Identity;
using Cratebase.Infrastructure.Persistence.Configurations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Infrastructure.Persistence;

public partial class CratebaseDbContext : IdentityDbContext<CratebaseUser, IdentityRole<Guid>, Guid>, IUnitOfWork
{
    private const string RatingCriterionCodeUniqueIndex = "IX_rating_criteria_collection_id_code";

    public CratebaseDbContext(DbContextOptions<CratebaseDbContext> options)
        : base(options)
    {
    }

    public CratebaseDbContext(DbContextOptions<CratebaseDbContext> options, ICurrentCollection currentCollection)
        : base(options)
    {
        ArgumentNullException.ThrowIfNull(currentCollection);

        CurrentCollectionId = currentCollection.CollectionId;
        HasCurrentCollection = true;
    }

    public DbSet<MusicCollection> MusicCollections => Set<MusicCollection>();

    public DbSet<Artist> Artists => Set<Artist>();

    public DbSet<Label> Labels => Set<Label>();

    public DbSet<Release> Releases => Set<Release>();

    public DbSet<Track> Tracks => Set<Track>();

    public DbSet<OwnedItem> OwnedItems => Set<OwnedItem>();

    public DbSet<Credit> Credits => Set<Credit>();

    public DbSet<ArtistRelation> ArtistRelations => Set<ArtistRelation>();

    public DbSet<TrackRelation> TrackRelations => Set<TrackRelation>();

    public DbSet<CollectionDictionaryEntry> CollectionDictionaryEntries => Set<CollectionDictionaryEntry>();

    public DbSet<RatingCriterion> RatingCriteria => Set<RatingCriterion>();

    public DbSet<RatingValue> RatingValues => Set<RatingValue>();

    public DbSet<ImportPattern> ImportPatterns => Set<ImportPattern>();

    public DbSet<ReleaseImportSession> ReleaseImportSessions => Set<ReleaseImportSession>();

    public DbSet<ReleaseImportDraft> ReleaseImportDrafts => Set<ReleaseImportDraft>();

    public DbSet<ReleaseImportDraftTrack> ReleaseImportDraftTracks => Set<ReleaseImportDraftTrack>();

    public bool HasCurrentCollection { get; private set; }

    public CollectionId CurrentCollectionId { get; private set; }

    public IRepository<TAggregate, TKey> GetRepository<TAggregate, TKey>()
    {
        return (IRepository<TAggregate, TKey>)this;
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (PostgresPersistenceErrors.IsReferencedResourceMissing(exception))
        {
            throw new ReferencedResourceMissingException(exception);
        }
        catch (DbUpdateException exception) when (PostgresPersistenceErrors.IsResourceHasDependents(exception))
        {
            throw new ResourceHasDependentsException(exception);
        }
        catch (DbUpdateException exception) when (PostgresPersistenceErrors.IsUniqueConstraintViolation(exception, RatingCriterionCodeUniqueIndex))
        {
            throw new ResourceConflictException(ResourceConflictException.RatingCriterionCode, exception);
        }
        catch (DbUpdateException exception) when (PostgresPersistenceErrors.IsRatingValueTargetConflict(exception))
        {
            throw new ResourceConflictException(ResourceConflictException.RatingValueTarget, exception);
        }
        catch (InvalidOperationException exception) when (EfCorePersistenceErrors.IsRequiredRelationshipConflict(exception))
        {
            throw new ResourceHasDependentsException(exception);
        }
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        _ = builder.ApplyConfiguration(new ArtistConfiguration());
        _ = builder.ApplyConfiguration(new ArtistRelationConfiguration());
        _ = builder.ApplyConfiguration(new CollectionDictionaryEntryConfiguration());
        _ = builder.ApplyConfiguration(new CreditConfiguration());
        _ = builder.ApplyConfiguration(new LabelConfiguration());
        _ = builder.ApplyConfiguration(new ImportPatternConfiguration());
        _ = builder.ApplyConfiguration(new MusicCollectionConfiguration());
        _ = builder.ApplyConfiguration(new OwnedItemConfiguration());
        _ = builder.ApplyConfiguration(new RatingCriterionConfiguration());
        _ = builder.ApplyConfiguration(new RatingValueConfiguration());
        _ = builder.ApplyConfiguration(new ReleaseConfiguration());
        _ = builder.ApplyConfiguration(new ReleaseImportDraftConfiguration());
        _ = builder.ApplyConfiguration(new ReleaseImportDraftTrackConfiguration());
        _ = builder.ApplyConfiguration(new ReleaseImportSessionConfiguration());
        _ = builder.ApplyConfiguration(new TrackConfiguration());
        _ = builder.ApplyConfiguration(new TrackRelationConfiguration());

        ConfigureIdentity(builder);
        ConfigureCollectionFilters(builder);
    }

    private static void ConfigureIdentity(ModelBuilder builder)
    {
        _ = builder.Entity<CratebaseUser>(user =>
        {
            _ = user.Property(value => value.DefaultCollectionId)
                .HasConversion(
                    value => value.HasValue ? value.Value.Value : (Guid?)null,
                    value => value.HasValue ? new CollectionId(value.Value) : null);

            _ = user.HasOne<MusicCollection>()
                .WithMany()
                .HasForeignKey(value => value.DefaultCollectionId)
                .HasPrincipalKey(collection => collection.Id)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    private void ConfigureCollectionFilters(ModelBuilder modelBuilder)
    {
        _ = modelBuilder.Entity<Artist>().HasQueryFilter(artist => !HasCurrentCollection || artist.CollectionId == CurrentCollectionId);
        _ = modelBuilder.Entity<Label>().HasQueryFilter(label => !HasCurrentCollection || label.CollectionId == CurrentCollectionId);
        _ = modelBuilder.Entity<Release>().HasQueryFilter(release => !HasCurrentCollection || release.CollectionId == CurrentCollectionId);
        _ = modelBuilder.Entity<Track>().HasQueryFilter(track => !HasCurrentCollection || track.CollectionId == CurrentCollectionId);
        _ = modelBuilder.Entity<OwnedItem>().HasQueryFilter(item => !HasCurrentCollection || item.CollectionId == CurrentCollectionId);
        _ = modelBuilder.Entity<Credit>().HasQueryFilter(credit => !HasCurrentCollection || credit.CollectionId == CurrentCollectionId);
        _ = modelBuilder.Entity<ArtistRelation>().HasQueryFilter(relation => !HasCurrentCollection || relation.CollectionId == CurrentCollectionId);
        _ = modelBuilder.Entity<TrackRelation>().HasQueryFilter(relation => !HasCurrentCollection || relation.CollectionId == CurrentCollectionId);
        _ = modelBuilder.Entity<CollectionDictionaryEntry>().HasQueryFilter(entry => !HasCurrentCollection || entry.CollectionId == CurrentCollectionId);
        _ = modelBuilder.Entity<RatingCriterion>().HasQueryFilter(criterion => !HasCurrentCollection || criterion.CollectionId == CurrentCollectionId);
        _ = modelBuilder.Entity<RatingValue>().HasQueryFilter(value => !HasCurrentCollection || value.CollectionId == CurrentCollectionId);
        _ = modelBuilder.Entity<ImportPattern>().HasQueryFilter(pattern => !HasCurrentCollection || pattern.CollectionId == CurrentCollectionId);
        _ = modelBuilder.Entity<ReleaseImportSession>().HasQueryFilter(session => !HasCurrentCollection || session.CollectionId == CurrentCollectionId);
        _ = modelBuilder.Entity<ReleaseImportDraft>().HasQueryFilter(draft => !HasCurrentCollection || draft.CollectionId == CurrentCollectionId);
        _ = modelBuilder.Entity<ReleaseImportDraftTrack>().HasQueryFilter(track => !HasCurrentCollection || track.CollectionId == CurrentCollectionId);
    }
}
