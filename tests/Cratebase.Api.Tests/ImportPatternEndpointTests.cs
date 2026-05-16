using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Cratebase.Api.Tests;

public sealed class ImportPatternEndpointTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _postgres;

    public ImportPatternEndpointTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact(DisplayName = "Import patterns list defaults and validate kind filters")]
    public async Task Import_patterns_list_defaults_and_validate_kind_filters()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();

        using HttpResponseMessage invalidResponse = await client.GetAsync("/api/settings/import-patterns?kind=bad");
        using JsonDocument invalidDocument = await ReadJsonAsync(invalidResponse);
        using HttpResponseMessage listResponse = await client.GetAsync("/api/settings/import-patterns?kind=trackFile");
        using JsonDocument listDocument = await ReadJsonAsync(listResponse);

        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);
        Assert.Equal("import_pattern.kind_invalid", invalidDocument.RootElement.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.Equal(4, listDocument.RootElement.GetProperty("total").GetInt32());
        Assert.All(
            listDocument.RootElement.GetProperty("items").EnumerateArray(),
            item =>
            {
                Assert.Equal("trackFile", item.GetProperty("kind").GetString());
                Assert.True(item.GetProperty("isBuiltin").GetBoolean());
            });
    }

    [Fact(DisplayName = "Import pattern CRUD and test preview use structured validation")]
    public async Task Import_pattern_crud_and_test_preview_use_structured_validation()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();

        using HttpResponseMessage createResponse = await client.PostAsJsonAsync(
            "/api/settings/import-patterns",
            new
            {
                kind = "trackFile",
                template = "{position}. {title}",
                sortOrder = 55,
                isActive = true
            });
        using JsonDocument createDocument = await ReadJsonAsync(createResponse);
        Guid patternId = createDocument.RootElement.GetProperty("id").GetGuid();

        using HttpResponseMessage testResponse = await client.PostAsJsonAsync(
            "/api/settings/import-patterns/test",
            new
            {
                kind = "trackFile",
                template = "{position}. {title}",
                input = "09. Balance.flac"
            });
        using JsonDocument testDocument = await ReadJsonAsync(testResponse);

        using HttpResponseMessage immutableResponse = await client.PutAsJsonAsync(
            $"/api/settings/import-patterns/{patternId}",
            new
            {
                kind = "releaseFolder",
                template = "{position}. {title}",
                sortOrder = 60,
                isActive = true
            });
        using JsonDocument immutableDocument = await ReadJsonAsync(immutableResponse);

        using HttpResponseMessage updateResponse = await client.PutAsJsonAsync(
            $"/api/settings/import-patterns/{patternId}",
            new
            {
                kind = "trackFile",
                template = "{position}. {title}",
                sortOrder = 60,
                isActive = false
            });
        using JsonDocument updateDocument = await ReadJsonAsync(updateResponse);
        using HttpResponseMessage deleteResponse = await client.DeleteAsync($"/api/settings/import-patterns/{patternId}");

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, testResponse.StatusCode);
        Assert.True(testDocument.RootElement.GetProperty("matched").GetBoolean());
        Assert.Equal("9", testDocument.RootElement.GetProperty("fields").GetProperty("position").GetString());
        Assert.Equal("Balance", testDocument.RootElement.GetProperty("fields").GetProperty("title").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, immutableResponse.StatusCode);
        Assert.Equal("import_pattern.kind_immutable", immutableDocument.RootElement.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.Equal(60, updateDocument.RootElement.GetProperty("sortOrder").GetInt32());
        Assert.False(updateDocument.RootElement.GetProperty("isActive").GetBoolean());
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        Stream stream = await response.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(stream);
    }
}
