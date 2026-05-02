using Microsoft.AspNetCore.Identity;

namespace Cratebase.Infrastructure.Identity;

public sealed class CratebaseUser : IdentityUser<Guid>
{
    public Guid DefaultCollectionId { get; set; }

    public bool IsDisabled { get; set; }
}
