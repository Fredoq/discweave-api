using System.Security.Claims;
using DiscWeave.Api;
using DiscWeave.Api.Auth;
using DiscWeave.Api.Features;
using DiscWeave.Api.Features.Imports;
using DiscWeave.Api.Hosting;
using DiscWeave.Api.Http;
using DiscWeave.Application;
using DiscWeave.Application.Security;
using DiscWeave.Infrastructure.Identity;
using DiscWeave.Infrastructure;
using DiscWeave.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddDiscWeaveApplication();
builder.Services.AddDiscWeaveInfrastructure(builder.Configuration);
builder.Services.AddHostedSecurity(builder.Configuration);
builder.Services.AddScoped<ReleaseImportConfirmationService>();
builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddIdentityCookies();
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    options.ValidationInterval = TimeSpan.Zero;
});
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "DiscWeave.Auth";
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

        UserManager<DiscWeaveUser> userManager = context.HttpContext.RequestServices.GetRequiredService<UserManager<DiscWeaveUser>>();
        DiscWeaveUser? user = await userManager.FindByIdAsync(parsedUserId.ToString());
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
    DbContextOptions<DiscWeaveDbContext> options = provider.GetRequiredService<DbContextOptions<DiscWeaveDbContext>>();
    ClaimsPrincipal? user = provider.GetRequiredService<IHttpContextAccessor>().HttpContext?.User;

    return HasValidCollectionScope(user)
        ? new DiscWeaveDbContext(options, provider.GetRequiredService<ICurrentCollection>())
        : new DiscWeaveDbContext(options);
});
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(DiscWeaveAuthorizationPolicies.Admin, policy => policy.RequireRole(DiscWeaveRoles.Admin))
    .AddPolicy(DiscWeaveAuthorizationPolicies.CollectionMember, policy =>
    {
        _ = policy.RequireAuthenticatedUser();
        _ = policy.RequireAssertion(context => HasValidCollectionScope(context.User));
    });

WebApplication app = builder.Build();

app.UseHostedSecurity();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapDiscWeaveEndpoints();

app.MapGet("/health", () =>
{
    HealthResponse response = new()
    {
        Service = "discweave-api",
        Status = "ok"
    };

    return Results.Ok(response);
})
.WithName("GetHealth");

await app.RunAsync();
return;

static bool HasValidCollectionScope(ClaimsPrincipal? user)
{
    string? collectionId = user?.FindFirstValue(DiscWeaveClaimTypes.DefaultCollectionId);

    return user?.Identity?.IsAuthenticated == true &&
        Guid.TryParse(collectionId, out Guid parsedCollectionId) &&
        parsedCollectionId != Guid.Empty;
}

static Task WriteErrorAsync(HttpResponse response, int statusCode, string code, string message)
{
    response.StatusCode = statusCode;

    return response.WriteAsJsonAsync(new ErrorResponse(code, message));
}
