using Cratebase.Application.Persistence;
using Cratebase.Domain.Relations;
using Cratebase.Domain.SharedKernel.Ids;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Infrastructure.Persistence;

public partial class CratebaseDbContext : IRepository<TrackRelation, TrackRelationId>
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
