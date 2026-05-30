using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace DiscWeave.Api.Tests;

public sealed class TagRoleMappingEndpointTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _postgres;

    public TagRoleMappingEndpointTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact(DisplayName = "Tag role mapping endpoints expose defaults and manage custom mappings")]
    public async Task Tag_role_mapping_endpoints_expose_defaults_and_manage_custom_mappings()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();

        using HttpResponseMessage defaultListResponse = await client.GetAsync("/api/settings/tag-role-mappings");
        using JsonDocument defaultList = await ReadJsonAsync(defaultListResponse);

        using HttpResponseMessage createRoleResponse = await client.PostAsJsonAsync(
            "/api/settings/dictionaries",
            new { kind = "creditRole", code = "arranger", name = "Arranger", sortOrder = 80, isActive = true });
        _ = createRoleResponse.EnsureSuccessStatusCode();

        using HttpResponseMessage createResponse = await client.PostAsJsonAsync(
            "/api/settings/tag-role-mappings",
            new { creditRoleCode = "arranger", tagField = "ARRANGER", sortOrder = 80, isActive = true });
        using JsonDocument createDocument = await ReadJsonAsync(createResponse);
        Guid mappingId = createDocument.RootElement.GetProperty("id").GetGuid();

        using HttpResponseMessage updateResponse = await client.PutAsJsonAsync(
            $"/api/settings/tag-role-mappings/{mappingId}",
            new { creditRoleCode = "arranger", tagField = "producer", sortOrder = 85, isActive = false });
        using JsonDocument updateDocument = await ReadJsonAsync(updateResponse);

        using HttpRequestMessage deleteRequest = new(HttpMethod.Delete, $"/api/settings/tag-role-mappings/{mappingId}");
        deleteRequest.Headers.Add("X-DiscWeave-Confirm-Delete", $"tag-role-mapping:{mappingId}");
        using HttpResponseMessage deleteResponse = await client.SendAsync(deleteRequest);

        Assert.Equal(HttpStatusCode.OK, defaultListResponse.StatusCode);
        JsonElement defaults = defaultList.RootElement.GetProperty("items");
        Assert.Contains(defaults.EnumerateArray(), mapping =>
            mapping.GetProperty("creditRoleCode").GetString() == "producer" &&
            mapping.GetProperty("tagField").GetString() == "producer" &&
            mapping.GetProperty("isBuiltin").GetBoolean());

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.Equal("arranger", createDocument.RootElement.GetProperty("creditRoleCode").GetString());
        Assert.Equal("ARRANGER", createDocument.RootElement.GetProperty("tagField").GetString());
        Assert.False(createDocument.RootElement.GetProperty("isBuiltin").GetBoolean());

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.Equal("producer", updateDocument.RootElement.GetProperty("tagField").GetString());
        Assert.Equal(85, updateDocument.RootElement.GetProperty("sortOrder").GetInt32());
        Assert.False(updateDocument.RootElement.GetProperty("isActive").GetBoolean());

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    [Fact(DisplayName = "Tag role mappings validate roles fields and protect builtin deletes")]
    public async Task Tag_role_mappings_validate_roles_fields_and_protect_builtin_deletes()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();

        using HttpResponseMessage invalidRoleResponse = await client.PostAsJsonAsync(
            "/api/settings/tag-role-mappings",
            new { creditRoleCode = "missingRole", tagField = "producer", sortOrder = 100, isActive = true });
        using JsonDocument invalidRoleDocument = await ReadJsonAsync(invalidRoleResponse);

        using HttpResponseMessage invalidFieldResponse = await client.PostAsJsonAsync(
            "/api/settings/tag-role-mappings",
            new { creditRoleCode = "producer", tagField = "custom field", sortOrder = 100, isActive = true });
        using JsonDocument invalidFieldDocument = await ReadJsonAsync(invalidFieldResponse);

        using HttpResponseMessage listResponse = await client.GetAsync("/api/settings/tag-role-mappings");
        using JsonDocument listDocument = await ReadJsonAsync(listResponse);
        Guid builtinId = Assert.Single(
            listDocument.RootElement.GetProperty("items").EnumerateArray(),
            mapping => mapping.GetProperty("creditRoleCode").GetString() == "producer")
            .GetProperty("id")
            .GetGuid();

        using HttpRequestMessage deleteRequest = new(HttpMethod.Delete, $"/api/settings/tag-role-mappings/{builtinId}");
        deleteRequest.Headers.Add("X-DiscWeave-Confirm-Delete", $"tag-role-mapping:{builtinId}");
        using HttpResponseMessage deleteResponse = await client.SendAsync(deleteRequest);
        using JsonDocument deleteDocument = await ReadJsonAsync(deleteResponse);

        Assert.Equal(HttpStatusCode.BadRequest, invalidRoleResponse.StatusCode);
        Assert.Equal("tag_role_mapping.role_invalid", invalidRoleDocument.RootElement.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, invalidFieldResponse.StatusCode);
        Assert.Equal("tag_role_mapping.tag_field_invalid", invalidFieldDocument.RootElement.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, deleteResponse.StatusCode);
        Assert.Equal("tag_role_mapping.builtin_immutable", deleteDocument.RootElement.GetProperty("code").GetString());
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
    }
}
