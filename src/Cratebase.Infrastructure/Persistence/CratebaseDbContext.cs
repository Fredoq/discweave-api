using Cratebase.Application.Persistence;
using Cratebase.Domain.Catalog;
using Cratebase.Domain.Collection;
using Cratebase.Domain.Credits;
using Cratebase.Domain.Relations;
using Cratebase.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Infrastructure.Persistence;

public partial class CratebaseDbContext : DbContext, IUnitOfWork
{
    public CratebaseDbContext(DbContextOptions<CratebaseDbContext> options)
        : base(options)
    {
    }

    public DbSet<Artist> Artists => Set<Artist>();

    public DbSet<Label> Labels => Set<Label>();

    public DbSet<Release> Releases => Set<Release>();

    public DbSet<Track> Tracks => Set<Track>();

    public DbSet<OwnedItem> OwnedItems => Set<OwnedItem>();

    public DbSet<Credit> Credits => Set<Credit>();

    public DbSet<ArtistRelation> ArtistRelations => Set<ArtistRelation>();

    public DbSet<TrackRelation> TrackRelations => Set<TrackRelation>();

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
        catch (DbUpdateException exception) when (PostgresPersistenceErrors.IsForeignKeyViolation(exception))
        {
            throw new PersistenceConflictException(PersistenceConflictKind.ForeignKeyViolation, exception);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        _ = modelBuilder.ApplyConfiguration(new ArtistConfiguration());
        _ = modelBuilder.ApplyConfiguration(new ArtistRelationConfiguration());
        _ = modelBuilder.ApplyConfiguration(new CreditConfiguration());
        _ = modelBuilder.ApplyConfiguration(new LabelConfiguration());
        _ = modelBuilder.ApplyConfiguration(new OwnedItemConfiguration());
        _ = modelBuilder.ApplyConfiguration(new ReleaseConfiguration());
        _ = modelBuilder.ApplyConfiguration(new TrackConfiguration());
        _ = modelBuilder.ApplyConfiguration(new TrackRelationConfiguration());
    }
}
