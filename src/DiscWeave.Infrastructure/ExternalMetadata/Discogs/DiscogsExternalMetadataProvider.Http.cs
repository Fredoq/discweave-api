using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using DiscWeave.Application.ExternalMetadata;
using Microsoft.Extensions.Logging;

namespace DiscWeave.Infrastructure.ExternalMetadata.Discogs;

public sealed partial class DiscogsExternalMetadataProvider
{
    private const int MaximumSendAttempts = 2;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(50);

    private async Task<ExternalMetadataResult<T>> SendAsync<T>(
        string path,
        Dictionary<string, string> parameters,
        CancellationToken cancellationToken)
        where T : class
    {
        for (int attempt = 1; attempt <= MaximumSendAttempts; attempt++)
        {
            using HttpRequestMessage request = CreateRequest(path, parameters);
            try
            {
                using HttpResponseMessage response = await _httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    if (attempt < MaximumSendAttempts && IsRetryable(response.StatusCode))
                    {
                        LogDiscogsRetry(path, attempt, (int)response.StatusCode);
                        await Task.Delay(RetryDelay, cancellationToken);
                        continue;
                    }

                    ExternalMetadataError error = MapFailure(response);
                    LogDiscogsFailure(path, (int)response.StatusCode, error.Kind);
                    return new ExternalMetadataResult<T>(error);
                }

                await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                T? value = await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken);
                return value is null
                    ? new ExternalMetadataResult<T>(InvalidResponse())
                    : new ExternalMetadataResult<T>(value);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                LogDiscogsFailure(path, null, ExternalMetadataErrorKind.Timeout);
                return new ExternalMetadataResult<T>(Timeout());
            }
            catch (JsonException)
            {
                LogDiscogsFailure(path, null, ExternalMetadataErrorKind.InvalidResponse);
                return new ExternalMetadataResult<T>(InvalidResponse());
            }
            catch (HttpRequestException) when (attempt < MaximumSendAttempts)
            {
                LogDiscogsRetry(path, attempt, null);
                await Task.Delay(RetryDelay, cancellationToken);
            }
            catch (HttpRequestException)
            {
                LogDiscogsFailure(path, null, ExternalMetadataErrorKind.Unavailable);
                return new ExternalMetadataResult<T>(Unavailable());
            }
        }

        LogDiscogsFailure(path, null, ExternalMetadataErrorKind.Unavailable);
        return new ExternalMetadataResult<T>(Unavailable());
    }

    private HttpRequestMessage CreateRequest(string path, Dictionary<string, string> parameters)
    {
        HttpRequestMessage request = new(HttpMethod.Get, BuildUri(path, parameters));
        request.Headers.UserAgent.Clear();
        _ = request.Headers.UserAgent.TryParseAdd(_options.UserAgent);
        request.Headers.Authorization = new AuthenticationHeaderValue("Discogs", $"token={_options.AccessToken}");

        return request;
    }

    private static bool IsRetryable(HttpStatusCode statusCode)
    {
        int code = (int)statusCode;
        return code >= 500 && statusCode is not HttpStatusCode.NotImplemented;
    }

    private void LogDiscogsRetry(string path, int attempt, int? statusCode)
    {
        DiscogsRetry(_logger, path, attempt, statusCode);
    }

    private void LogDiscogsFailure(string path, int? statusCode, ExternalMetadataErrorKind kind)
    {
        DiscogsFailure(_logger, path, kind, statusCode);
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Retrying Discogs request for {Path} after transient failure on attempt {Attempt}. StatusCode: {StatusCode}")]
    private static partial void DiscogsRetry(ILogger logger, string path, int attempt, int? statusCode);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "Discogs request for {Path} failed with {ErrorKind}. StatusCode: {StatusCode}")]
    private static partial void DiscogsFailure(ILogger logger, string path, ExternalMetadataErrorKind errorKind, int? statusCode);
}
