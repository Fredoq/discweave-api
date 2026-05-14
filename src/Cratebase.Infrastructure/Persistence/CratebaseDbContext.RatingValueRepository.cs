using Cratebase.Application.Persistence;
using Cratebase.Domain.Ratings;
using Cratebase.Domain.SharedKernel.Ids;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Infrastructure.Persistence;

public partial class CratebaseDbContext : IRepository<RatingValue, RatingValueId>
{
    async Task<RatingValue?> IRepository<RatingValue, RatingValueId>.TryFindAsync(
        RatingValueId key,
        CancellationToken cancellationToken)
    {
        return await RatingValues.FirstOrDefaultAsync(value => value.Id == key, cancellationToken);
    }

    void IRepository<RatingValue, RatingValueId>.Add(RatingValue aggregate)
    {
        _ = RatingValues.Add(aggregate);
    }

    void IRepository<RatingValue, RatingValueId>.Delete(RatingValue aggregate)
    {
        _ = RatingValues.Remove(aggregate);
    }
}
