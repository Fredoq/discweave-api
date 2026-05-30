using DiscWeave.Application.Persistence;
using DiscWeave.Domain.Relations;
using DiscWeave.Domain.SharedKernel.Ids;
using Microsoft.EntityFrameworkCore;

namespace DiscWeave.Infrastructure.Persistence;

public partial class DiscWeaveDbContext : IRepository<TrackRelation, TrackRelationId>
{
    async Task<TrackRelation?> IRepository<TrackRelation, TrackRelationId>.TryFindAsync(
        TrackRelationId key,
        CancellationToken cancellationToken)
    {
        return await TrackRelations.FirstOrDefaultAsync(relation => relation.Id == key, cancellationToken);
    }

    void IRepository<TrackRelation, TrackRelationId>.Add(TrackRelation aggregate)
    {
        _ = TrackRelations.Add(aggregate);
    }

    void IRepository<TrackRelation, TrackRelationId>.Delete(TrackRelation aggregate)
    {
        _ = TrackRelations.Remove(aggregate);
    }
}
