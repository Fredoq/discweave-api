using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace DiscWeave.Api.Tests;

public sealed class ReleaseTypeDictionaryConcurrencyE2ETests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact(DisplayName = "Release entry create resolves concurrent new credit role codes idempotently")]
    public async Task Release_entry_create_resolves_concurrent_new_credit_role_codes_idempotently()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using ApiTestHost host = await ApiTestHost.CreateAsync(postgres, timeout.Token);
        HttpClient client = await host.CreateAuthenticatedClientAsync(timeout.Token);
        Guid artistId = await CreateArtistAsync(client, "The Orb", timeout.Token);
        const string role = "Concurrent Discogs Role";

        Task<HttpResponseMessage>[] createTasks =
        [
            client.PostAsJsonAsync("/api/releases", CreateReleasePayload("Concurrent Role A", artistId, role), timeout.Token),
            client.PostAsJsonAsync("/api/releases", CreateReleasePayload("Concurrent Role B", artistId, role), timeout.Token)
        ];
        HttpResponseMessage[] createResponses = await Task.WhenAll(createTasks).WaitAsync(timeout.Token);

        using HttpResponseMessage firstCreateResponse = createResponses[0];
        using HttpResponseMessage secondCreateResponse = createResponses[1];
        string firstCreateBody = await firstCreateResponse.Content.ReadAsStringAsync(timeout.Token);
        string secondCreateBody = await secondCreateResponse.Content.ReadAsStringAsync(timeout.Token);

        Assert.True(firstCreateResponse.StatusCode == HttpStatusCode.Created, firstCreateBody);
        Assert.True(secondCreateResponse.StatusCode == HttpStatusCode.Created, secondCreateBody);

        using HttpResponseMessage settingsResponse = await client.GetAsync("/api/settings/dictionaries?kind=creditRole", timeout.Token);
        using JsonDocument settingsDocument = await ReadJsonAsync(settingsResponse, timeout.Token);

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

    private static async Task<Guid> CreateArtistAsync(HttpClient client, string name, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/artists", new { type = "group", name }, cancellationToken);
        using JsonDocument document = await ReadJsonAsync(response, cancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }
}
