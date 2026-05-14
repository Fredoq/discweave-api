using Cratebase.Api.Http;
using Cratebase.Domain.Collection;
using Cratebase.Domain.Ratings;
using Cratebase.Domain.Settings;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Infrastructure.Identity;
using Cratebase.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Api.Features.Auth;

public static class AuthEndpointRouteBuilderExtensions
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

        return endpoints;
    }

    private static async Task<IResult> RegisterAsync(
        AuthRequest request,
        UserManager<CratebaseUser> userManager,
        SignInManager<CratebaseUser> signInManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        CratebaseDbContext context,
        CancellationToken cancellationToken)
    {
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await context.Database.BeginTransactionAsync(cancellationToken);
        _ = await context.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({FirstUserBootstrapLockKey})", cancellationToken);

        if (await userManager.Users.AnyAsync(cancellationToken))
        {
            return EndpointErrors.Conflict("auth.registration_closed", "Public registration is closed");
        }

        IdentityResult rolesResult = await EnsureRolesAsync(roleManager);
        if (!rolesResult.Succeeded)
        {
            return IdentityError(rolesResult);
        }

        var collectionId = CollectionId.New();
        CratebaseUser user = CreateUser(request.Email);
        IdentityResult createResult = await userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            return IdentityError(createResult);
        }

        IdentityResult roleResult = await userManager.AddToRolesAsync(user, [CratebaseRoles.Admin, CratebaseRoles.User]);
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
        await signInManager.SignInAsync(user, isPersistent: true);

        return Results.Created("/api/auth/me", await ToResponseAsync(user, userManager));
    }

    private static async Task<IResult> LoginAsync(
        AuthRequest request,
        UserManager<CratebaseUser> userManager,
        SignInManager<CratebaseUser> signInManager)
    {
        CratebaseUser? user = await userManager.FindByEmailAsync(request.Email.Trim());
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
        UserManager<CratebaseUser> userManager,
        SignInManager<CratebaseUser> signInManager,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (httpContext.User.Identity?.IsAuthenticated == true)
        {
            CratebaseUser? user = await userManager.GetUserAsync(httpContext.User);
            if (user is not null && !user.IsDisabled && user.DefaultCollectionId is not null)
            {
                return Results.Ok(await ToSessionResponseAsync(user, userManager));
            }

            await signInManager.SignOutAsync();
        }

        bool bootstrapRequired = !await userManager.Users.AnyAsync(cancellationToken);

        return Results.Ok(new AuthSessionResponse(false, bootstrapRequired, null, []));
    }

    private static async Task<IResult> LogoutAsync(SignInManager<CratebaseUser> signInManager)
    {
        await signInManager.SignOutAsync();

        return Results.NoContent();
    }

    private static async Task<IResult> GetMeAsync(UserManager<CratebaseUser> userManager, HttpContext httpContext)
    {
        CratebaseUser? user = await userManager.GetUserAsync(httpContext.User);
        return user is null || user.IsDisabled || user.DefaultCollectionId is null
            ? EndpointErrors.Unauthorized("auth.unauthenticated", "User is not authenticated")
            : Results.Ok(await ToResponseAsync(user, userManager));
    }

    private static CratebaseUser CreateUser(string email)
    {
        string normalizedEmail = email.Trim();

        return new CratebaseUser
        {
            Id = Guid.CreateVersion7(),
            Email = normalizedEmail,
            UserName = normalizedEmail
        };
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

    private static async Task<AuthResponse> ToResponseAsync(CratebaseUser user, UserManager<CratebaseUser> userManager)
    {
        IReadOnlyList<string> roles = await GetRolesAsync(user, userManager);

        return new AuthResponse(true, user.Email ?? string.Empty, roles);
    }

    private static async Task<AuthSessionResponse> ToSessionResponseAsync(CratebaseUser user, UserManager<CratebaseUser> userManager)
    {
        IReadOnlyList<string> roles = await GetRolesAsync(user, userManager);

        return new AuthSessionResponse(true, false, user.Email ?? string.Empty, roles);
    }

    private static async Task<IReadOnlyList<string>> GetRolesAsync(CratebaseUser user, UserManager<CratebaseUser> userManager)
    {
        return [.. (await userManager.GetRolesAsync(user)).Order(StringComparer.Ordinal)];
    }

    private static IResult IdentityError(IdentityResult result)
    {
        string code = result.Errors.FirstOrDefault()?.Code ?? "auth.identity_error";

        return EndpointErrors.BadRequest($"auth.{code}", "Identity request is invalid");
    }
}
