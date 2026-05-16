using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Cratebase.Api.Tests;

public sealed class DesktopImportEndpointTests : IClassFixture<PostgresFixture>
{
    private static readonly string[] BeginsTrackArtistNames = ["Steve Bicknell", "C.K. & pH 1"];

    private readonly PostgresFixture _postgres;

    public DesktopImportEndpointTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact(DisplayName = "Desktop import endpoint requires authentication")]
    public async Task Desktop_import_endpoint_requires_authentication()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = host.CreateClient();

        using HttpResponseMessage listResponse = await client.GetAsync("/api/imports");
        using HttpResponseMessage scanResponse = await client.PostAsJsonAsync("/api/imports/desktop-folder-scans", EmptyDesktopScan());

        Assert.Equal(HttpStatusCode.Unauthorized, listResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, scanResponse.StatusCode);
    }

    [Fact(DisplayName = "Local agent endpoints are removed")]
    public async Task Local_agent_endpoints_are_removed()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();

        using HttpResponseMessage tokenResponse = await client.PostAsync("/api/imports/local-agent-tokens", null);
        using HttpResponseMessage scanResponse = await client.PostAsJsonAsync("/api/imports/local-agent-scans", new { });
        using HttpResponseMessage downloadResponse = await client.GetAsync("/api/imports/local-agent-downloads/macos");

        AssertOldEndpointIsUnavailable(tokenResponse);
        AssertOldEndpointIsUnavailable(scanResponse);
        AssertOldEndpointIsUnavailable(downloadResponse);
    }

    [Fact(DisplayName = "Desktop download returns the configured macOS installer")]
    public async Task Desktop_download_returns_the_configured_macos_installer()
    {
        using var installerDirectory = TempImportRoot.Create();
        string installerPath = Path.Combine(installerDirectory.Path, "Cratebase-0.0.0-arm64.dmg");
        byte[] installerBytes = [0x43, 0x42, 0x44, 0x4d, 0x47];
        await File.WriteAllBytesAsync(installerPath, installerBytes);
        await using ApiTestHost host = await ApiTestHost.CreateAsync(
            _postgres,
            new Dictionary<string, string?>
            {
                ["DesktopDownloads:MacOsInstallerDirectory"] = installerDirectory.Path
            });
        HttpClient client = await host.CreateAuthenticatedClientAsync();

        using HttpResponseMessage response = await client.GetAsync("/api/imports/desktop-downloads/macos");
        byte[] responseBytes = await response.Content.ReadAsByteArrayAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/x-apple-diskimage", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Cratebase-0.0.0-arm64.dmg", response.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        Assert.Equal(installerBytes, responseBytes);
    }

    [Fact(DisplayName = "Desktop scan persists draft and confirm creates catalog data")]
    public async Task Desktop_scan_persists_draft_and_confirm_creates_catalog_data()
    {
        using var root = TempImportRoot.Create();
        string releaseDirectory = Path.Combine(root.Path, "[AA 01, 2016-07-15] Steven Julien - Fallen");
        _ = Directory.CreateDirectory(releaseDirectory);
        string audioPath = Path.Combine(releaseDirectory, "01 Begins.flac");
        await File.WriteAllTextAsync(audioPath, "fake flac");
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();

        using JsonDocument scanDocument = await PostScanAsync(client, root.Path, audioPath);
        JsonElement draft = scanDocument.RootElement.GetProperty("drafts")[0];
        JsonElement draftTrack = draft.GetProperty("tracks")[0];
        Guid sessionId = scanDocument.RootElement.GetProperty("id").GetGuid();
        Guid draftId = draft.GetProperty("id").GetGuid();

        Assert.Equal("Fallen", draft.GetProperty("title").GetString());
        Assert.Equal("AA 01", draft.GetProperty("catalogNumber").GetString());
        Assert.Equal("2016-07-15", draft.GetProperty("releaseDate").GetString());
        Assert.Equal("Steven Julien", draft.GetProperty("artistNames")[0].GetString());
        Assert.Equal("Begins", draftTrack.GetProperty("title").GetString());
        Assert.Equal(2, draftTrack.GetProperty("artistCredits").GetArrayLength());
        Assert.Equal("Steve Bicknell", draftTrack.GetProperty("artistCredits")[0].GetProperty("name").GetString());
        Assert.Equal("mainArtist", draftTrack.GetProperty("artistCredits")[0].GetProperty("role").GetString());
        Assert.Equal("C.K. & pH 1", draftTrack.GetProperty("artistCredits")[1].GetProperty("name").GetString());

        using HttpResponseMessage confirmResponse = await client.PostAsync($"/api/imports/{sessionId}/drafts/{draftId}/confirm", null);
        using JsonDocument confirmDocument = await ReadJsonAsync(confirmResponse);
        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);
        Assert.Equal("confirmed", confirmDocument.RootElement.GetProperty("drafts")[0].GetProperty("status").GetString());

        using HttpResponseMessage releaseResponse = await client.GetAsync("/api/releases?search=Fallen&limit=10&offset=0");
        using JsonDocument releaseDocument = await ReadJsonAsync(releaseResponse);
        using HttpResponseMessage trackResponse = await client.GetAsync("/api/tracks?search=Begins&limit=10&offset=0");
        using JsonDocument trackDocument = await ReadJsonAsync(trackResponse);
        using HttpResponseMessage itemResponse = await client.GetAsync("/api/owned-items?limit=10&offset=0");
        using JsonDocument itemDocument = await ReadJsonAsync(itemResponse);

        Assert.Equal(HttpStatusCode.OK, releaseResponse.StatusCode);
        Assert.Equal(1, releaseDocument.RootElement.GetProperty("total").GetInt32());
        Assert.Equal("2016-07-15", releaseDocument.RootElement.GetProperty("items")[0].GetProperty("releaseDate").GetString());
        Assert.Equal(HttpStatusCode.OK, trackResponse.StatusCode);
        Assert.Equal(1, trackDocument.RootElement.GetProperty("total").GetInt32());
        JsonElement trackCredits = trackDocument.RootElement.GetProperty("items")[0].GetProperty("credits");
        Assert.Equal(2, trackCredits.GetArrayLength());
        Assert.Contains(trackCredits.EnumerateArray(), credit =>
            credit.GetProperty("artistName").GetString() == "Steve Bicknell" &&
            credit.GetProperty("role").GetString() == "mainArtist");
        Assert.Contains(trackCredits.EnumerateArray(), credit =>
            credit.GetProperty("artistName").GetString() == "C.K. & pH 1" &&
            credit.GetProperty("role").GetString() == "mainArtist");
        Assert.Equal(1, itemDocument.RootElement.GetProperty("total").GetInt32());
        Assert.Equal("track", itemDocument.RootElement.GetProperty("items")[0].GetProperty("targetType").GetString());
        Assert.Equal("flac", itemDocument.RootElement.GetProperty("items")[0].GetProperty("medium").GetProperty("format").GetString());
    }

    [Fact(DisplayName = "Confirmed desktop import drafts are terminal")]
    public async Task Confirmed_desktop_import_drafts_are_terminal()
    {
        using var root = TempImportRoot.Create();
        string releaseDirectory = Path.Combine(root.Path, "[AA 01, 2016-07-15] Steven Julien - Fallen");
        _ = Directory.CreateDirectory(releaseDirectory);
        string audioPath = Path.Combine(releaseDirectory, "01 Begins.flac");
        await File.WriteAllTextAsync(audioPath, "fake flac");
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();

        using JsonDocument scanDocument = await PostScanAsync(client, root.Path, audioPath);
        Guid sessionId = scanDocument.RootElement.GetProperty("id").GetGuid();
        Guid draftId = scanDocument.RootElement.GetProperty("drafts")[0].GetProperty("id").GetGuid();

        using HttpResponseMessage confirmResponse = await client.PostAsync($"/api/imports/{sessionId}/drafts/{draftId}/confirm", null);
        _ = confirmResponse.EnsureSuccessStatusCode();

        using HttpResponseMessage updateResponse = await client.PutAsJsonAsync(
            $"/api/imports/{sessionId}/drafts/{draftId}",
            ConfirmedDraftUpdatePayload());
        using JsonDocument updateDocument = await ReadJsonAsync(updateResponse);
        using HttpResponseMessage skipResponse = await client.PostAsync($"/api/imports/{sessionId}/drafts/{draftId}/skip", null);
        using JsonDocument skipDocument = await ReadJsonAsync(skipResponse);

        Assert.Equal(HttpStatusCode.BadRequest, updateResponse.StatusCode);
        Assert.Equal("release_import_draft.confirmed", updateDocument.RootElement.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, skipResponse.StatusCode);
        Assert.Equal("release_import_draft.confirmed", skipDocument.RootElement.GetProperty("code").GetString());
    }

    [Fact(DisplayName = "Concurrent desktop import confirmations create one release")]
    public async Task Concurrent_desktop_import_confirmations_create_one_release()
    {
        using var root = TempImportRoot.Create();
        string releaseDirectory = Path.Combine(root.Path, "[AA 01, 2016-07-15] Steven Julien - Fallen");
        _ = Directory.CreateDirectory(releaseDirectory);
        string audioPath = Path.Combine(releaseDirectory, "01 Begins.flac");
        await File.WriteAllTextAsync(audioPath, "fake flac");
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();

        using JsonDocument scanDocument = await PostScanAsync(client, root.Path, audioPath);
        Guid sessionId = scanDocument.RootElement.GetProperty("id").GetGuid();
        Guid draftId = scanDocument.RootElement.GetProperty("drafts")[0].GetProperty("id").GetGuid();

        HttpResponseMessage[] confirmResponses = await Task.WhenAll(
            Enumerable.Range(0, 4).Select(_ => client.PostAsync($"/api/imports/{sessionId}/drafts/{draftId}/confirm", null)));
        foreach (HttpResponseMessage response in confirmResponses)
        {
            using (response)
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }
        }

        using HttpResponseMessage releaseResponse = await client.GetAsync("/api/releases?search=Fallen&limit=10&offset=0");
        using JsonDocument releaseDocument = await ReadJsonAsync(releaseResponse);
        using HttpResponseMessage itemResponse = await client.GetAsync("/api/owned-items?limit=10&offset=0");
        using JsonDocument itemDocument = await ReadJsonAsync(itemResponse);

        Assert.Equal(HttpStatusCode.OK, releaseResponse.StatusCode);
        Assert.Equal(1, releaseDocument.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.OK, itemResponse.StatusCode);
        Assert.Equal(1, itemDocument.RootElement.GetProperty("total").GetInt32());
    }

    private static async Task<JsonDocument> PostScanAsync(HttpClient client, string rootPath, string audioPath)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/imports/desktop-folder-scans",
            new
            {
                sourceRoot = rootPath,
                ignoredFileCount = 0,
                files = new[]
                {
                    new
                    {
                        filePath = audioPath,
                        relativePath = Path.GetRelativePath(rootPath, audioPath),
                        format = "flac",
                        sizeBytes = 9,
                        lastModifiedAt = DateTimeOffset.UtcNow,
                        audioMetadata = new
                        {
                            title = (string?)null,
                            artists = BeginsTrackArtistNames,
                            albumTitle = (string?)null,
                            albumArtists = Array.Empty<string>(),
                            catalogNumber = (string?)null,
                            releaseDate = (string?)null,
                            year = (int?)null,
                            durationSeconds = (int?)null,
                            trackNumber = (int?)null
                        },
                        coverArtifact = (object?)null
                    }
                }
            });
        JsonDocument document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return document;
    }

    private static object EmptyDesktopScan()
    {
        return new { sourceRoot = "/tmp/cratebase-empty", files = Array.Empty<object>(), ignoredFileCount = 0 };
    }

    private static object ConfirmedDraftUpdatePayload()
    {
        return new
        {
            title = "Edited after confirmation",
            type = "unknown",
            catalogNumber = (string?)null,
            labelName = (string?)null,
            releaseDate = (string?)null,
            year = (int?)null,
            isVariousArtists = false,
            notOnLabel = false,
            coverPath = (string?)null,
            artistNames = Array.Empty<string>(),
            artistCredits = Array.Empty<object>(),
            labels = Array.Empty<object>(),
            selectedArtistIds = Array.Empty<Guid>(),
            genres = Array.Empty<string>(),
            tags = Array.Empty<string>(),
            tracks = Array.Empty<object>()
        };
    }

    private static void AssertOldEndpointIsUnavailable(HttpResponseMessage response)
    {
        Assert.True(
            response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed,
            $"Expected old endpoint to be unavailable, got {response.StatusCode}");
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        Stream stream = await response.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(stream);
    }

    private sealed class TempImportRoot : IDisposable
    {
        private TempImportRoot(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TempImportRoot Create()
        {
            return new TempImportRoot(Directory.CreateTempSubdirectory("cratebase-import-test-").FullName);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
