using System.Net;
using DiscWeave.Application.ExternalMetadata;

namespace DiscWeave.Infrastructure.ExternalMetadata.Discogs;

public sealed partial class DiscogsExternalMetadataProvider
{
    private static ExternalMetadataError MapFailure(HttpResponseMessage response)
    {
#pragma warning disable IDE0066
#pragma warning disable IDE0010
        switch (response.StatusCode)
        {
            case HttpStatusCode.Unauthorized:
            case HttpStatusCode.Forbidden:
                return Unauthorized();
            case HttpStatusCode.TooManyRequests:
                return RateLimited(response.Headers.RetryAfter?.Delta);
            case HttpStatusCode.RequestTimeout:
            case HttpStatusCode.GatewayTimeout:
                return Timeout();
            default:
                return (int)response.StatusCode >= 500
                    ? Unavailable()
                    : InvalidResponse();
        }
#pragma warning restore IDE0010
#pragma warning restore IDE0066
    }

    private static ExternalMetadataError Disabled()
    {
        return new ExternalMetadataError(
            ExternalMetadataErrorKind.Disabled,
            "external_metadata.disabled",
            "External metadata provider is disabled");
    }

    private static ExternalMetadataError NotConfigured()
    {
        return new ExternalMetadataError(
            ExternalMetadataErrorKind.NotConfigured,
            "external_metadata.not_configured",
            "External metadata provider is not configured");
    }

    private static ExternalMetadataError Unauthorized()
    {
        return new ExternalMetadataError(
            ExternalMetadataErrorKind.Unauthorized,
            "external_metadata.unauthorized",
            "External metadata provider credentials were rejected");
    }

    private static ExternalMetadataError RateLimited(TimeSpan? retryAfter)
    {
        return new ExternalMetadataError(
            ExternalMetadataErrorKind.RateLimited,
            "external_metadata.rate_limited",
            "External metadata provider rate limit was exceeded",
            retryAfter);
    }

    private static ExternalMetadataError Timeout()
    {
        return new ExternalMetadataError(
            ExternalMetadataErrorKind.Timeout,
            "external_metadata.timeout",
            "External metadata provider timed out");
    }

    private static ExternalMetadataError Unavailable()
    {
        return new ExternalMetadataError(
            ExternalMetadataErrorKind.Unavailable,
            "external_metadata.unavailable",
            "External metadata provider is unavailable");
    }

    private static ExternalMetadataError InvalidResponse()
    {
        return new ExternalMetadataError(
            ExternalMetadataErrorKind.InvalidResponse,
            "external_metadata.invalid_response",
            "External metadata provider returned an invalid response");
    }
}
