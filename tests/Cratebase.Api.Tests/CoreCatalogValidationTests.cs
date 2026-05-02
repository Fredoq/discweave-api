using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Cratebase.Api.Tests;

public sealed class CoreCatalogValidationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _postgres;

    public CoreCatalogValidationTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact(DisplayName = "Creating a release with a missing label returns a conflict")]
    public async Task Creating_a_release_with_a_missing_label_returns_a_conflict()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = host.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/releases",
            new { title = "Technique", type = "album", labelId = Guid.CreateVersion7(), year = 1989 });
        using JsonDocument document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("release.label_conflict", document.RootElement.GetProperty("code").GetString());
    }

    [Fact(DisplayName = "Updating a release with a missing label returns a conflict")]
    public async Task Updating_a_release_with_a_missing_label_returns_a_conflict()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = host.CreateClient();
        Guid releaseId = await CreateReleaseAsync(client);

        using HttpResponseMessage response = await client.PutAsJsonAsync(
            $"/api/releases/{releaseId}",
            new { title = "Technique", type = "album", labelId = Guid.CreateVersion7(), year = 1989 });
        using JsonDocument document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("release.label_conflict", document.RootElement.GetProperty("code").GetString());
    }

    [Fact(DisplayName = "Creating a track with malformed text returns a validation error")]
    public async Task Creating_a_track_with_malformed_text_returns_a_validation_error()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = host.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/tracks",
            new { title = (string?)null, durationSeconds = 316 });
        using JsonDocument document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("track.title_required", document.RootElement.GetProperty("code").GetString());
    }

    [Fact(DisplayName = "Updating a track with malformed text returns a validation error")]
    public async Task Updating_a_track_with_malformed_text_returns_a_validation_error()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = host.CreateClient();
        Guid trackId = await CreateTrackAsync(client);

        using HttpResponseMessage response = await client.PutAsJsonAsync(
            $"/api/tracks/{trackId}",
            new { title = (string?)null, durationSeconds = 316 });
        using JsonDocument document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("track.title_required", document.RootElement.GetProperty("code").GetString());
    }

    private static async Task<Guid> CreateReleaseAsync(HttpClient client)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/releases", new { title = "Technique", type = "album", year = 1989 });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateTrackAsync(HttpClient client)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/tracks", new { title = "Fine Time", durationSeconds = 250 });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
    }
}
