using DiscWeave.Domain.Collection;
using DiscWeave.Domain.Ratings;
using DiscWeave.Domain.Settings;
using DiscWeave.Domain.SharedKernel.Ids;
using DiscWeave.Infrastructure.Identity;
using DiscWeave.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;

namespace DiscWeave.Api.Features.Auth;

internal static class UserProvisioning
{
    public static async Task<IdentityResult> EnsureRolesAsync(RoleManager<IdentityRole<Guid>> roleManager)
    {
        foreach (string roleName in new[] { DiscWeaveRoles.Admin, DiscWeaveRoles.User })
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                IdentityResult result = await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
                if (!result.Succeeded)
                {
                    return result;
                }
            }
        }

        return IdentityResult.Success;
    }

    public static async Task<(IdentityResult Result, DiscWeaveUser? User)> CreateUserWithCollectionAsync(
        string email,
        string password,
        IReadOnlyList<string> roles,
        UserManager<DiscWeaveUser> userManager,
        DiscWeaveDbContext context,
        CancellationToken cancellationToken)
    {
        var collectionId = CollectionId.New();
        DiscWeaveUser user = CreateUser(email);
        IdentityResult createResult = await userManager.CreateAsync(user, password);
        if (!createResult.Succeeded)
        {
            return (createResult, null);
        }

        IdentityResult roleResult = await userManager.AddToRolesAsync(user, roles);
        if (!roleResult.Succeeded)
        {
            return (roleResult, null);
        }

        _ = context.MusicCollections.Add(MusicCollection.Create(collectionId, new UserId(user.Id), "Main collection"));
        context.CollectionDictionaryEntries.AddRange(CollectionDictionaryDefaults.CreateEntries(collectionId));
        context.RatingCriteria.AddRange(RatingCriterionDefaults.CreateCriteria(collectionId));
        _ = await context.SaveChangesAsync(cancellationToken);

        user.DefaultCollectionId = collectionId;
        IdentityResult updateResult = await userManager.UpdateAsync(user);

        return (updateResult, updateResult.Succeeded ? user : null);
    }

    private static DiscWeaveUser CreateUser(string email)
    {
        string normalizedEmail = email.Trim();

        return new DiscWeaveUser
        {
            Id = Guid.CreateVersion7(),
            Email = normalizedEmail,
            UserName = normalizedEmail
        };
    }
}
