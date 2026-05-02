using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Cratebase.Api.Tests;

public sealed class ArtistWorkflowE2ETests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _postgres;

    public ArtistWorkflowE2ETests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact(DisplayName = "Artist endpoints support the full cataloging workflow")]
    public async Task Artist_endpoints_support_the_full_cataloging_workflow()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();

        using HttpResponseMessage createResponse = await client.PostAsJsonAsync(
            "/api/artists",
            new CreateArtistRequest("person", "  Bernard Sumner  "));
        using JsonDocument createDocument = await ReadJsonAsync(createResponse);
        Guid artistId = createDocument.RootElement.GetProperty("id").GetGuid();

        using HttpResponseMessage getCreatedResponse = await client.GetAsync($"/api/artists/{artistId}");
        using JsonDocument getCreatedDocument = await ReadJsonAsync(getCreatedResponse);

        using HttpResponseMessage updateResponse = await client.PutAsJsonAsync(
            $"/api/artists/{artistId}",
            new UpdateArtistRequest("Bernard Dicken"));
        using JsonDocument updateDocument = await ReadJsonAsync(updateResponse);

        using HttpResponseMessage listResponse = await client.GetAsync("/api/artists?search=dicken&type=person&limit=10&offset=0");
        using JsonDocument listDocument = await ReadJsonAsync(listResponse);

        using HttpRequestMessage deleteRequest = new(HttpMethod.Delete, $"/api/artists/{artistId}");
        deleteRequest.Headers.Add("X-Cratebase-Confirm-Delete", $"artist:{artistId}");
        using HttpResponseMessage deleteResponse = await client.SendAsync(deleteRequest);

        using HttpResponseMessage getDeletedResponse = await client.GetAsync($"/api/artists/{artistId}");
        using JsonDocument getDeletedDocument = await ReadJsonAsync(getDeletedResponse);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotEqual(Guid.Empty, artistId);
        Assert.Equal("Bernard Sumner", createDocument.RootElement.GetProperty("name").GetString());

        Assert.Equal(HttpStatusCode.OK, getCreatedResponse.StatusCode);
        Assert.Equal(artistId, getCreatedDocument.RootElement.GetProperty("id").GetGuid());
        Assert.Equal("person", getCreatedDocument.RootElement.GetProperty("type").GetString());
        Assert.Equal("Bernard Sumner", getCreatedDocument.RootElement.GetProperty("name").GetString());

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.Equal(artistId, updateDocument.RootElement.GetProperty("id").GetGuid());
        Assert.Equal("person", updateDocument.RootElement.GetProperty("type").GetString());
        Assert.Equal("Bernard Dicken", updateDocument.RootElement.GetProperty("name").GetString());

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.Equal(1, listDocument.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(artistId, listDocument.RootElement.GetProperty("items")[0].GetProperty("id").GetGuid());
        Assert.Equal("Bernard Dicken", listDocument.RootElement.GetProperty("items")[0].GetProperty("name").GetString());

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, getDeletedResponse.StatusCode);
        Assert.Equal("artist.not_found", getDeletedDocument.RootElement.GetProperty("code").GetString());
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
    }
}
