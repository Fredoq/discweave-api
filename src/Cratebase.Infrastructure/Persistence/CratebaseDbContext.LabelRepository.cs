using Cratebase.Application.Persistence;
using Cratebase.Domain.Catalog;
using Cratebase.Domain.SharedKernel.Ids;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Infrastructure.Persistence;

public partial class CratebaseDbContext : IRepository<Label, LabelId>
{
    async Task<Label?> IRepository<Label, LabelId>.TryFindAsync(
        LabelId key,
        CancellationToken cancellationToken)
    {
        return await Labels.FirstOrDefaultAsync(label => label.Id == key, cancellationToken);
    }

    void IRepository<Label, LabelId>.Add(Label aggregate)
    {
        _ = Labels.Add(aggregate);
    }

    void IRepository<Label, LabelId>.Delete(Label aggregate)
    {
        _ = Labels.Remove(aggregate);
    }
}
