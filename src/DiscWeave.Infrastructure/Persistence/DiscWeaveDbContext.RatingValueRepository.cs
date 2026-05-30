using DiscWeave.Application.Persistence;
using DiscWeave.Domain.Ratings;
using DiscWeave.Domain.SharedKernel.Ids;
using Microsoft.EntityFrameworkCore;

namespace DiscWeave.Infrastructure.Persistence;

public partial class DiscWeaveDbContext : IRepository<RatingValue, RatingValueId>
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
