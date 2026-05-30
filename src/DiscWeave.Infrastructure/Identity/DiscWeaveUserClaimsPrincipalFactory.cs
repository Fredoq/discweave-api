using System.Security.Claims;
using DiscWeave.Application.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace DiscWeave.Infrastructure.Identity;

public sealed class DiscWeaveUserClaimsPrincipalFactory : UserClaimsPrincipalFactory<DiscWeaveUser, IdentityRole<Guid>>
{
    public DiscWeaveUserClaimsPrincipalFactory(
        UserManager<DiscWeaveUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        IOptions<IdentityOptions> optionsAccessor)
        : base(userManager, roleManager, optionsAccessor)
    {
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(DiscWeaveUser user)
    {
        ClaimsIdentity identity = await base.GenerateClaimsAsync(user);
        if (user.DefaultCollectionId is { } defaultCollectionId)
        {
            identity.AddClaim(new Claim(DiscWeaveClaimTypes.DefaultCollectionId, defaultCollectionId.Value.ToString()));
        }

        return identity;
    }
}
