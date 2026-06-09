using DiscWeave.Api.Http;
using DiscWeave.Application.ExternalMetadata;

namespace DiscWeave.Api.Features.ExternalMetadata;

public static class ExternalMetadataEndpointErrors
{
    public static IResult ToHttpResult(ExternalMetadataError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        IResult result = error.Kind switch
        {
            ExternalMetadataErrorKind.Disabled => new ExternalMetadataErrorResult(error, StatusCodes.Status503ServiceUnavailable),
            ExternalMetadataErrorKind.NotConfigured => new ExternalMetadataErrorResult(error, StatusCodes.Status503ServiceUnavailable),
            ExternalMetadataErrorKind.Unauthorized => new ExternalMetadataErrorResult(error, StatusCodes.Status502BadGateway),
            ExternalMetadataErrorKind.RateLimited => RateLimited(error),
            ExternalMetadataErrorKind.Timeout => new ExternalMetadataErrorResult(error, StatusCodes.Status504GatewayTimeout),
            ExternalMetadataErrorKind.Unavailable => new ExternalMetadataErrorResult(error, StatusCodes.Status503ServiceUnavailable),
            ExternalMetadataErrorKind.InvalidResponse => new ExternalMetadataErrorResult(error, StatusCodes.Status502BadGateway),
            _ => new ExternalMetadataErrorResult(
                new ExternalMetadataError(
                    ExternalMetadataErrorKind.Unavailable,
                    "external_metadata.unavailable",
                    "External metadata provider is unavailable"),
                StatusCodes.Status503ServiceUnavailable)
        };

        return result;
    }

    private static RateLimitedResult RateLimited(ExternalMetadataError error)
    {
        return new RateLimitedResult(error);
    }

    private sealed class ExternalMetadataErrorResult(ExternalMetadataError error, int statusCode) : IResult
    {
        public async Task ExecuteAsync(HttpContext httpContext)
        {
            ArgumentNullException.ThrowIfNull(httpContext);

            httpContext.Response.StatusCode = statusCode;
            await httpContext.Response.WriteAsJsonAsync(new ErrorResponse(error.Code, error.Message));
        }
    }

    private sealed class RateLimitedResult(ExternalMetadataError error) : IResult
    {
        public async Task ExecuteAsync(HttpContext httpContext)
        {
            ArgumentNullException.ThrowIfNull(httpContext);

            if (error.RetryAfter.HasValue)
            {
                httpContext.Response.Headers.RetryAfter = Math.Ceiling(error.RetryAfter.Value.TotalSeconds)
                    .ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await httpContext.Response.WriteAsJsonAsync(new ErrorResponse(error.Code, error.Message));
        }
    }
}
