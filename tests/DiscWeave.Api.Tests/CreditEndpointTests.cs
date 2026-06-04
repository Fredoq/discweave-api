using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace DiscWeave.Api.Tests;

public sealed class CreditEndpointTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _postgres;

    public CreditEndpointTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact(DisplayName = "Credit endpoints support create read list update and delete")]
    public async Task Credit_endpoints_support_create_read_list_update_and_delete()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        Guid artistId = await CreateArtistAsync(client, "Arthur Baker");
        Guid releaseId = await CreateReleaseAsync(client, "Confusion");
        Guid trackId = await CreateTrackAsync(client, "Confusion (Instrumental)");

        using HttpResponseMessage createResponse = await client.PostAsJsonAsync(
            "/api/credits",
            new { contributorArtistId = artistId, targetType = "release", targetId = releaseId, roles = Roles("producer") });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        using JsonDocument createDocument = await ReadJsonAsync(createResponse);
        Guid creditId = createDocument.RootElement.GetProperty("id").GetGuid();

        using HttpResponseMessage getResponse = await client.GetAsync($"/api/credits/{creditId}");
        using JsonDocument getDocument = await ReadJsonAsync(getResponse);

        using HttpResponseMessage updateResponse = await client.PutAsJsonAsync(
            $"/api/credits/{creditId}",
            new { contributorArtistId = artistId, targetType = "track", targetId = trackId, roles = Roles("remixer") });
        using JsonDocument updateDocument = await ReadJsonAsync(updateResponse);

        using HttpResponseMessage listResponse = await client.GetAsync(
            $"/api/credits?contributorArtistId={artistId}&targetType=track&targetId={trackId}&role=remixer&limit=10&offset=0");
        using JsonDocument listDocument = await ReadJsonAsync(listResponse);

        using HttpRequestMessage deleteRequest = new(HttpMethod.Delete, $"/api/credits/{creditId}");
        deleteRequest.Headers.Add("X-DiscWeave-Confirm-Delete", $"credit:{creditId}");
        using HttpResponseMessage deleteResponse = await client.SendAsync(deleteRequest);

        Assert.Equal("producer", createDocument.RootElement.GetProperty("role").GetString());
        Assert.Collection(
            createDocument.RootElement.GetProperty("roles").EnumerateArray().Select(role => role.GetString()),
            role => Assert.Equal("producer", role));
        Assert.Equal("release", createDocument.RootElement.GetProperty("targetType").GetString());
        Assert.Equal(artistId, createDocument.RootElement.GetProperty("contributorArtistId").GetGuid());
        Assert.Equal("Arthur Baker", createDocument.RootElement.GetProperty("contributorName").GetString());
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal(creditId, getDocument.RootElement.GetProperty("id").GetGuid());
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.Equal("track", updateDocument.RootElement.GetProperty("targetType").GetString());
        Assert.Equal(trackId, updateDocument.RootElement.GetProperty("targetId").GetGuid());
        Assert.Equal("remixer", updateDocument.RootElement.GetProperty("role").GetString());
        Assert.Collection(
            updateDocument.RootElement.GetProperty("roles").EnumerateArray().Select(role => role.GetString()),
            role => Assert.Equal("remixer", role));
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.Equal(1, listDocument.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(creditId, listDocument.RootElement.GetProperty("items")[0].GetProperty("id").GetGuid());
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    [Fact(DisplayName = "Credit endpoints support multiple roles on one credit")]
    public async Task Credit_endpoints_support_multiple_roles_on_one_credit()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        Guid artistId = await CreateArtistAsync(client, "Jimmy Cauty");
        Guid trackId = await CreateTrackAsync(client, "Huge Ever Growing Pulsating Brain");
        string[] requestedRoles = ["engineer", "producer", "composer"];

        using HttpResponseMessage createResponse = await client.PostAsJsonAsync(
            "/api/credits",
            new
            {
                contributorArtistId = artistId,
                targetType = "track",
                targetId = trackId,
                roles = requestedRoles
            });
        using JsonDocument createDocument = await ReadJsonAsync(createResponse);

        using HttpResponseMessage listResponse = await client.GetAsync(
            $"/api/credits?contributorArtistId={artistId}&targetType=track&targetId={trackId}&role=producer&limit=10&offset=0");
        using JsonDocument listDocument = await ReadJsonAsync(listResponse);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.Equal("engineer", createDocument.RootElement.GetProperty("role").GetString());
        AssertCreditRoles(createDocument.RootElement.GetProperty("roles"));
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.Equal(1, listDocument.RootElement.GetProperty("total").GetInt32());
        AssertCreditRoles(listDocument.RootElement.GetProperty("items")[0].GetProperty("roles"));
    }

    private static void AssertCreditRoles(JsonElement roles)
    {
        Assert.Collection(
            roles.EnumerateArray().Select(role => role.GetString()),
            role => Assert.Equal("engineer", role),
            role => Assert.Equal("producer", role),
            role => Assert.Equal("composer", role));
    }

    private static string[] Roles(string role)
    {
        return [role];
    }

    [Fact(DisplayName = "Creating a credit for a missing target returns a conflict")]
    public async Task Creating_a_credit_for_a_missing_target_returns_a_conflict()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        Guid artistId = await CreateArtistAsync(client, "Arthur Baker");

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/credits",
            new { contributorArtistId = artistId, targetType = "release", targetId = Guid.CreateVersion7(), roles = Roles("producer") });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal("credit.target_conflict", document.RootElement.GetProperty("code").GetString());
    }

    [Fact(DisplayName = "Credit endpoints validate references and request codes")]
    public async Task Credit_endpoints_validate_references_and_request_codes()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        Guid artistId = await CreateArtistAsync(client, "Arthur Baker");
        Guid releaseId = await CreateReleaseAsync(client, "Confusion");
        var missingArtistId = Guid.CreateVersion7();

        using HttpResponseMessage missingContributorResponse = await client.PostAsJsonAsync(
            "/api/credits",
            new { contributorArtistId = missingArtistId, targetType = "release", targetId = releaseId, roles = Roles("producer") });
        using JsonDocument missingContributorDocument = await ReadJsonAsync(missingContributorResponse);

        using HttpResponseMessage invalidTargetResponse = await client.PostAsJsonAsync(
            "/api/credits",
            new { contributorArtistId = artistId, targetType = "video", targetId = releaseId, roles = Roles("producer") });
        using JsonDocument invalidTargetDocument = await ReadJsonAsync(invalidTargetResponse);

        using HttpResponseMessage invalidRoleResponse = await client.PostAsJsonAsync(
            "/api/credits",
            new { contributorArtistId = artistId, targetType = "release", targetId = releaseId, roles = Roles("sleeveDesigner") });
        using JsonDocument invalidRoleDocument = await ReadJsonAsync(invalidRoleResponse);

        Assert.Equal(HttpStatusCode.Conflict, missingContributorResponse.StatusCode);
        Assert.Equal("credit.contributor_conflict", missingContributorDocument.RootElement.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, invalidTargetResponse.StatusCode);
        Assert.Equal("credit.target_type_invalid", invalidTargetDocument.RootElement.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, invalidRoleResponse.StatusCode);
        Assert.Equal("credit.role_invalid", invalidRoleDocument.RootElement.GetProperty("code").GetString());
    }

    [Fact(DisplayName = "Credit list returns a validation error for invalid role filters")]
    public async Task Credit_list_returns_a_validation_error_for_invalid_role_filters()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();

        using HttpResponseMessage response = await client.GetAsync("/api/credits?role=sleeveDesigner&limit=10&offset=0");
        using JsonDocument document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("credit.role_invalid", document.RootElement.GetProperty("code").GetString());
    }

    [Fact(DisplayName = "Credit endpoints expose supported role codes")]
    public async Task Credit_endpoints_expose_supported_role_codes()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        Guid artistId = await CreateArtistAsync(client, "New Order", "group");
        Guid releaseId = await CreateReleaseAsync(client, "Technique");
        string[] roles = ["mainArtist", "featuredArtist", "composer", "performer", "engineer"];

        foreach (string role in roles)
        {
            using HttpResponseMessage response = await client.PostAsJsonAsync(
                "/api/credits",
                new { contributorArtistId = artistId, targetType = "release", targetId = releaseId, roles = Roles(role) });
            using JsonDocument document = await ReadJsonAsync(response);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            Assert.Equal(role, document.RootElement.GetProperty("role").GetString());
        }
    }

    private static async Task<Guid> CreateArtistAsync(HttpClient client, string name)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/artists", new { type = "person", name });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateArtistAsync(HttpClient client, string name, string type)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/artists", new { type, name });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateReleaseAsync(HttpClient client, string title)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/releases", new { title, type = "standalone", isVariousArtists = true });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateTrackAsync(HttpClient client, string title)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/tracks", new { title });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
    }
}
