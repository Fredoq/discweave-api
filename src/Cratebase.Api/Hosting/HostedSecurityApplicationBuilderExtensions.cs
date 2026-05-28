using Cratebase.Api.Http;

namespace Cratebase.Api.Hosting;

public static class HostedSecurityApplicationBuilderExtensions
{
    public static WebApplication UseHostedSecurity(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        _ = app.UseForwardedHeaders();
        if (!app.Environment.IsDevelopment())
        {
            _ = app.UseExceptionHandler(errorApp =>
            {
                errorApp.Run(context =>
                {
                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    return context.Response.WriteAsJsonAsync(
                        new ErrorResponse("server.error", "An internal server error occurred"));
                });
            });
            _ = app.UseHsts();
        }

        _ = app.Use(async (context, next) =>
        {
            AddSecurityHeaders(context.Response);
            await next(context);
        });
        _ = app.Use(ValidateUnsafeOriginAsync);

        return app;
    }

    private static void AddSecurityHeaders(HttpResponse response)
    {
        response.Headers.XContentTypeOptions = "nosniff";
        response.Headers.XFrameOptions = "DENY";
        response.Headers["Referrer-Policy"] = "no-referrer";
    }

    private static async Task ValidateUnsafeOriginAsync(HttpContext context, Func<Task> next)
    {
        IHeaderDictionary headers = context.Request.Headers;
        if (IsUnsafeMethod(context.Request.Method) &&
            headers.Origin.Count > 0 &&
            (headers.Origin.Count != 1 || !OriginIsAllowed(context.Request, headers.Origin.ToString())))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new ErrorResponse("security.origin_invalid", "Request origin is not allowed"));
            return;
        }

        await next();
    }

    private static bool IsUnsafeMethod(string method)
    {
        return HttpMethods.IsPost(method) ||
            HttpMethods.IsPut(method) ||
            HttpMethods.IsPatch(method) ||
            HttpMethods.IsDelete(method);
    }

    private static bool OriginIsAllowed(HttpRequest request, string origin)
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out Uri? originUri))
        {
            return false;
        }

        Uri requestOrigin = new UriBuilder(request.Scheme, request.Host.Host, request.Host.Port ?? -1).Uri;
        return SameOrigin(originUri, requestOrigin) || IsLoopbackHttpOrigin(originUri);
    }

    private static bool SameOrigin(Uri left, Uri right)
    {
        return string.Equals(left.Scheme, right.Scheme, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(left.Host, right.Host, StringComparison.OrdinalIgnoreCase) &&
            left.Port == right.Port;
    }

    private static bool IsLoopbackHttpOrigin(Uri origin)
    {
        return (origin.Scheme == Uri.UriSchemeHttp || origin.Scheme == Uri.UriSchemeHttps) &&
            Uri.CheckHostName(origin.Host) != UriHostNameType.Unknown &&
            (string.Equals(origin.Host, "localhost", StringComparison.OrdinalIgnoreCase) ||
                IPAddressIsLoopback(origin.Host));
    }

    private static bool IPAddressIsLoopback(string host)
    {
        return System.Net.IPAddress.TryParse(host, out System.Net.IPAddress? address) &&
            System.Net.IPAddress.IsLoopback(address);
    }
}
