using System.Security.Claims;
using System.Threading.RateLimiting;
using DiscWeave.Api.Http;
using Microsoft.AspNetCore.HttpOverrides;

namespace DiscWeave.Api.Hosting;

public static class HostedSecurityServiceCollectionExtensions
{
    public static IServiceCollection AddHostedSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        _ = services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders =
                ForwardedHeaders.XForwardedFor |
                ForwardedHeaders.XForwardedProto |
                ForwardedHeaders.XForwardedHost;
            options.ForwardLimit = 1;
            AddKnownForwardedHeaderPeers(options, configuration);
        });
        _ = services.AddHsts(options =>
        {
            options.MaxAge = TimeSpan.FromDays(180);
            options.IncludeSubDomains = true;
        });
        _ = services.AddRateLimiter(options =>
        {
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.HttpContext.Response.WriteAsJsonAsync(
                    new ErrorResponse("rate_limit.exceeded", "Rate limit exceeded"),
                    cancellationToken);
            };
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(CreateLimiter);
        });

        return services;
    }

    private static void AddKnownForwardedHeaderPeers(
        ForwardedHeadersOptions options,
        IConfiguration configuration)
    {
        foreach (string value in ConfiguredValues(configuration, "HostedSecurity:ForwardedHeaders:KnownNetworks"))
        {
            options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(value));
        }

        foreach (string value in ConfiguredValues(configuration, "HostedSecurity:ForwardedHeaders:KnownProxies"))
        {
            options.KnownProxies.Add(System.Net.IPAddress.Parse(value));
        }
    }

    private static IEnumerable<string> ConfiguredValues(IConfiguration configuration, string key)
    {
        return configuration.GetSection(key).GetChildren()
            .Select(section => section.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!);
    }

    private static RateLimitPartition<string> CreateLimiter(HttpContext context)
    {
        string policy = PolicyFor(context.Request);
        return policy switch
        {
            HostedSecurityRateLimitPolicies.Auth => FixedWindow(policy, ClientKey(context), 10, TimeSpan.FromMinutes(1)),
            HostedSecurityRateLimitPolicies.Lifecycle => FixedWindow(policy, ActorKey(context), 20, TimeSpan.FromMinutes(1)),
            HostedSecurityRateLimitPolicies.DesktopImport => FixedWindow(policy, ActorKey(context), 12, TimeSpan.FromHours(1)),
            HostedSecurityRateLimitPolicies.Export => FixedWindow(policy, ActorKey(context), 10, TimeSpan.FromHours(1)),
            _ => RateLimitPartition.GetNoLimiter(HostedSecurityRateLimitPolicies.Unlimited)
        };
    }

    private static RateLimitPartition<string> FixedWindow(
        string policy,
        string key,
        int permitLimit,
        TimeSpan window)
    {
        return RateLimitPartition.GetFixedWindowLimiter(
            $"{policy}:{key}",
            _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = permitLimit,
                QueueLimit = 0,
                Window = window
            });
    }

    private static string PolicyFor(HttpRequest request)
    {
        bool isAuthRequest = HttpMethods.IsPost(request.Method) &&
            (request.Path.Equals("/api/auth/login", StringComparison.OrdinalIgnoreCase) ||
                request.Path.Equals("/api/auth/register", StringComparison.OrdinalIgnoreCase));
        bool isLifecycleRequest = request.Path.StartsWithSegments("/api/admin/invites", StringComparison.OrdinalIgnoreCase) ||
            request.Path.StartsWithSegments("/api/admin/users", StringComparison.OrdinalIgnoreCase) ||
            request.Path.Equals("/api/auth/password", StringComparison.OrdinalIgnoreCase);
        bool isDesktopImportRequest = HttpMethods.IsPost(request.Method) &&
            request.Path.Equals("/api/imports/desktop-folder-scans", StringComparison.OrdinalIgnoreCase);
        bool isExportRequest = request.Path.StartsWithSegments("/api/exports", StringComparison.OrdinalIgnoreCase);

        return request switch
        {
            _ when isAuthRequest => HostedSecurityRateLimitPolicies.Auth,
            _ when isLifecycleRequest => HostedSecurityRateLimitPolicies.Lifecycle,
            _ when isDesktopImportRequest => HostedSecurityRateLimitPolicies.DesktopImport,
            _ when isExportRequest => HostedSecurityRateLimitPolicies.Export,
            _ => HostedSecurityRateLimitPolicies.Unlimited
        };
    }

    private static string ActorKey(HttpContext context)
    {
        string? userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return !string.IsNullOrWhiteSpace(userId) ? $"user:{userId}" : ClientKey(context);
    }

    private static string ClientKey(HttpContext context)
    {
        return $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
    }
}
