using DiscWeave.Application.Persistence;
using DiscWeave.Domain.Catalog;
using DiscWeave.Domain.SharedKernel.Ids;
using Microsoft.EntityFrameworkCore;

namespace DiscWeave.Infrastructure.Persistence;

public partial class DiscWeaveDbContext : IRepository<Label, LabelId>
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
