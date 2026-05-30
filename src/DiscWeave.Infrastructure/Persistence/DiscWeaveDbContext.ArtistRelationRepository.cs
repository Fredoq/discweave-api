using DiscWeave.Application.Persistence;
using DiscWeave.Domain.Relations;
using DiscWeave.Domain.SharedKernel.Ids;
using Microsoft.EntityFrameworkCore;

namespace DiscWeave.Infrastructure.Persistence;

public partial class DiscWeaveDbContext : IRepository<ArtistRelation, ArtistRelationId>
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
