using Cratebase.Api.Http;
using Cratebase.Domain.Collection;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Infrastructure.Identity;
using Cratebase.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Api.Features.Auth;

public static class AuthEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder group = endpoints.MapGroup("/api/auth")
            .WithTags("Auth")
            .AllowAnonymous();

        _ = group.MapPost("/register", RegisterAsync).WithName("RegisterFirstUser");
        _ = group.MapPost("/login", LoginAsync).WithName("Login");
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
        if (await userManager.Users.AnyAsync(cancellationToken))
        {
            return EndpointErrors.Conflict("auth.registration_closed", "Public registration is closed");
        }

        CratebaseUser user = CreateUser(request.Email, CollectionId.New());
        IdentityResult createResult = await userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            return IdentityError(createResult);
        }

        await EnsureRolesAsync(roleManager);
        IdentityResult roleResult = await userManager.AddToRolesAsync(user, [CratebaseRoles.Admin, CratebaseRoles.User]);
        if (!roleResult.Succeeded)
        {
            return IdentityError(roleResult);
        }

        _ = context.MusicCollections.Add(MusicCollection.Create(new CollectionId(user.DefaultCollectionId), new UserId(user.Id), "Main collection"));
        _ = await context.SaveChangesAsync(cancellationToken);

        await signInManager.SignInAsync(user, isPersistent: true);

        return Results.Created("/api/auth/me", await ToResponseAsync(user, userManager));
    }

    private static async Task<IResult> LoginAsync(
        AuthRequest request,
        UserManager<CratebaseUser> userManager,
        SignInManager<CratebaseUser> signInManager)
    {
        CratebaseUser? user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null || user.IsDisabled || !await userManager.CheckPasswordAsync(user, request.Password))
        {
            return EndpointErrors.Unauthorized("auth.invalid_credentials", "Email or password is invalid");
        }

        await signInManager.SignInAsync(user, isPersistent: true);

        return Results.Ok(await ToResponseAsync(user, userManager));
    }

    private static async Task<IResult> LogoutAsync(SignInManager<CratebaseUser> signInManager)
    {
        await signInManager.SignOutAsync();

        return Results.NoContent();
    }

    private static async Task<IResult> GetMeAsync(UserManager<CratebaseUser> userManager, HttpContext httpContext)
    {
        CratebaseUser? user = await userManager.GetUserAsync(httpContext.User);
        return user is null || user.IsDisabled
            ? EndpointErrors.Unauthorized("auth.unauthenticated", "User is not authenticated")
            : Results.Ok(await ToResponseAsync(user, userManager));
    }

    private static CratebaseUser CreateUser(string email, CollectionId collectionId)
    {
        string normalizedEmail = email.Trim();

        return new CratebaseUser
        {
            Id = Guid.CreateVersion7(),
            Email = normalizedEmail,
            UserName = normalizedEmail,
            DefaultCollectionId = collectionId.Value
        };
    }

    private static async Task EnsureRolesAsync(RoleManager<IdentityRole<Guid>> roleManager)
    {
        foreach (string roleName in new[] { CratebaseRoles.Admin, CratebaseRoles.User })
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                _ = await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
            }
        }
    }

    private static async Task<AuthResponse> ToResponseAsync(CratebaseUser user, UserManager<CratebaseUser> userManager)
    {
        IReadOnlyList<string> roles = [.. await userManager.GetRolesAsync(user)];

        return new AuthResponse(user.Id, user.Email ?? string.Empty, roles, user.DefaultCollectionId);
    }

    private static IResult IdentityError(IdentityResult result)
    {
        string code = result.Errors.FirstOrDefault()?.Code ?? "auth.identity_error";

        return EndpointErrors.BadRequest($"auth.{code}", "Identity request is invalid");
    }
}
