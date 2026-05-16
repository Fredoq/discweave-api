using System.Security.Claims;
using Cratebase.Api;
using Cratebase.Api.Auth;
using Cratebase.Api.Features;
using Cratebase.Api.Features.Imports;
using Cratebase.Api.Http;
using Cratebase.Application;
using Cratebase.Application.Security;
using Cratebase.Infrastructure.Identity;
using Cratebase.Infrastructure;
using Cratebase.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddCratebaseApplication();
builder.Services.AddCratebaseInfrastructure(builder.Configuration);
builder.Services.AddScoped<ReleaseImportScanService>();
builder.Services.AddScoped<ReleaseImportConfirmationService>();
builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddIdentityCookies();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "Cratebase.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.LoginPath = "/api/auth/login";
    options.AccessDeniedPath = "/api/auth/forbidden";
    options.ReturnUrlParameter = CookieAuthenticationDefaults.ReturnUrlParameter;
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.Events.OnRedirectToLogin = context => WriteErrorAsync(
        context.Response,
        StatusCodes.Status401Unauthorized,
        "auth.unauthenticated",
        "User is not authenticated");
    options.Events.OnRedirectToAccessDenied = context => WriteErrorAsync(
        context.Response,
        StatusCodes.Status403Forbidden,
        "auth.forbidden",
        "User is not authorized for this action");
    options.Events.OnValidatePrincipal = async context =>
    {
        await SecurityStampValidator.ValidatePrincipalAsync(context);
        if (context.Principal?.Identity?.IsAuthenticated != true)
        {
            return;
        }

        string? userId = context.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userId, out Guid parsedUserId))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);

            return;
        }

        UserManager<CratebaseUser> userManager = context.HttpContext.RequestServices.GetRequiredService<UserManager<CratebaseUser>>();
        CratebaseUser? user = await userManager.FindByIdAsync(parsedUserId.ToString());
        if (user is null || user.IsDisabled || user.DefaultCollectionId is null)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        }
    };
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddScoped<ICurrentCollection, HttpCurrentCollection>();
builder.Services.AddScoped(provider =>
{
    DbContextOptions<CratebaseDbContext> options = provider.GetRequiredService<DbContextOptions<CratebaseDbContext>>();
    ClaimsPrincipal? user = provider.GetRequiredService<IHttpContextAccessor>().HttpContext?.User;

    return HasValidCollectionScope(user)
        ? new CratebaseDbContext(options, provider.GetRequiredService<ICurrentCollection>())
        : new CratebaseDbContext(options);
});
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(CratebaseAuthorizationPolicies.Admin, policy => policy.RequireRole(CratebaseRoles.Admin))
    .AddPolicy(CratebaseAuthorizationPolicies.CollectionMember, policy =>
    {
        _ = policy.RequireAuthenticatedUser();
        _ = policy.RequireAssertion(context => HasValidCollectionScope(context.User));
    });

WebApplication app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapCratebaseEndpoints();

app.MapGet("/health", () =>
{
    HealthResponse response = new()
    {
        Service = "cratebase-api",
        Status = "ok"
    };

    return Results.Ok(response);
})
.WithName("GetHealth");

await app.RunAsync();
return;

static bool HasValidCollectionScope(ClaimsPrincipal? user)
{
    string? collectionId = user?.FindFirstValue(CratebaseClaimTypes.DefaultCollectionId);

    return user?.Identity?.IsAuthenticated == true &&
        Guid.TryParse(collectionId, out Guid parsedCollectionId) &&
        parsedCollectionId != Guid.Empty;
}

static Task WriteErrorAsync(HttpResponse response, int statusCode, string code, string message)
{
    response.StatusCode = statusCode;

    return response.WriteAsJsonAsync(new ErrorResponse(code, message));
}
