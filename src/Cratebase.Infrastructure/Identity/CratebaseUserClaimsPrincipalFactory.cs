using System.Security.Claims;
using Cratebase.Application.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Cratebase.Infrastructure.Identity;

public sealed class CratebaseUserClaimsPrincipalFactory : UserClaimsPrincipalFactory<CratebaseUser, IdentityRole<Guid>>
{
    public CratebaseUserClaimsPrincipalFactory(
        UserManager<CratebaseUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        IOptions<IdentityOptions> optionsAccessor)
        : base(userManager, roleManager, optionsAccessor)
    {
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(CratebaseUser user)
    {
        ClaimsIdentity identity = await base.GenerateClaimsAsync(user);
        if (user.DefaultCollectionId is { } defaultCollectionId)
        {
            identity.AddClaim(new Claim(CratebaseClaimTypes.DefaultCollectionId, defaultCollectionId.Value.ToString()));
        }

        return identity;
    }
}
