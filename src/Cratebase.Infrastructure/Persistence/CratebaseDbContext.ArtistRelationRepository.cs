using Cratebase.Application.Persistence;
using Cratebase.Domain.Relations;
using Cratebase.Domain.SharedKernel.Ids;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Infrastructure.Persistence;

public partial class CratebaseDbContext : IRepository<ArtistRelation, ArtistRelationId>
{
    async Task<ArtistRelation?> IRepository<ArtistRelation, ArtistRelationId>.TryFindAsync(
        ArtistRelationId key,
        CancellationToken cancellationToken)
    {
        return await ArtistRelations.FirstOrDefaultAsync(relation => relation.Id == key, cancellationToken);
    }

    void IRepository<ArtistRelation, ArtistRelationId>.Add(ArtistRelation aggregate)
    {
        _ = ArtistRelations.Add(aggregate);
    }

    void IRepository<ArtistRelation, ArtistRelationId>.Delete(ArtistRelation aggregate)
    {
        _ = ArtistRelations.Remove(aggregate);
    }
}
