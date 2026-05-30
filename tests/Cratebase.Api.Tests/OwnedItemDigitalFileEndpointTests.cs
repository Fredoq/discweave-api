using System.Net;
using System.Net.Http.Json;
using System.Globalization;
using System.Text.Json;

namespace Cratebase.Api.Tests;

public sealed class OwnedItemDigitalFileEndpointTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _postgres;

    public OwnedItemDigitalFileEndpointTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact(DisplayName = "Digital file patch updates path format and import identity")]
    public async Task Digital_file_patch_updates_path_format_and_import_identity()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        Guid releaseId = await CreateReleaseAsync(client, "Patchable Digital Release");
        Guid ownedItemId = await CreateOwnedItemAsync(
            client,
            releaseId,
            new { type = "digital", path = "/music/old/01-old.flac", format = "flac" });

        using HttpResponseMessage response = await PatchJsonAsync(
            client,
            $"/api/owned-items/{ownedItemId}/digital-file",
            new
            {
                path = "/music/new/01 Age of Consent.m4a",
                format = "m4a",
                sizeBytes = 123456,
                lastModifiedAt = "2026-05-29T09:15:00Z",
                contentHash = "ABCDEF0123"
            });
        using JsonDocument document = await ReadJsonAsync(response);
        DigitalImportIdentity? identity = await host.FindDigitalImportIdentityAsync(ownedItemId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement medium = document.RootElement.GetProperty("medium");
        Assert.Equal("digital", medium.GetProperty("type").GetString());
        Assert.Equal("/music/new/01 Age of Consent.m4a", medium.GetProperty("path").GetString());
        Assert.Equal("m4a", medium.GetProperty("format").GetString());

        Assert.NotNull(identity);
        Assert.Equal("/music/new/01 Age of Consent.m4a", identity.Path);
        Assert.Equal(123456, identity.SizeBytes);
        Assert.Equal(DateTimeOffset.Parse("2026-05-29T09:15:00Z", CultureInfo.InvariantCulture), identity.LastModifiedAt);
        Assert.Equal("abcdef0123", identity.ContentHash);
    }

    [Fact(DisplayName = "Digital file patch stays scoped to the authenticated collection")]
    public async Task Digital_file_patch_stays_scoped_to_the_authenticated_collection()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        (HttpClient adminClient, HttpClient userClient) = await CreateAuthenticatedClientsAsync(host);
        Guid adminReleaseId = await CreateReleaseAsync(adminClient, "Admin Digital Release");
        Guid adminOwnedItemId = await CreateOwnedItemAsync(
            adminClient,
            adminReleaseId,
            new { type = "digital", path = "/music/admin.flac", format = "flac" });

        using HttpResponseMessage response = await PatchJsonAsync(
            userClient,
            $"/api/owned-items/{adminOwnedItemId}/digital-file",
            new
            {
                path = "/music/user-claim.flac",
                format = "flac",
                sizeBytes = 100,
                lastModifiedAt = "2026-05-29T09:15:00Z",
                contentHash = "abc"
            });
        using JsonDocument document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("owned_item.not_found", document.RootElement.GetProperty("code").GetString());
    }

    [Fact(DisplayName = "Digital file patch supports batch client updates without search rebuild conflicts")]
    public async Task Digital_file_patch_supports_batch_client_updates_without_search_rebuild_conflicts()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        Guid releaseId = await CreateReleaseAsync(client, "Batch Patch Digital Release");
        Guid firstOwnedItemId = await CreateOwnedItemAsync(
            client,
            releaseId,
            new { type = "digital", path = "/music/batch/old-1.flac", format = "flac" });
        Guid secondOwnedItemId = await CreateOwnedItemAsync(
            client,
            releaseId,
            new { type = "digital", path = "/music/batch/old-2.flac", format = "flac" });

        HttpResponseMessage[] responses = await Task.WhenAll(
            PatchJsonAsync(
                client,
                $"/api/owned-items/{firstOwnedItemId}/digital-file",
                new
                {
                    path = "/music/batch/new-1.flac",
                    format = "flac",
                    sizeBytes = 111,
                    lastModifiedAt = "2026-05-29T09:15:00Z",
                    contentHash = "hash-1"
                }),
            PatchJsonAsync(
                client,
                $"/api/owned-items/{secondOwnedItemId}/digital-file",
                new
                {
                    path = "/music/batch/new-2.flac",
                    format = "flac",
                    sizeBytes = 222,
                    lastModifiedAt = "2026-05-29T09:16:00Z",
                    contentHash = "hash-2"
                }));

        foreach (HttpResponseMessage response in responses)
        {
            using (response)
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }
        }
    }

    [Fact(DisplayName = "Digital file patch rejects non digital owned items")]
    public async Task Digital_file_patch_rejects_non_digital_owned_items()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        Guid releaseId = await CreateReleaseAsync(client, "Physical Release");
        Guid ownedItemId = await CreateOwnedItemAsync(client, releaseId, new { type = "vinyl", description = "LP" });

        using HttpResponseMessage response = await PatchJsonAsync(
            client,
            $"/api/owned-items/{ownedItemId}/digital-file",
            new
            {
                path = "/music/physical.flac",
                format = "flac",
                sizeBytes = 100,
                lastModifiedAt = "2026-05-29T09:15:00Z",
                contentHash = "abc"
            });
        using JsonDocument document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("owned_item.digital_file_required", document.RootElement.GetProperty("code").GetString());
    }

    private static async Task<(HttpClient AdminClient, HttpClient UserClient)> CreateAuthenticatedClientsAsync(ApiTestHost host)
    {
        HttpClient adminClient = host.CreateClient();
        using HttpResponseMessage registerResponse = await adminClient.PostAsJsonAsync(
            "/api/auth/register",
            new { email = "owner@example.com", password = "Password1!" });
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        using HttpResponseMessage createUserResponse = await adminClient.PostAsJsonAsync(
            "/api/admin/users",
            new { email = "collector@example.com", password = "Password1!", isAdmin = false });
        Assert.Equal(HttpStatusCode.Created, createUserResponse.StatusCode);

        HttpClient userClient = host.CreateClient();
        using HttpResponseMessage loginResponse = await userClient.PostAsJsonAsync(
            "/api/auth/login",
            new { email = "collector@example.com", password = "Password1!" });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        return (adminClient, userClient);
    }

    private static async Task<Guid> CreateReleaseAsync(HttpClient client, string title)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/releases",
            new
            {
                title,
                type = "standalone",
                isVariousArtists = true,
                notOnLabel = true,
                artistCredits = Array.Empty<object>(),
                tracklist = Array.Empty<object>(),
                genres = Array.Empty<string>(),
                tags = Array.Empty<string>()
            });
        using JsonDocument document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateOwnedItemAsync(HttpClient client, Guid releaseId, object medium)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/owned-items",
            new
            {
                targetType = "release",
                targetId = releaseId,
                status = "owned",
                medium
            });
        using JsonDocument document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<HttpResponseMessage> PatchJsonAsync(HttpClient client, string path, object request)
    {
        using var message = new HttpRequestMessage(HttpMethod.Patch, path)
        {
            Content = JsonContent.Create(request)
        };

        return await client.SendAsync(message);
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
    }
}
