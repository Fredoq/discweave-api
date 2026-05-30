using DiscWeave.Domain.SharedKernel.Ids;
using Microsoft.AspNetCore.Identity;

namespace DiscWeave.Infrastructure.Identity;

public sealed class DiscWeaveUser : IdentityUser<Guid>
{
    public CollectionId? DefaultCollectionId { get; set; }

    public bool IsDisabled { get; set; }
}
