using Cratebase.Domain.SharedKernel.Ids;
using Microsoft.AspNetCore.Identity;

namespace Cratebase.Infrastructure.Identity;

public sealed class CratebaseUser : IdentityUser<Guid>
{
    public CollectionId? DefaultCollectionId { get; set; }

    public bool IsDisabled { get; set; }
}
