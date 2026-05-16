using Cratebase.Domain.Imports;
using Cratebase.Importing;
using Microsoft.AspNetCore.Http;
using System.Net.Http.Json;

namespace Cratebase.LocalAgent;

public sealed class LocalAgentScanHandler
{
    private readonly ILocalFolderPicker _folderPicker;
    private readonly ReleaseFolderScanner _scanner;

    public LocalAgentScanHandler(ILocalFolderPicker folderPicker, ReleaseFolderScanner scanner)
    {
        _folderPicker = folderPicker;
        _scanner = scanner;
    }

    public static async Task<IResult> HandleAsync(
        LocalAgentPickAndScanRequest request,
        LocalAgentScanHandler handler,
        CancellationToken cancellationToken)
    {
        return await handler.HandleAsync(request, cancellationToken);
    }

    private async Task<IResult> HandleAsync(LocalAgentPickAndScanRequest request, CancellationToken cancellationToken)
    {
        if (!TryBackendBaseUrl(request.BackendBaseUrl, out Uri? backendBaseUrl))
        {
            return Results.BadRequest(new { code = "local_agent.backend_url_invalid", message = "Backend URL is invalid" });
        }
        Uri backendUri = backendBaseUrl ?? throw new InvalidOperationException("Backend URL is required");

        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return Results.BadRequest(new { code = "local_agent.token_required", message = "Pairing token is required" });
        }

        string? folderPath = await _folderPicker.PickFolderAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return Results.BadRequest(new { code = "local_agent.folder_not_selected", message = "No folder was selected" });
        }

        ReleaseFolderScanPayload scan = _scanner.Scan(
            folderPath,
            request.ReleaseFolderPatterns is { Count: > 0 } ? request.ReleaseFolderPatterns : [ReleaseFolderNameParser.DefaultTemplate],
            request.TrackFilePatterns is { Count: > 0 } ? request.TrackFilePatterns : TrackFileNameParser.DefaultTemplates,
            includeCoverArtifacts: true);

        using var httpClient = new HttpClient();
        using HttpResponseMessage response = await httpClient.PostAsJsonAsync(
            new Uri(backendUri, "/api/imports/local-agent-scans"),
            new LocalAgentScanUploadRequest(request.Token, scan),
            cancellationToken);
        string content = await response.Content.ReadAsStringAsync(cancellationToken);

        return Results.Content(content, response.Content.Headers.ContentType?.MediaType ?? "application/json", statusCode: (int)response.StatusCode);
    }

    private static bool TryBackendBaseUrl(string value, out Uri? uri)
    {
        uri = null;
        return Uri.TryCreate(value, UriKind.Absolute, out Uri? parsed) &&
            (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps) &&
            (uri = new Uri(parsed.GetLeftPart(UriPartial.Authority))) is not null;
    }
}
