using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace DiscWeave.Api.Tests;

public sealed class RelationEndpointTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _postgres;

    public RelationEndpointTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact(DisplayName = "Artist relation endpoints support create read list update and delete")]
    public async Task Artist_relation_endpoints_support_create_read_list_update_and_delete()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        Guid artistId = await CreateArtistAsync(client, "Bernard Sumner");
        Guid groupId = await CreateArtistAsync(client, "New Order", "group");

        using HttpResponseMessage createResponse = await client.PostAsJsonAsync(
            "/api/artist-relations",
            new { sourceArtistId = artistId, targetArtistId = groupId, type = "memberOf", startYear = 1980 });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        using JsonDocument createDocument = await ReadJsonAsync(createResponse);
        Guid relationId = createDocument.RootElement.GetProperty("id").GetGuid();

        using HttpResponseMessage getResponse = await client.GetAsync($"/api/artist-relations/{relationId}");
        using JsonDocument getDocument = await ReadJsonAsync(getResponse);

        using HttpResponseMessage updateResponse = await client.PutAsJsonAsync(
            $"/api/artist-relations/{relationId}",
            new { sourceArtistId = artistId, targetArtistId = groupId, type = "collaboration", startYear = 1980, endYear = 1983 });
        using JsonDocument updateDocument = await ReadJsonAsync(updateResponse);

        using HttpResponseMessage selfUpdateResponse = await client.PutAsJsonAsync(
            $"/api/artist-relations/{relationId}",
            new { sourceArtistId = artistId, targetArtistId = artistId, type = "alias" });
        using JsonDocument selfUpdateDocument = await ReadJsonAsync(selfUpdateResponse);

        using HttpResponseMessage listResponse = await client.GetAsync($"/api/artist-relations?sourceArtistId={artistId}&type=collaboration&limit=10&offset=0");
        using JsonDocument listDocument = await ReadJsonAsync(listResponse);

        using HttpRequestMessage deleteRequest = new(HttpMethod.Delete, $"/api/artist-relations/{relationId}");
        deleteRequest.Headers.Add("X-DiscWeave-Confirm-Delete", $"artist-relation:{relationId}");
        using HttpResponseMessage deleteResponse = await client.SendAsync(deleteRequest);

        Assert.Equal("memberOf", createDocument.RootElement.GetProperty("type").GetString());
        Assert.Equal(1980, createDocument.RootElement.GetProperty("startYear").GetInt32());
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal(relationId, getDocument.RootElement.GetProperty("id").GetGuid());
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.Equal("collaboration", updateDocument.RootElement.GetProperty("type").GetString());
        Assert.Equal(1983, updateDocument.RootElement.GetProperty("endYear").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, selfUpdateResponse.StatusCode);
        Assert.Equal("artist_relation.self_relation", selfUpdateDocument.RootElement.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.Equal(1, listDocument.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(relationId, listDocument.RootElement.GetProperty("items")[0].GetProperty("id").GetGuid());
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    [Fact(DisplayName = "Track relation endpoints support create read list update and delete")]
    public async Task Track_relation_endpoints_support_create_read_list_update_and_delete()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        Guid remixId = await CreateTrackAsync(client, "Blue Monday (Hardfloor Mix)");
        Guid originalId = await CreateTrackAsync(client, "Blue Monday");

        using HttpResponseMessage createResponse = await client.PostAsJsonAsync(
            "/api/track-relations",
            new { sourceTrackId = remixId, targetTrackId = originalId, type = "remixOf" });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        using JsonDocument createDocument = await ReadJsonAsync(createResponse);
        Guid relationId = createDocument.RootElement.GetProperty("id").GetGuid();

        using HttpResponseMessage getResponse = await client.GetAsync($"/api/track-relations/{relationId}");
        using JsonDocument getDocument = await ReadJsonAsync(getResponse);

        using HttpResponseMessage updateResponse = await client.PutAsJsonAsync(
            $"/api/track-relations/{relationId}",
            new { sourceTrackId = remixId, targetTrackId = originalId, type = "versionOf" });
        using JsonDocument updateDocument = await ReadJsonAsync(updateResponse);

        using HttpResponseMessage selfUpdateResponse = await client.PutAsJsonAsync(
            $"/api/track-relations/{relationId}",
            new { sourceTrackId = remixId, targetTrackId = remixId, type = "versionOf" });
        using JsonDocument selfUpdateDocument = await ReadJsonAsync(selfUpdateResponse);

        using HttpResponseMessage listResponse = await client.GetAsync($"/api/track-relations?targetTrackId={originalId}&type=versionOf&limit=10&offset=0");
        using JsonDocument listDocument = await ReadJsonAsync(listResponse);

        using HttpRequestMessage deleteRequest = new(HttpMethod.Delete, $"/api/track-relations/{relationId}");
        deleteRequest.Headers.Add("X-DiscWeave-Confirm-Delete", $"track-relation:{relationId}");
        using HttpResponseMessage deleteResponse = await client.SendAsync(deleteRequest);

        Assert.Equal("remixOf", createDocument.RootElement.GetProperty("type").GetString());
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal(relationId, getDocument.RootElement.GetProperty("id").GetGuid());
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.Equal("versionOf", updateDocument.RootElement.GetProperty("type").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, selfUpdateResponse.StatusCode);
        Assert.Equal("track_relation.self_relation", selfUpdateDocument.RootElement.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.Equal(1, listDocument.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(relationId, listDocument.RootElement.GetProperty("items")[0].GetProperty("id").GetGuid());
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    [Fact(DisplayName = "Artist relation endpoints validate references and support alternate types")]
    public async Task Artist_relation_endpoints_validate_references_and_support_alternate_types()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        Guid artistId = await CreateArtistAsync(client, "Bernard Sumner");
        Guid aliasId = await CreateArtistAsync(client, "Electronic", "group");

        using HttpResponseMessage aliasResponse = await client.PostAsJsonAsync(
            "/api/artist-relations",
            new { sourceArtistId = artistId, targetArtistId = aliasId, type = "alias", endYear = 1999 });
        using JsonDocument aliasDocument = await ReadJsonAsync(aliasResponse);

        using HttpResponseMessage soloProjectResponse = await client.PostAsJsonAsync(
            "/api/artist-relations",
            new { sourceArtistId = aliasId, targetArtistId = artistId, type = "soloProject" });
        using JsonDocument soloProjectDocument = await ReadJsonAsync(soloProjectResponse);

        using HttpResponseMessage missingArtistResponse = await client.PostAsJsonAsync(
            "/api/artist-relations",
            new { sourceArtistId = artistId, targetArtistId = Guid.CreateVersion7(), type = "memberOf" });
        using JsonDocument missingArtistDocument = await ReadJsonAsync(missingArtistResponse);

        using HttpResponseMessage selfRelationResponse = await client.PostAsJsonAsync(
            "/api/artist-relations",
            new { sourceArtistId = artistId, targetArtistId = artistId, type = "alias" });
        using JsonDocument selfRelationDocument = await ReadJsonAsync(selfRelationResponse);

        using HttpResponseMessage invalidTypeResponse = await client.PostAsJsonAsync(
            "/api/artist-relations",
            new { sourceArtistId = artistId, targetArtistId = aliasId, type = "influencedBy" });
        using JsonDocument invalidTypeDocument = await ReadJsonAsync(invalidTypeResponse);

        Assert.Equal(HttpStatusCode.Created, aliasResponse.StatusCode);
        Assert.Equal("alias", aliasDocument.RootElement.GetProperty("type").GetString());
        Assert.Equal(1999, aliasDocument.RootElement.GetProperty("endYear").GetInt32());
        Assert.Equal(HttpStatusCode.Created, soloProjectResponse.StatusCode);
        Assert.Equal("soloProject", soloProjectDocument.RootElement.GetProperty("type").GetString());
        Assert.Equal(HttpStatusCode.Conflict, missingArtistResponse.StatusCode);
        Assert.Equal("artist_relation.artist_conflict", missingArtistDocument.RootElement.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, selfRelationResponse.StatusCode);
        Assert.Equal("artist_relation.self_relation", selfRelationDocument.RootElement.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, invalidTypeResponse.StatusCode);
        Assert.Equal("artist_relation.type_invalid", invalidTypeDocument.RootElement.GetProperty("code").GetString());
    }

    [Fact(DisplayName = "Artist relation list returns a validation error for invalid type filters")]
    public async Task Artist_relation_list_returns_a_validation_error_for_invalid_type_filters()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();

        using HttpResponseMessage response = await client.GetAsync("/api/artist-relations?type=influencedBy&limit=10&offset=0");
        using JsonDocument document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("artist_relation.type_invalid", document.RootElement.GetProperty("code").GetString());
    }

    [Fact(DisplayName = "Track relation endpoints validate references and support edit relations")]
    public async Task Track_relation_endpoints_validate_references_and_support_edit_relations()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        Guid editId = await CreateTrackAsync(client, "Blue Monday (Edit)");
        Guid originalId = await CreateTrackAsync(client, "Blue Monday");

        using HttpResponseMessage editResponse = await client.PostAsJsonAsync(
            "/api/track-relations",
            new { sourceTrackId = editId, targetTrackId = originalId, type = "editOf" });
        using JsonDocument editDocument = await ReadJsonAsync(editResponse);

        using HttpResponseMessage missingTrackResponse = await client.PostAsJsonAsync(
            "/api/track-relations",
            new { sourceTrackId = editId, targetTrackId = Guid.CreateVersion7(), type = "remixOf" });
        using JsonDocument missingTrackDocument = await ReadJsonAsync(missingTrackResponse);

        using HttpResponseMessage selfRelationResponse = await client.PostAsJsonAsync(
            "/api/track-relations",
            new { sourceTrackId = editId, targetTrackId = editId, type = "remixOf" });
        using JsonDocument selfRelationDocument = await ReadJsonAsync(selfRelationResponse);

        using HttpResponseMessage invalidTypeResponse = await client.PostAsJsonAsync(
            "/api/track-relations",
            new { sourceTrackId = editId, targetTrackId = originalId, type = "coverOf" });
        using JsonDocument invalidTypeDocument = await ReadJsonAsync(invalidTypeResponse);

        Assert.Equal(HttpStatusCode.Created, editResponse.StatusCode);
        Assert.Equal("editOf", editDocument.RootElement.GetProperty("type").GetString());
        Assert.Equal(HttpStatusCode.Conflict, missingTrackResponse.StatusCode);
        Assert.Equal("track_relation.track_conflict", missingTrackDocument.RootElement.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, selfRelationResponse.StatusCode);
        Assert.Equal("track_relation.self_relation", selfRelationDocument.RootElement.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, invalidTypeResponse.StatusCode);
        Assert.Equal("track_relation.type_invalid", invalidTypeDocument.RootElement.GetProperty("code").GetString());
    }

    [Fact(DisplayName = "Track relation list returns a validation error for invalid type filters")]
    public async Task Track_relation_list_returns_a_validation_error_for_invalid_type_filters()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();

        using HttpResponseMessage response = await client.GetAsync("/api/track-relations?type=coverOf&limit=10&offset=0");
        using JsonDocument document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("track_relation.type_invalid", document.RootElement.GetProperty("code").GetString());
    }

    private static async Task<Guid> CreateArtistAsync(HttpClient client, string name, string type = "person")
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/artists", new { type, name });
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
