using DiscWeave.Application.Persistence;
using DiscWeave.Domain.Ratings;
using DiscWeave.Domain.SharedKernel.Ids;
using Microsoft.EntityFrameworkCore;

namespace DiscWeave.Infrastructure.Persistence;

public partial class DiscWeaveDbContext : IRepository<RatingCriterion, RatingCriterionId>
{
    async Task<RatingCriterion?> IRepository<RatingCriterion, RatingCriterionId>.TryFindAsync(
        RatingCriterionId key,
        CancellationToken cancellationToken)
    {
        return await RatingCriteria.FirstOrDefaultAsync(criterion => criterion.Id == key, cancellationToken);
    }

    void IRepository<RatingCriterion, RatingCriterionId>.Add(RatingCriterion aggregate)
    {
        _ = RatingCriteria.Add(aggregate);
    }

    void IRepository<RatingCriterion, RatingCriterionId>.Delete(RatingCriterion aggregate)
    {
        _ = RatingCriteria.Remove(aggregate);
    }
}
