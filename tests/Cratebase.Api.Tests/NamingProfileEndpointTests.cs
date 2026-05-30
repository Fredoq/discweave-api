using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Cratebase.Api.Tests;

public sealed class NamingProfileEndpointTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _postgres;

    public NamingProfileEndpointTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact(DisplayName = "Naming profile endpoints create update list and delete custom profiles")]
    public async Task Naming_profile_endpoints_create_update_list_and_delete_custom_profiles()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();

        using HttpResponseMessage defaultListResponse = await client.GetAsync("/api/settings/naming-profiles");
        using JsonDocument defaultList = await ReadJsonAsync(defaultListResponse);

        using HttpResponseMessage createResponse = await client.PostAsJsonAsync(
            "/api/settings/naming-profiles",
            new
            {
                name = "WEB FLAC",
                releaseFolderTemplate = "{releaseArtists} - {title} ({year}) [{source} {format} {bitDepth}]",
                trackFileTemplate = "{position2} {title}",
                trackFileWithArtistTemplate = "{position2} {trackArtists} - {title}",
                sortOrder = 50,
                isDefault = true,
                isActive = true
            });
        using JsonDocument createDocument = await ReadJsonAsync(createResponse);
        Guid profileId = createDocument.RootElement.GetProperty("id").GetGuid();

        using HttpResponseMessage updateResponse = await client.PutAsJsonAsync(
            $"/api/settings/naming-profiles/{profileId}",
            new
            {
                name = "WEB FLAC 24-bit",
                releaseFolderTemplate = "{releaseArtists} - {title} ({year}) [{source} {format} {bitDepth}]",
                trackFileTemplate = "{position} - {title}",
                trackFileWithArtistTemplate = "{position} - {trackArtists} - {title}",
                sortOrder = 55,
                isDefault = true,
                isActive = true
            });
        using JsonDocument updateDocument = await ReadJsonAsync(updateResponse);

        using HttpResponseMessage listResponse = await client.GetAsync("/api/settings/naming-profiles");
        using JsonDocument listDocument = await ReadJsonAsync(listResponse);

        using HttpRequestMessage deleteRequest = new(HttpMethod.Delete, $"/api/settings/naming-profiles/{profileId}");
        deleteRequest.Headers.Add("X-Cratebase-Confirm-Delete", $"naming-profile:{profileId}");
        using HttpResponseMessage deleteResponse = await client.SendAsync(deleteRequest);

        Assert.Equal(HttpStatusCode.OK, defaultListResponse.StatusCode);
        Assert.True(defaultList.RootElement.GetProperty("total").GetInt32() >= 1);
        JsonElement builtin = Assert.Single(
            defaultList.RootElement.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("isBuiltin").GetBoolean());
        Assert.True(builtin.GetProperty("isDefault").GetBoolean());
        Assert.Equal("{position2} {title}", builtin.GetProperty("trackFileTemplate").GetString());

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.True(createDocument.RootElement.GetProperty("isDefault").GetBoolean());
        Assert.False(createDocument.RootElement.GetProperty("isBuiltin").GetBoolean());

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.Equal("WEB FLAC 24-bit", updateDocument.RootElement.GetProperty("name").GetString());
        Assert.Equal(55, updateDocument.RootElement.GetProperty("sortOrder").GetInt32());

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.Equal(2, listDocument.RootElement.GetProperty("total").GetInt32());
        JsonElement defaultProfile = Assert.Single(
            listDocument.RootElement.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("isDefault").GetBoolean());
        Assert.Equal(profileId, defaultProfile.GetProperty("id").GetGuid());

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    [Fact(DisplayName = "Naming profile endpoints validate templates and protect builtins")]
    public async Task Naming_profile_endpoints_validate_templates_and_protect_builtins()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();

        using HttpResponseMessage invalidResponse = await client.PostAsJsonAsync(
            "/api/settings/naming-profiles",
            new
            {
                name = "Bad",
                releaseFolderTemplate = "{unknown}",
                trackFileTemplate = "{position2} {title}",
                trackFileWithArtistTemplate = "{position2} {trackArtists} - {title}",
                sortOrder = 10,
                isDefault = false,
                isActive = true
            });
        using JsonDocument invalidDocument = await ReadJsonAsync(invalidResponse);

        using HttpResponseMessage listResponse = await client.GetAsync("/api/settings/naming-profiles");
        using JsonDocument listDocument = await ReadJsonAsync(listResponse);
        Guid builtinId = Assert.Single(
            listDocument.RootElement.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("isBuiltin").GetBoolean())
            .GetProperty("id")
            .GetGuid();

        using HttpResponseMessage builtinUpdateResponse = await client.PutAsJsonAsync(
            $"/api/settings/naming-profiles/{builtinId}",
            new
            {
                name = "Edited builtin",
                releaseFolderTemplate = "{releaseArtists} - {title}",
                trackFileTemplate = "{position} {title}",
                trackFileWithArtistTemplate = "{position} {trackArtists} - {title}",
                sortOrder = 10,
                isDefault = true,
                isActive = true
            });
        using JsonDocument builtinUpdateDocument = await ReadJsonAsync(builtinUpdateResponse);

        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);
        Assert.Equal("naming_profile.template_token_invalid", invalidDocument.RootElement.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, builtinUpdateResponse.StatusCode);
        Assert.Equal("naming_profile.builtin_immutable", builtinUpdateDocument.RootElement.GetProperty("code").GetString());
    }

    [Fact(DisplayName = "Release naming overrides are scoped to the authenticated collection")]
    public async Task Release_naming_overrides_are_scoped_to_the_authenticated_collection()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        (HttpClient adminClient, HttpClient userClient) = await CreateAuthenticatedClientsAsync(host);

        Guid adminReleaseId = await CreateReleaseAsync(adminClient, "Admin Release");
        Guid userReleaseId = await CreateReleaseAsync(userClient, "User Release");
        Guid userProfileId = await CreateNamingProfileAsync(userClient, "User WEB");

        using HttpResponseMessage putResponse = await userClient.PutAsJsonAsync(
            $"/api/releases/{userReleaseId}/naming-override",
            new
            {
                namingProfileId = userProfileId,
                releaseFolderTemplate = "{releaseArtists} - {title} ({year}) [{source} {format} {bitDepth}]",
                trackFileTemplate = "{position} {title}",
                trackFileWithArtistTemplate = "{position} {trackArtists} - {title}",
                source = "WEB"
            });
        using JsonDocument putDocument = await ReadJsonAsync(putResponse);

        using HttpResponseMessage getResponse = await userClient.GetAsync($"/api/releases/{userReleaseId}/naming-override");
        using JsonDocument getDocument = await ReadJsonAsync(getResponse);

        using HttpResponseMessage foreignReleaseResponse = await userClient.PutAsJsonAsync(
            $"/api/releases/{adminReleaseId}/naming-override",
            new { source = "WEB" });
        using JsonDocument foreignReleaseDocument = await ReadJsonAsync(foreignReleaseResponse);

        using HttpResponseMessage foreignProfileResponse = await adminClient.PutAsJsonAsync(
            $"/api/releases/{adminReleaseId}/naming-override",
            new { namingProfileId = userProfileId, source = "WEB" });
        using JsonDocument foreignProfileDocument = await ReadJsonAsync(foreignProfileResponse);

        Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);
        Assert.Equal("WEB", putDocument.RootElement.GetProperty("source").GetString());
        Assert.Equal(userProfileId, putDocument.RootElement.GetProperty("namingProfileId").GetGuid());

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal("WEB", getDocument.RootElement.GetProperty("source").GetString());

        Assert.Equal(HttpStatusCode.NotFound, foreignReleaseResponse.StatusCode);
        Assert.Equal("release.not_found", foreignReleaseDocument.RootElement.GetProperty("code").GetString());

        Assert.Equal(HttpStatusCode.BadRequest, foreignProfileResponse.StatusCode);
        Assert.Equal("naming_profile.not_found", foreignProfileDocument.RootElement.GetProperty("code").GetString());
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

    private static async Task<Guid> CreateNamingProfileAsync(HttpClient client, string name)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/settings/naming-profiles",
            new
            {
                name,
                releaseFolderTemplate = "{releaseArtists} - {title} ({year}) [{source} {format} {bitDepth}]",
                trackFileTemplate = "{position} {title}",
                trackFileWithArtistTemplate = "{position} {trackArtists} - {title}",
                sortOrder = 50,
                isDefault = false,
                isActive = true
            });
        using JsonDocument document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
    }
}
