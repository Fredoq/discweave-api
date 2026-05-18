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
using Cratebase.Infrastructure.Persistence.Search;
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

    internal DbSet<SearchDocument> SearchDocuments => Set<SearchDocument>();

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
            HashSet<CollectionId> searchCollections = CollectSearchDocumentCollections();
            if (searchCollections.Count == 0)
            {
                return await base.SaveChangesAsync(cancellationToken);
            }

            if (Database.CurrentTransaction is not null)
            {
                int result = await base.SaveChangesAsync(cancellationToken);
                await SearchDocumentRebuilder.RebuildAsync(this, searchCollections, cancellationToken);

                return result;
            }

            await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction = await Database.BeginTransactionAsync(cancellationToken);
            int saved = await base.SaveChangesAsync(cancellationToken);
            await SearchDocumentRebuilder.RebuildAsync(this, searchCollections, cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return saved;
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
        _ = builder.ApplyConfiguration(new SearchDocumentConfiguration());
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
        _ = modelBuilder.Entity<SearchDocument>().HasQueryFilter(document => !HasCurrentCollection || document.CollectionId == CurrentCollectionId);
    }

    private HashSet<CollectionId> CollectSearchDocumentCollections()
    {
        HashSet<CollectionId> collectionIds = [];

        foreach (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry in ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            if (TryGetSearchCollectionId(entry.Entity, out CollectionId collectionId))
            {
                _ = collectionIds.Add(collectionId);
            }
        }

        return collectionIds;
    }

    private static bool TryGetSearchCollectionId(object entity, out CollectionId collectionId)
    {
        collectionId = default;

        switch (entity)
        {
            case Artist artist:
                collectionId = artist.CollectionId;
                return true;
            case Label label:
                collectionId = label.CollectionId;
                return true;
            case Release release:
                collectionId = release.CollectionId;
                return true;
            case Track track:
                collectionId = track.CollectionId;
                return true;
            case OwnedItem ownedItem:
                collectionId = ownedItem.CollectionId;
                return true;
            case Credit credit:
                collectionId = credit.CollectionId;
                return true;
            case ArtistRelation artistRelation:
                collectionId = artistRelation.CollectionId;
                return true;
            case TrackRelation trackRelation:
                collectionId = trackRelation.CollectionId;
                return true;
            case CollectionDictionaryEntry entry:
                collectionId = entry.CollectionId;
                return true;
            default:
                return false;
        }
    }
}
