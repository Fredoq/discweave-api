using System.Net;
using System.Text.Json;
using DiscWeave.Api.Features.ExternalMetadata;
using DiscWeave.Application.ExternalMetadata;
using Microsoft.AspNetCore.Http;

namespace DiscWeave.Api.Tests;

public sealed class ExternalMetadataEndpointErrorTests
{
    [Theory(DisplayName = "External metadata errors map to deterministic HTTP responses")]
    [InlineData(ExternalMetadataErrorKind.Disabled, HttpStatusCode.ServiceUnavailable, "external_metadata.disabled")]
    [InlineData(ExternalMetadataErrorKind.NotConfigured, HttpStatusCode.ServiceUnavailable, "external_metadata.not_configured")]
    [InlineData(ExternalMetadataErrorKind.Unauthorized, HttpStatusCode.BadGateway, "external_metadata.unauthorized")]
    [InlineData(ExternalMetadataErrorKind.RateLimited, HttpStatusCode.TooManyRequests, "external_metadata.rate_limited")]
    [InlineData(ExternalMetadataErrorKind.Timeout, HttpStatusCode.GatewayTimeout, "external_metadata.timeout")]
    [InlineData(ExternalMetadataErrorKind.Unavailable, HttpStatusCode.ServiceUnavailable, "external_metadata.unavailable")]
    [InlineData(ExternalMetadataErrorKind.InvalidResponse, HttpStatusCode.BadGateway, "external_metadata.invalid_response")]
    public async Task External_metadata_errors_map_to_deterministic_Http_responses(
        ExternalMetadataErrorKind kind,
        HttpStatusCode expectedStatusCode,
        string expectedCode)
    {
        ExternalMetadataError error = new(kind, expectedCode, "Safe provider message");

        DefaultHttpContext context = CreateContext();
        IResult result = ExternalMetadataEndpointErrors.ToHttpResult(error);
        await result.ExecuteAsync(context);
        context.Response.Body.Position = 0;
        using JsonDocument document = await JsonDocument.ParseAsync(context.Response.Body);

        Assert.Equal((int)expectedStatusCode, context.Response.StatusCode);
        Assert.Equal(expectedCode, document.RootElement.GetProperty("code").GetString());
        Assert.Equal("Safe provider message", document.RootElement.GetProperty("message").GetString());
    }

    [Fact(DisplayName = "External metadata rate limit responses include retry-after when available")]
    public async Task External_metadata_rate_limit_responses_include_retry_after_when_available()
    {
        ExternalMetadataError error = new(
            ExternalMetadataErrorKind.RateLimited,
            "external_metadata.rate_limited",
            "External metadata provider rate limit was exceeded",
            TimeSpan.FromSeconds(45));

        DefaultHttpContext context = CreateContext();
        IResult result = ExternalMetadataEndpointErrors.ToHttpResult(error);
        await result.ExecuteAsync(context);

        Assert.Equal(StatusCodes.Status429TooManyRequests, context.Response.StatusCode);
        Assert.Equal("45", context.Response.Headers.RetryAfter.ToString());
    }

    private static DefaultHttpContext CreateContext()
    {
        return new DefaultHttpContext
        {
            Response =
            {
                Body = new MemoryStream()
            }
        };
    }
}
