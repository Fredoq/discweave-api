using Cratebase.Api.Auth;
using Cratebase.Api.Http;
using Cratebase.Domain.Collection;
using Cratebase.Domain.Ratings;
using Cratebase.Domain.Settings;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Infrastructure.Identity;
using Cratebase.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Api.Features.Admin;

public static class AdminUsersEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapAdminUsersEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        _ = endpoints.MapGet("/api/admin/users", ListUsersAsync)
            .WithTags("Admin Users")
            .RequireAuthorization(CratebaseAuthorizationPolicies.Admin)
            .WithName("ListUsers");

        RouteGroupBuilder group = endpoints.MapGroup("/api/admin/users")
            .WithTags("Admin Users")
            .RequireAuthorization(CratebaseAuthorizationPolicies.Admin);

        _ = group.MapPost("/", CreateUserAsync).WithName("CreateUser");
        _ = group.MapPatch("/{userId:guid}/status", UpdateStatusAsync).WithName("UpdateUserStatus");

        return endpoints;
    }

    private static async Task<IResult> ListUsersAsync(UserManager<CratebaseUser> userManager, CancellationToken cancellationToken)
    {
        CratebaseUser[] users = await userManager.Users
            .OrderBy(user => user.Email)
            .ToArrayAsync(cancellationToken);

        List<AdminUserResponse> responses = [];
        foreach (CratebaseUser user in users)
        {
            responses.Add(await ToResponseAsync(user, userManager));
        }

        return Results.Ok(responses);
    }

    private static async Task<IResult> CreateUserAsync(
        CreateUserRequest request,
        UserManager<CratebaseUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        CratebaseDbContext context,
        CancellationToken cancellationToken)
    {
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await context.Database.BeginTransactionAsync(cancellationToken);

        IdentityResult rolesResult = await EnsureRolesAsync(roleManager);
        if (!rolesResult.Succeeded)
        {
            return IdentityError(rolesResult);
        }

        var collectionId = CollectionId.New();
        string normalizedEmail = request.Email.Trim();
        var user = new CratebaseUser
        {
            Id = Guid.CreateVersion7(),
            Email = normalizedEmail,
            UserName = normalizedEmail
        };

        IdentityResult createResult = await userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            return IdentityError(createResult);
        }

        string[] roles = request.IsAdmin ? [CratebaseRoles.Admin, CratebaseRoles.User] : [CratebaseRoles.User];
        IdentityResult roleResult = await userManager.AddToRolesAsync(user, roles);
        if (!roleResult.Succeeded)
        {
            return IdentityError(roleResult);
        }

        _ = context.MusicCollections.Add(MusicCollection.Create(collectionId, new UserId(user.Id), "Main collection"));
        context.CollectionDictionaryEntries.AddRange(CollectionDictionaryDefaults.CreateEntries(collectionId));
        context.RatingCriteria.AddRange(RatingCriterionDefaults.CreateCriteria(collectionId));
        _ = await context.SaveChangesAsync(cancellationToken);

        user.DefaultCollectionId = collectionId;
        IdentityResult updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            return IdentityError(updateResult);
        }

        await transaction.CommitAsync(cancellationToken);

        return Results.Created($"/api/admin/users/{user.Id}", await ToResponseAsync(user, userManager));
    }

    private static async Task<IResult> UpdateStatusAsync(
        Guid userId,
        UpdateUserStatusRequest request,
        UserManager<CratebaseUser> userManager)
    {
        CratebaseUser? user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return EndpointErrors.NotFound("user.not_found", "User was not found");
        }

        user.IsDisabled = request.IsDisabled;
        IdentityResult result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return IdentityError(result);
        }

        IdentityResult stampResult = await userManager.UpdateSecurityStampAsync(user);
        return !stampResult.Succeeded
            ? IdentityError(stampResult)
            : Results.Ok(await ToResponseAsync(user, userManager));
    }

    private static async Task<IdentityResult> EnsureRolesAsync(RoleManager<IdentityRole<Guid>> roleManager)
    {
        foreach (string roleName in new[] { CratebaseRoles.Admin, CratebaseRoles.User })
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

    private static async Task<AdminUserResponse> ToResponseAsync(CratebaseUser user, UserManager<CratebaseUser> userManager)
    {
        IReadOnlyList<string> roles = [.. await userManager.GetRolesAsync(user)];

        return new AdminUserResponse(user.Id, user.Email ?? string.Empty, roles, RequireDefaultCollectionId(user), user.IsDisabled);
    }

    private static Guid RequireDefaultCollectionId(CratebaseUser user)
    {
        return user.DefaultCollectionId?.Value ?? throw new InvalidOperationException("Default collection is not initialized");
    }

    private static IResult IdentityError(IdentityResult result)
    {
        string code = result.Errors.FirstOrDefault()?.Code ?? "auth.identity_error";

        return EndpointErrors.BadRequest($"auth.{code}", "Identity request is invalid");
    }
}
