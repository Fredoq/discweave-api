using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace DiscWeave.Api.Tests;

public sealed class ReleaseTypeDictionaryConcurrencyE2ETests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact(DisplayName = "Release entry create resolves concurrent new credit role codes idempotently")]
    public async Task Release_entry_create_resolves_concurrent_new_credit_role_codes_idempotently()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        Guid artistId = await CreateArtistAsync(client, "The Orb");
        const string role = "Concurrent Discogs Role";

        Task<HttpResponseMessage>[] createTasks =
        [
            client.PostAsJsonAsync("/api/releases", CreateReleasePayload("Concurrent Role A", artistId, role)),
            client.PostAsJsonAsync("/api/releases", CreateReleasePayload("Concurrent Role B", artistId, role))
        ];
        HttpResponseMessage[] createResponses = await Task.WhenAll(createTasks);

        using HttpResponseMessage firstCreateResponse = createResponses[0];
        using HttpResponseMessage secondCreateResponse = createResponses[1];
        string firstCreateBody = await firstCreateResponse.Content.ReadAsStringAsync();
        string secondCreateBody = await secondCreateResponse.Content.ReadAsStringAsync();

        Assert.True(firstCreateResponse.StatusCode == HttpStatusCode.Created, firstCreateBody);
        Assert.True(secondCreateResponse.StatusCode == HttpStatusCode.Created, secondCreateBody);

        using HttpResponseMessage settingsResponse = await client.GetAsync("/api/settings/dictionaries?kind=creditRole");
        using JsonDocument settingsDocument = await ReadJsonAsync(settingsResponse);

        Assert.Equal(HttpStatusCode.OK, settingsResponse.StatusCode);
        int matchingRoleCount = settingsDocument.RootElement.GetProperty("items")
            .EnumerateArray()
            .Count(item => item.GetProperty("code").GetString() == role);
        Assert.Equal(1, matchingRoleCount);
    }

    private static object CreateReleasePayload(string title, Guid artistId, string role)
    {
        return new
        {
            title,
            type = "single",
            isVariousArtists = false,
            artistCredits = new object[] { new { artistId, role = "mainArtist" } },
            labels = Array.Empty<object>(),
            notOnLabel = true,
            year = 1991,
            genres = Array.Empty<string>(),
            tags = Array.Empty<string>(),
            tracklist = new object[]
            {
                new
                {
                    title = $"{title} Track",
                    position = 1,
                    durationSeconds = 266,
                    artistCredits = new object[] { new { artistId, roles = new[] { role } } },
                    versionNote = (string?)null
                }
            },
            ownedCopy = (object?)null
        };
    }

    private static async Task<Guid> CreateArtistAsync(HttpClient client, string name)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/artists", new { type = "group", name });
        using JsonDocument document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        Stream stream = await response.Content.ReadAsStreamAsync();

        return await JsonDocument.ParseAsync(stream);
    }
}
