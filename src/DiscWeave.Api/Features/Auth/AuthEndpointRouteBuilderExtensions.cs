using DiscWeave.Api.Http;
using DiscWeave.Api.Features.Invites;
using DiscWeave.Infrastructure.Identity;
using DiscWeave.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DiscWeave.Api.Features.Auth;

public static partial class AuthEndpointRouteBuilderExtensions
{
    private const long FirstUserBootstrapLockKey = 807719852889734940;

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder group = endpoints.MapGroup("/api/auth")
            .WithTags("Auth")
            .AllowAnonymous();

        _ = group.MapPost("/register", RegisterAsync).WithName("RegisterFirstUser");
        _ = group.MapPost("/login", LoginAsync).WithName("Login");
        _ = group.MapGet("/session", GetSessionAsync).WithName("GetAuthSession");
        _ = group.MapPost("/logout", LogoutAsync).RequireAuthorization().WithName("Logout");
        _ = group.MapGet("/me", GetMeAsync).RequireAuthorization().WithName("GetCurrentUser");
        _ = group.MapPost("/password", ChangePasswordAsync).RequireAuthorization().WithName("ChangeCurrentUserPassword");

        return endpoints;
    }

    private static async Task<IResult> RegisterAsync(
        AuthRequest request,
        UserManager<DiscWeaveUser> userManager,
        SignInManager<DiscWeaveUser> signInManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        DiscWeaveDbContext context,
        CancellationToken cancellationToken)
    {
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await context.Database.BeginTransactionAsync(cancellationToken);
        _ = await context.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({FirstUserBootstrapLockKey})", cancellationToken);

        if (await userManager.Users.AnyAsync(cancellationToken))
        {
            return await RegisterInvitedUserAsync(request, userManager, signInManager, roleManager, context, transaction, cancellationToken);
        }

        IdentityResult rolesResult = await UserProvisioning.EnsureRolesAsync(roleManager);
        if (!rolesResult.Succeeded)
        {
            return IdentityError(rolesResult);
        }

        (IdentityResult createResult, DiscWeaveUser? user) = await UserProvisioning.CreateUserWithCollectionAsync(
            request.Email,
            request.Password,
            [DiscWeaveRoles.Admin, DiscWeaveRoles.User],
            userManager,
            context,
            cancellationToken);
        if (!createResult.Succeeded || user is null)
        {
            return IdentityError(createResult);
        }

        await transaction.CommitAsync(cancellationToken);
        await signInManager.SignInAsync(user, isPersistent: true);

        return Results.Created("/api/auth/me", await ToResponseAsync(user, userManager));
    }

    private static async Task<IResult> RegisterInvitedUserAsync(
        AuthRequest request,
        UserManager<DiscWeaveUser> userManager,
        SignInManager<DiscWeaveUser> signInManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        DiscWeaveDbContext context,
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.InviteCode))
        {
            return EndpointErrors.BadRequest("auth.invite_required", "Invite code is required");
        }

        string codeHash = InviteCodes.Hash(request.InviteCode);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Invite? invite = await context.Invites
            .FromSqlInterpolated($"""
                SELECT *
                FROM invites
                WHERE code_hash = {codeHash}
                FOR UPDATE
                """)
            .SingleOrDefaultAsync(cancellationToken);
        if (invite is null || !invite.IsAvailable(now))
        {
            return EndpointErrors.BadRequest("auth.invite_unavailable", "Invite code is unavailable");
        }

        IdentityResult rolesResult = await UserProvisioning.EnsureRolesAsync(roleManager);
        if (!rolesResult.Succeeded)
        {
            return IdentityError(rolesResult);
        }

        (IdentityResult createResult, DiscWeaveUser? user) = await UserProvisioning.CreateUserWithCollectionAsync(
            request.Email,
            request.Password,
            [DiscWeaveRoles.User],
            userManager,
            context,
            cancellationToken);
        if (!createResult.Succeeded || user is null)
        {
            return IdentityError(createResult);
        }

        invite.Redeem(user.Id, user.Email ?? request.Email, now);
        _ = await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await signInManager.SignInAsync(user, isPersistent: true);

        return Results.Created("/api/auth/me", await ToResponseAsync(user, userManager));
    }

    private static async Task<IResult> LoginAsync(
        AuthRequest request,
        UserManager<DiscWeaveUser> userManager,
        SignInManager<DiscWeaveUser> signInManager)
    {
        DiscWeaveUser? user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user?.DefaultCollectionId is null)
        {
            return EndpointErrors.Unauthorized("auth.invalid_credentials", "Email or password is invalid");
        }

        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            return EndpointErrors.Unauthorized("auth.invalid_credentials", "Email or password is invalid");
        }

        if (user.IsDisabled)
        {
            return EndpointErrors.Unauthorized("auth.user_disabled", "User account is disabled");
        }

        await signInManager.SignInAsync(user, isPersistent: true);

        return Results.Ok(await ToResponseAsync(user, userManager));
    }

    private static async Task<IResult> GetSessionAsync(
        UserManager<DiscWeaveUser> userManager,
        SignInManager<DiscWeaveUser> signInManager,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (httpContext.User.Identity?.IsAuthenticated == true)
        {
            DiscWeaveUser? user = await userManager.GetUserAsync(httpContext.User);
            if (user is not null && !user.IsDisabled && user.DefaultCollectionId is not null)
            {
                return Results.Ok(await ToSessionResponseAsync(user, userManager));
            }

            await signInManager.SignOutAsync();
        }

        bool bootstrapRequired = !await userManager.Users.AnyAsync(cancellationToken);

        return Results.Ok(new AuthSessionResponse(false, bootstrapRequired, null, []));
    }

    private static async Task<IResult> LogoutAsync(SignInManager<DiscWeaveUser> signInManager)
    {
        await signInManager.SignOutAsync();

        return Results.NoContent();
    }

    private static async Task<IResult> GetMeAsync(UserManager<DiscWeaveUser> userManager, HttpContext httpContext)
    {
        DiscWeaveUser? user = await userManager.GetUserAsync(httpContext.User);
        return user is null || user.IsDisabled || user.DefaultCollectionId is null
            ? EndpointErrors.Unauthorized("auth.unauthenticated", "User is not authenticated")
            : Results.Ok(await ToResponseAsync(user, userManager));
    }

    private static async Task<IResult> ChangePasswordAsync(
        ChangePasswordRequest request,
        UserManager<DiscWeaveUser> userManager,
        HttpContext httpContext)
    {
        DiscWeaveUser? user = await userManager.GetUserAsync(httpContext.User);
        if (user is null || user.IsDisabled || user.DefaultCollectionId is null)
        {
            return EndpointErrors.Unauthorized("auth.unauthenticated", "User is not authenticated");
        }

        IdentityResult result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        return result.Succeeded ? Results.NoContent() : IdentityError(result);
    }

    private static async Task<AuthResponse> ToResponseAsync(DiscWeaveUser user, UserManager<DiscWeaveUser> userManager)
    {
        IReadOnlyList<string> roles = await GetRolesAsync(user, userManager);

        return new AuthResponse(true, user.Email ?? string.Empty, roles);
    }

    private static async Task<AuthSessionResponse> ToSessionResponseAsync(DiscWeaveUser user, UserManager<DiscWeaveUser> userManager)
    {
        IReadOnlyList<string> roles = await GetRolesAsync(user, userManager);

        return new AuthSessionResponse(true, false, user.Email ?? string.Empty, roles);
    }

    private static async Task<IReadOnlyList<string>> GetRolesAsync(DiscWeaveUser user, UserManager<DiscWeaveUser> userManager)
    {
        return [.. (await userManager.GetRolesAsync(user)).Order(StringComparer.Ordinal)];
    }

    private static IResult IdentityError(IdentityResult result)
    {
        string code = result.Errors.FirstOrDefault()?.Code ?? "auth.identity_error";

        return EndpointErrors.BadRequest($"auth.{code}", "Identity request is invalid");
    }
}
