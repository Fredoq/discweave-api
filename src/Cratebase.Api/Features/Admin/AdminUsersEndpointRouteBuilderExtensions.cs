using Cratebase.Api.Auth;
using Cratebase.Api.Features.Auth;
using Cratebase.Api.Http;
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
        _ = group.MapPost("/{userId:guid}/password", SetPasswordAsync).WithName("SetUserPassword");

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

        IdentityResult rolesResult = await UserProvisioning.EnsureRolesAsync(roleManager);
        if (!rolesResult.Succeeded)
        {
            return IdentityError(rolesResult);
        }

        (IdentityResult createResult, CratebaseUser? user) = await UserProvisioning.CreateUserWithCollectionAsync(
            request.Email,
            request.Password,
            [CratebaseRoles.User],
            userManager,
            context,
            cancellationToken);
        if (!createResult.Succeeded || user is null)
        {
            return IdentityError(createResult);
        }

        await transaction.CommitAsync(cancellationToken);

        return Results.Created($"/api/admin/users/{user.Id}", await ToResponseAsync(user, userManager));
    }

    private static async Task<IResult> UpdateStatusAsync(
        Guid userId,
        UpdateUserStatusRequest request,
        UserManager<CratebaseUser> userManager,
        CratebaseDbContext context,
        CancellationToken cancellationToken)
    {
        bool isAdmin = await IsAdminAsync(userId, context, cancellationToken);

        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await context.Database.BeginTransactionAsync(cancellationToken);

        CratebaseUser? user = await LockUserForStatusUpdateAsync(
            userId,
            request.IsDisabled && isAdmin,
            context,
            cancellationToken);
        if (user is null)
        {
            return EndpointErrors.NotFound("user.not_found", "User was not found");
        }

        if (request.IsDisabled && !user.IsDisabled && isAdmin && await IsLastActiveAdminAsync(context, cancellationToken))
        {
            return EndpointErrors.Conflict("user.last_admin", "The last active admin cannot be disabled");
        }

        user.IsDisabled = request.IsDisabled;
        IdentityResult result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return IdentityError(result);
        }

        IdentityResult stampResult = await userManager.UpdateSecurityStampAsync(user);
        if (!stampResult.Succeeded)
        {
            return IdentityError(stampResult);
        }

        await transaction.CommitAsync(cancellationToken);

        return Results.Ok(await ToResponseAsync(user, userManager));
    }

    private static async Task<IResult> SetPasswordAsync(
        Guid userId,
        AdminPasswordRequest request,
        UserManager<CratebaseUser> userManager)
    {
        CratebaseUser? user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return EndpointErrors.NotFound("user.not_found", "User was not found");
        }

        string resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
        IdentityResult resetResult = await userManager.ResetPasswordAsync(user, resetToken, request.TemporaryPassword);
        if (!resetResult.Succeeded)
        {
            return IdentityError(resetResult);
        }

        IdentityResult stampResult = await userManager.UpdateSecurityStampAsync(user);
        return stampResult.Succeeded ? Results.NoContent() : IdentityError(stampResult);
    }

    private static async Task<CratebaseUser?> LockUserForStatusUpdateAsync(
        Guid userId,
        bool lockActiveAdmins,
        CratebaseDbContext context,
        CancellationToken cancellationToken)
    {
        if (lockActiveAdmins)
        {
            CratebaseUser[] activeAdmins = await context.Users
                .FromSqlInterpolated($"""
                    SELECT u.*
                    FROM "AspNetUsers" AS u
                    INNER JOIN "AspNetUserRoles" AS ur ON u."Id" = ur."UserId"
                    INNER JOIN "AspNetRoles" AS r ON ur."RoleId" = r."Id"
                    WHERE r."Name" = {CratebaseRoles.Admin} AND u."IsDisabled" = FALSE
                    ORDER BY u."Id"
                    FOR UPDATE
                    """)
                .ToArrayAsync(cancellationToken);
            CratebaseUser? activeAdmin = activeAdmins.SingleOrDefault(admin => admin.Id == userId);
            if (activeAdmin is not null)
            {
                return activeAdmin;
            }
        }

        return await context.Users
            .FromSqlInterpolated($"""
                SELECT *
                FROM "AspNetUsers"
                WHERE "Id" = {userId}
                FOR UPDATE
                """)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private static async Task<bool> IsLastActiveAdminAsync(CratebaseDbContext context, CancellationToken cancellationToken)
    {
        int activeAdminCount = await context.Users
            .CountAsync(user =>
                !user.IsDisabled &&
                context.UserRoles.Any(userRole =>
                    userRole.UserId == user.Id &&
                    context.Roles.Any(role => role.Id == userRole.RoleId && role.Name == CratebaseRoles.Admin)),
                cancellationToken);

        return activeAdminCount <= 1;
    }

    private static async Task<bool> IsAdminAsync(Guid userId, CratebaseDbContext context, CancellationToken cancellationToken)
    {
        return await context.UserRoles.AnyAsync(
            userRole =>
                userRole.UserId == userId &&
                context.Roles.Any(role => role.Id == userRole.RoleId && role.Name == CratebaseRoles.Admin),
            cancellationToken);
    }


    private static async Task<AdminUserResponse> ToResponseAsync(CratebaseUser user, UserManager<CratebaseUser> userManager)
    {
        IReadOnlyList<string> roles = [.. (await userManager.GetRolesAsync(user)).Order(StringComparer.Ordinal)];

        return new AdminUserResponse(user.Id, user.Email ?? string.Empty, roles, user.IsDisabled);
    }

    private static IResult IdentityError(IdentityResult result)
    {
        string code = result.Errors.FirstOrDefault()?.Code ?? "auth.identity_error";

        return EndpointErrors.BadRequest($"auth.{code}", "Identity request is invalid");
    }
}
