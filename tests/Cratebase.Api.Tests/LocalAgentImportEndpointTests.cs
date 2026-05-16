using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Cratebase.Api.Tests;

public sealed class LocalAgentImportEndpointTests : IClassFixture<PostgresFixture>
{
    private static readonly string[] StevenJulienArtistNames = ["Steven Julien"];
    private static readonly string[] BeginsTrackArtistNames = ["Steve Bicknell", "C.K. & pH 1"];

    private readonly PostgresFixture _postgres;

    public LocalAgentImportEndpointTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact(DisplayName = "Local agent import endpoints require authentication where expected")]
    public async Task Local_agent_import_endpoints_require_authentication_where_expected()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = host.CreateClient();

        using HttpResponseMessage listResponse = await client.GetAsync("/api/imports");
        using HttpResponseMessage tokenResponse = await client.PostAsync("/api/imports/local-agent-tokens", null);

        Assert.Equal(HttpStatusCode.Unauthorized, listResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, tokenResponse.StatusCode);
    }

    [Fact(DisplayName = "Local agent token is short lived and single use")]
    public async Task Local_agent_token_is_short_lived_and_single_use()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        string token = await CreateTokenAsync(client);

        using HttpResponseMessage invalidResponse = await client.PostAsJsonAsync(
            "/api/imports/local-agent-scans",
            new { token = "invalid", scan = EmptyScan() });
        using JsonDocument invalidDocument = await ReadJsonAsync(invalidResponse);
        using HttpResponseMessage firstResponse = await client.PostAsJsonAsync(
            "/api/imports/local-agent-scans",
            new { token, scan = EmptyScan() });
        using HttpResponseMessage secondResponse = await client.PostAsJsonAsync(
            "/api/imports/local-agent-scans",
            new { token, scan = EmptyScan() });
        using JsonDocument secondDocument = await ReadJsonAsync(secondResponse);

        Assert.Equal(HttpStatusCode.Unauthorized, invalidResponse.StatusCode);
        Assert.Equal("local_agent_import_token.invalid", invalidDocument.RootElement.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, secondResponse.StatusCode);
        Assert.Equal("local_agent_import_token.used", secondDocument.RootElement.GetProperty("code").GetString());
    }

    [Fact(DisplayName = "Local agent scan persists draft and confirm creates catalog data")]
    public async Task Local_agent_scan_persists_draft_and_confirm_creates_catalog_data()
    {
        using var root = TempImportRoot.Create();
        string releaseDirectory = Path.Combine(root.Path, "[AA 01, 2016-07-15] Steven Julien - Fallen");
        _ = Directory.CreateDirectory(releaseDirectory);
        string audioPath = Path.Combine(releaseDirectory, "01 Begins.flac");
        await File.WriteAllTextAsync(audioPath, "fake flac");
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        string token = await CreateTokenAsync(client);

        using JsonDocument scanDocument = await PostScanAsync(client, token, root.Path, releaseDirectory, audioPath);
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
        Assert.Equal(2, trackDocument.RootElement.GetProperty("items")[0].GetProperty("credits").GetArrayLength());
        Assert.Equal("Steve Bicknell", trackDocument.RootElement.GetProperty("items")[0].GetProperty("credits")[0].GetProperty("artistName").GetString());
        Assert.Equal("mainArtist", trackDocument.RootElement.GetProperty("items")[0].GetProperty("credits")[0].GetProperty("role").GetString());
        Assert.Equal("C.K. & pH 1", trackDocument.RootElement.GetProperty("items")[0].GetProperty("credits")[1].GetProperty("artistName").GetString());
        Assert.Equal(1, itemDocument.RootElement.GetProperty("total").GetInt32());
        Assert.Equal("track", itemDocument.RootElement.GetProperty("items")[0].GetProperty("targetType").GetString());
        Assert.Equal("flac", itemDocument.RootElement.GetProperty("items")[0].GetProperty("medium").GetProperty("format").GetString());
    }

    private static async Task<string> CreateTokenAsync(HttpClient client)
    {
        using HttpResponseMessage response = await client.PostAsync("/api/imports/local-agent-tokens", null);
        using JsonDocument document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("http://127.0.0.1:43817", document.RootElement.GetProperty("agentBaseUrl").GetString());
        Assert.NotEmpty(document.RootElement.GetProperty("releaseFolderPatterns").EnumerateArray());
        return document.RootElement.GetProperty("token").GetString() ?? throw new InvalidOperationException("Token is required");
    }

    private static async Task<JsonDocument> PostScanAsync(
        HttpClient client,
        string token,
        string rootPath,
        string releaseDirectory,
        string audioPath)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/imports/local-agent-scans",
            new
            {
                token,
                scan = new
                {
                    sourceRoot = rootPath,
                    ignoredFileCount = 0,
                    drafts = new[]
                    {
                        new
                        {
                            sourcePath = releaseDirectory,
                            relativePath = Path.GetFileName(releaseDirectory),
                            title = "Fallen",
                            type = "unknown",
                            catalogNumber = "AA 01",
                            labelName = (string?)null,
                            releaseDate = "2016-07-15",
                            year = 2016,
                            isVariousArtists = false,
                            notOnLabel = true,
                            coverPath = (string?)null,
                            artistNames = StevenJulienArtistNames,
                            selectedArtistIds = Array.Empty<Guid>(),
                            genres = Array.Empty<string>(),
                            tags = Array.Empty<string>(),
                            issues = Array.Empty<object>(),
                            coverArtifact = (object?)null,
                            tracks = new[]
                            {
                                new
                                {
                                    filePath = audioPath,
                                    relativePath = "01 Begins.flac",
                                    format = 1,
                                    sizeBytes = 9,
                                    lastModifiedAt = DateTimeOffset.UtcNow,
                                    duration = (string?)null,
                                    position = 1,
                                    title = "Begins",
                                    artistNames = BeginsTrackArtistNames,
                                    issues = Array.Empty<object>()
                                }
                            }
                        }
                    }
                }
            });
        JsonDocument document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return document;
    }

    private static object EmptyScan()
    {
        return new { sourceRoot = "/tmp/cratebase-empty", drafts = Array.Empty<object>(), ignoredFileCount = 0 };
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
