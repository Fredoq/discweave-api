using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Cratebase.Api.Tests;

public sealed class ExportRestoreEndpointTests : IClassFixture<PostgresFixture>
{
    private const string RestoreConfirmationHeader = "X-Cratebase-Confirm-Restore";
    private const string RestoreConfirmationValue = "restore-empty-collection";
    private static readonly string[] PostPunkGenres = ["Post-punk"];
    private static readonly string[] FactoryTags = ["factory", "classic"];
    private readonly PostgresFixture _postgres;

    public ExportRestoreEndpointTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact(DisplayName = "JSON restore rebuilds an empty collection and preserves public ids")]
    public async Task Json_restore_rebuilds_an_empty_collection_and_preserves_public_ids()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient adminClient = await host.CreateAuthenticatedClientAsync();
        Guid labelId = await CreateLabelAsync(adminClient, "Factory Records");
        Guid artistId = await CreateArtistAsync(adminClient, "New Order");
        Guid releaseId = await CreateReleaseWithTrackAsync(adminClient, labelId, artistId);
        _ = await CreateOwnedItemAsync(adminClient, releaseId);
        string snapshot = await ExportJsonAsync(adminClient);
        HttpClient userClient = await CreateUserClientAsync(host, adminClient);

        using HttpResponseMessage restoreResponse = await PostRestoreAsync(userClient, snapshot);
        string restoreJson = await restoreResponse.Content.ReadAsStringAsync();
        string restoredSnapshot = await ExportJsonAsync(userClient);
        using var restoredDocument = JsonDocument.Parse(restoredSnapshot);

        Assert.True(restoreResponse.StatusCode == HttpStatusCode.OK, restoreJson);
        using var restoreDocument = JsonDocument.Parse(restoreJson);
        Assert.True(restoreDocument.RootElement.GetProperty("restored").GetBoolean());
        Assert.Equal(1, restoreDocument.RootElement.GetProperty("formatVersion").GetInt32());
        Assert.Equal(1, restoreDocument.RootElement.GetProperty("artists").GetInt32());
        Assert.Equal(1, restoreDocument.RootElement.GetProperty("labels").GetInt32());
        Assert.Equal(1, restoreDocument.RootElement.GetProperty("releases").GetInt32());
        Assert.Equal(1, restoreDocument.RootElement.GetProperty("tracks").GetInt32());
        Assert.Equal(1, restoreDocument.RootElement.GetProperty("ownedItems").GetInt32());
        Assert.DoesNotContain("collectionId", restoredSnapshot, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(artistId, Assert.Single(restoredDocument.RootElement.GetProperty("artists").EnumerateArray()).GetProperty("id").GetGuid());
        Assert.Equal(labelId, Assert.Single(restoredDocument.RootElement.GetProperty("labels").EnumerateArray()).GetProperty("id").GetGuid());
        Assert.Equal(releaseId, Assert.Single(restoredDocument.RootElement.GetProperty("releases").EnumerateArray()).GetProperty("id").GetGuid());
        Assert.Contains("Age of Consent", restoredSnapshot, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "JSON restore rejects populated collections")]
    public async Task Json_restore_rejects_populated_collections()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        Guid labelId = await CreateLabelAsync(client, "Factory Records");
        Guid artistId = await CreateArtistAsync(client, "New Order");
        _ = await CreateReleaseWithTrackAsync(client, labelId, artistId);
        string snapshot = await ExportJsonAsync(client);

        using HttpResponseMessage restoreResponse = await PostRestoreAsync(client, snapshot);
        using JsonDocument document = await ReadJsonAsync(restoreResponse);

        Assert.Equal(HttpStatusCode.Conflict, restoreResponse.StatusCode);
        Assert.Equal("export_restore.collection_not_empty", document.RootElement.GetProperty("code").GetString());
    }

    [Fact(DisplayName = "JSON restore requires an explicit confirmation header")]
    public async Task Json_restore_requires_an_explicit_confirmation_header()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient adminClient = await host.CreateAuthenticatedClientAsync();
        string snapshot = await CreateSnapshotAsync(adminClient);
        HttpClient userClient = await CreateUserClientAsync(host, adminClient);

        using HttpResponseMessage restoreResponse = await PostRestoreAsync(userClient, snapshot, confirm: false);
        using JsonDocument document = await ReadJsonAsync(restoreResponse);

        Assert.Equal(HttpStatusCode.BadRequest, restoreResponse.StatusCode);
        Assert.Equal("export_restore.confirmation_required", document.RootElement.GetProperty("code").GetString());
    }

    [Fact(DisplayName = "JSON restore rejects unsupported format versions")]
    public async Task Json_restore_rejects_unsupported_format_versions()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient adminClient = await host.CreateAuthenticatedClientAsync();
        JsonObject snapshot = JsonNode.Parse(await CreateSnapshotAsync(adminClient))!.AsObject();
        snapshot["formatVersion"] = 999;
        HttpClient userClient = await CreateUserClientAsync(host, adminClient);

        using HttpResponseMessage restoreResponse = await PostRestoreAsync(userClient, snapshot.ToJsonString());
        using JsonDocument document = await ReadJsonAsync(restoreResponse);

        Assert.Equal(HttpStatusCode.BadRequest, restoreResponse.StatusCode);
        Assert.Equal("export_restore.format_version_unsupported", document.RootElement.GetProperty("code").GetString());
    }

    [Fact(DisplayName = "JSON restore rejects invalid snapshot references")]
    public async Task Json_restore_rejects_invalid_snapshot_references()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient adminClient = await host.CreateAuthenticatedClientAsync();
        JsonObject snapshot = JsonNode.Parse(await CreateSnapshotAsync(adminClient))!.AsObject();
        JsonArray credits = snapshot["credits"]!.AsArray();
        credits[0]!["contributorArtistId"] = Guid.CreateVersion7();
        HttpClient userClient = await CreateUserClientAsync(host, adminClient);

        using HttpResponseMessage restoreResponse = await PostRestoreAsync(userClient, snapshot.ToJsonString());
        using JsonDocument document = await ReadJsonAsync(restoreResponse);

        Assert.Equal(HttpStatusCode.BadRequest, restoreResponse.StatusCode);
        Assert.Equal("export_restore.snapshot_invalid", document.RootElement.GetProperty("code").GetString());
    }

    [Fact(DisplayName = "JSON restore stays scoped to the authenticated collection")]
    public async Task Json_restore_stays_scoped_to_the_authenticated_collection()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient adminClient = await host.CreateAuthenticatedClientAsync();
        string snapshot = await CreateSnapshotAsync(adminClient);
        _ = await CreateArtistAsync(adminClient, "Admin Secret");
        HttpClient userClient = await CreateUserClientAsync(host, adminClient);

        using HttpResponseMessage restoreResponse = await PostRestoreAsync(userClient, snapshot);
        string restoreJson = await restoreResponse.Content.ReadAsStringAsync();
        _ = await CreateArtistAsync(userClient, "Collector Only");
        string adminSnapshot = await ExportJsonAsync(adminClient);
        string userSnapshot = await ExportJsonAsync(userClient);

        Assert.True(restoreResponse.StatusCode == HttpStatusCode.OK, restoreJson);
        Assert.Contains("Admin Secret", adminSnapshot, StringComparison.Ordinal);
        Assert.DoesNotContain("Collector Only", adminSnapshot, StringComparison.Ordinal);
        Assert.Contains("Collector Only", userSnapshot, StringComparison.Ordinal);
        Assert.DoesNotContain("Admin Secret", userSnapshot, StringComparison.Ordinal);
    }

    private static async Task<string> CreateSnapshotAsync(HttpClient client)
    {
        Guid labelId = await CreateLabelAsync(client, "Factory Records");
        Guid artistId = await CreateArtistAsync(client, "New Order");
        Guid releaseId = await CreateReleaseWithTrackAsync(client, labelId, artistId);
        _ = await CreateOwnedItemAsync(client, releaseId);

        return await ExportJsonAsync(client);
    }

    private static async Task<HttpClient> CreateUserClientAsync(ApiTestHost host, HttpClient adminClient)
    {
        string email = $"collector-{Guid.CreateVersion7()}@example.com";
        using HttpResponseMessage createUserResponse = await adminClient.PostAsJsonAsync(
            "/api/admin/users",
            new CreateUserRequest(email, "Password1!", false));
        Assert.Equal(HttpStatusCode.Created, createUserResponse.StatusCode);

        HttpClient userClient = host.CreateClient();
        using HttpResponseMessage loginResponse = await userClient.PostAsJsonAsync(
            "/api/auth/login",
            new AuthRequest(email, "Password1!"));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        return userClient;
    }

    private static async Task<string> ExportJsonAsync(HttpClient client)
    {
        using HttpResponseMessage response = await client.GetAsync("/api/exports/json");
        string json = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return json;
    }

    private static async Task<HttpResponseMessage> PostRestoreAsync(HttpClient client, string json, bool confirm = true)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/exports/json/restore")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        if (confirm)
        {
            request.Headers.Add(RestoreConfirmationHeader, RestoreConfirmationValue);
        }

        return await client.SendAsync(request);
    }

    private static async Task<Guid> CreateLabelAsync(HttpClient client, string name)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/labels", new { name });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateArtistAsync(HttpClient client, string name)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/artists", new { type = "person", name });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateReleaseWithTrackAsync(HttpClient client, Guid labelId, Guid artistId)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/releases",
            new
            {
                title = "Power, Corruption & Lies",
                type = "album",
                isVariousArtists = false,
                labelId,
                year = 1983,
                genres = PostPunkGenres,
                tags = FactoryTags,
                artistCredits = new[] { new { artistId, role = "mainArtist" } },
                tracklist = new[] { new { title = "Age of Consent", position = 1, durationSeconds = 316 } }
            });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateOwnedItemAsync(HttpClient client, Guid releaseId)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/owned-items",
            new
            {
                targetType = "release",
                targetId = releaseId,
                status = "owned",
                medium = new { type = "vinyl", description = "LP" },
                condition = "nearMint",
                storageLocation = "Shelf A"
            });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
    }

    private sealed record AuthRequest(string Email, string Password);

    private sealed record CreateUserRequest(string Email, string Password, bool IsAdmin);
}
