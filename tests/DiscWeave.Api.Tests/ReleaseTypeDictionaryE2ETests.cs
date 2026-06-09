using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace DiscWeave.Api.Tests;

public sealed class ReleaseTypeDictionaryE2ETests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly string[] ElectronicGenres = ["IDM", "Electronic"];
    private static readonly string[] LeftfieldGenres = ["Leftfield"];
    private static readonly string[] EngineerAssistantRoles = ["Engineer [Assistant]"];
    private static readonly string[] ProducerMixedByRoles = ["Producer, Mixed By"];

    [Fact(DisplayName = "Release entry create accepts a new release type code and adds it to the dictionary")]
    public async Task Release_entry_create_accepts_a_new_release_type_code_and_adds_it_to_the_dictionary()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        Guid artistId = await CreateArtistAsync(client, "New Order");

        using HttpResponseMessage createResponse = await client.PostAsJsonAsync(
            "/api/releases",
            new
            {
                title = "Blue Monday",
                type = "single",
                isVariousArtists = false,
                artistCredits = new object[] { new { artistId, role = "mainArtist" } },
                labels = new object[] { new { name = "Factory", catalogNumber = "FAC 73", hasNoCatalogNumber = false } },
                notOnLabel = false,
                year = 1983,
                genres = ElectronicGenres,
                tags = Array.Empty<string>(),
                tracklist = new object[]
                {
                    new
                    {
                        title = "Blue Monday",
                        position = 1,
                        durationSeconds = 449,
                        artistCredits = Array.Empty<object>(),
                        versionNote = (string?)null
                    }
                },
                ownedCopy = (object?)null
            });
        using JsonDocument createDocument = await ReadJsonAsync(createResponse);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.Equal("single", createDocument.RootElement.GetProperty("type").GetString());

        using HttpResponseMessage settingsResponse = await client.GetAsync("/api/settings/dictionaries?kind=releaseType");
        using JsonDocument settingsDocument = await ReadJsonAsync(settingsResponse);

        Assert.Equal(HttpStatusCode.OK, settingsResponse.StatusCode);
        JsonElement entry = settingsDocument.RootElement.GetProperty("items")
            .EnumerateArray()
            .Single(item => item.GetProperty("code").GetString() == "single");
        Assert.Equal("single", entry.GetProperty("name").GetString());
        Assert.True(entry.GetProperty("isActive").GetBoolean());
    }

    [Fact(DisplayName = "Release entry create accepts a new genre code and adds it to the dictionary")]
    public async Task Release_entry_create_accepts_a_new_genre_code_and_adds_it_to_the_dictionary()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        Guid artistId = await CreateArtistAsync(client, "The Orb");

        using HttpResponseMessage createResponse = await client.PostAsJsonAsync(
            "/api/releases",
            new
            {
                title = "Little Fluffy Clouds",
                type = "single",
                isVariousArtists = false,
                artistCredits = new object[] { new { artistId, role = "mainArtist" } },
                labels = new object[] { new { name = "Big Life", catalogNumber = "BLRCD 5", hasNoCatalogNumber = false } },
                notOnLabel = false,
                year = 1991,
                genres = LeftfieldGenres,
                tags = Array.Empty<string>(),
                tracklist = Array.Empty<object>(),
                ownedCopy = (object?)null
            });
        using JsonDocument createDocument = await ReadJsonAsync(createResponse);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.Equal("Leftfield", createDocument.RootElement.GetProperty("genres")[0].GetString());

        using HttpResponseMessage settingsResponse = await client.GetAsync("/api/settings/dictionaries?kind=genre");
        using JsonDocument settingsDocument = await ReadJsonAsync(settingsResponse);

        Assert.Equal(HttpStatusCode.OK, settingsResponse.StatusCode);
        JsonElement entry = settingsDocument.RootElement.GetProperty("items")
            .EnumerateArray()
            .Single(item => item.GetProperty("code").GetString() == "Leftfield");
        Assert.Equal("Leftfield", entry.GetProperty("name").GetString());
        Assert.True(entry.GetProperty("isActive").GetBoolean());
    }

    [Fact(DisplayName = "Release entry create accepts new credit role codes and adds them to the dictionary")]
    public async Task Release_entry_create_accepts_new_credit_role_codes_and_adds_them_to_the_dictionary()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        Guid releaseArtistId = await CreateArtistAsync(client, "The Orb");
        Guid trackArtistId = await CreateArtistAsync(client, "Youth");

        using HttpResponseMessage createResponse = await client.PostAsJsonAsync(
            "/api/releases",
            new
            {
                title = "Little Fluffy Clouds",
                type = "single",
                isVariousArtists = false,
                artistCredits = new object[] { new { artistId = releaseArtistId, role = "mainArtist" } },
                labels = new object[] { new { name = "Big Life", catalogNumber = "BLRCD 5", hasNoCatalogNumber = false } },
                notOnLabel = false,
                year = 1991,
                genres = LeftfieldGenres,
                tags = Array.Empty<string>(),
                tracklist = new object[]
                {
                    new
                    {
                        title = "Little Fluffy Clouds",
                        position = 1,
                        durationSeconds = 266,
                        artistCredits = new object[] { new { artistId = trackArtistId, roles = ProducerMixedByRoles } },
                        versionNote = (string?)null
                    }
                },
                ownedCopy = (object?)null
            });
        using JsonDocument createDocument = await ReadJsonAsync(createResponse);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        JsonElement trackCredit = createDocument.RootElement.GetProperty("tracklist")[0].GetProperty("artistCredits")[0];
        Assert.Equal("Youth", trackCredit.GetProperty("artistName").GetString());
        Assert.Equal("Producer", trackCredit.GetProperty("roles")[0].GetString());
        Assert.Equal("Mixed By", trackCredit.GetProperty("roles")[1].GetString());

        using HttpResponseMessage settingsResponse = await client.GetAsync("/api/settings/dictionaries?kind=creditRole");
        using JsonDocument settingsDocument = await ReadJsonAsync(settingsResponse);

        Assert.Equal(HttpStatusCode.OK, settingsResponse.StatusCode);
        string[] roleCodes =
        [
            .. settingsDocument.RootElement.GetProperty("items")
                .EnumerateArray()
                .Select(item => item.GetProperty("code").GetString()!)
        ];
        Assert.Contains("Producer", roleCodes);
        Assert.Contains("Mixed By", roleCodes);
    }

    [Fact(DisplayName = "Release entry create reuses a new credit role code repeated in the same request")]
    public async Task Release_entry_create_reuses_a_new_credit_role_code_repeated_in_the_same_request()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        Guid releaseArtistId = await CreateArtistAsync(client, "The Orb");
        Guid firstEngineerId = await CreateArtistAsync(client, "James Mit Flowers");
        Guid secondEngineerId = await CreateArtistAsync(client, "Maureen Mit Love");

        using HttpResponseMessage createResponse = await client.PostAsJsonAsync(
            "/api/releases",
            new
            {
                title = "Into the Fourth Dimension",
                type = "single",
                isVariousArtists = false,
                artistCredits = new object[] { new { artistId = releaseArtistId, role = "mainArtist" } },
                labels = new object[] { new { name = "Big Life", catalogNumber = "BLRCD 5", hasNoCatalogNumber = false } },
                notOnLabel = false,
                year = 1991,
                genres = LeftfieldGenres,
                tags = Array.Empty<string>(),
                tracklist = new object[]
                {
                    new
                    {
                        title = "Spanish Castles in Space",
                        position = 1,
                        durationSeconds = 906,
                        artistCredits = new object[] { new { artistId = firstEngineerId, roles = EngineerAssistantRoles } },
                        versionNote = (string?)null
                    },
                    new
                    {
                        title = "Into the Fourth Dimension",
                        position = 2,
                        durationSeconds = 556,
                        artistCredits = new object[] { new { artistId = secondEngineerId, roles = EngineerAssistantRoles } },
                        versionNote = (string?)null
                    }
                },
                ownedCopy = (object?)null
            });
        using JsonDocument createDocument = await ReadJsonAsync(createResponse);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.Equal("Engineer [Assistant]", createDocument.RootElement.GetProperty("tracklist")[0].GetProperty("artistCredits")[0].GetProperty("roles")[0].GetString());
        Assert.Equal("Engineer [Assistant]", createDocument.RootElement.GetProperty("tracklist")[1].GetProperty("artistCredits")[0].GetProperty("roles")[0].GetString());

        using HttpResponseMessage settingsResponse = await client.GetAsync("/api/settings/dictionaries?kind=creditRole");
        using JsonDocument settingsDocument = await ReadJsonAsync(settingsResponse);

        Assert.Equal(HttpStatusCode.OK, settingsResponse.StatusCode);
        int matchingRoleCount = settingsDocument.RootElement.GetProperty("items")
            .EnumerateArray()
            .Count(item => item.GetProperty("code").GetString() == "Engineer [Assistant]");
        Assert.Equal(1, matchingRoleCount);
    }

    [Fact(DisplayName = "Track entry create accepts new credit role codes and adds them to the dictionary")]
    public async Task Track_entry_create_accepts_new_credit_role_codes_and_adds_them_to_the_dictionary()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        Guid artistId = await CreateArtistAsync(client, "Jimmy Cauty");

        using HttpResponseMessage createResponse = await client.PostAsJsonAsync(
            "/api/tracks",
            new
            {
                title = "A Huge Ever Growing Pulsating Brain",
                durationSeconds = 1123,
                genres = Array.Empty<string>(),
                tags = Array.Empty<string>(),
                credits = new object[] { new { artistId, roles = ProducerMixedByRoles } },
                releaseAppearances = Array.Empty<object>()
            });
        using JsonDocument createDocument = await ReadJsonAsync(createResponse);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        JsonElement credit = createDocument.RootElement.GetProperty("credits")[0];
        Assert.Equal("Jimmy Cauty", credit.GetProperty("artistName").GetString());
        Assert.Equal("Producer", credit.GetProperty("roles")[0].GetString());
        Assert.Equal("Mixed By", credit.GetProperty("roles")[1].GetString());

        using HttpResponseMessage settingsResponse = await client.GetAsync("/api/settings/dictionaries?kind=creditRole");
        using JsonDocument settingsDocument = await ReadJsonAsync(settingsResponse);

        Assert.Equal(HttpStatusCode.OK, settingsResponse.StatusCode);
        string[] roleCodes =
        [
            .. settingsDocument.RootElement.GetProperty("items")
                .EnumerateArray()
                .Select(item => item.GetProperty("code").GetString()!)
        ];
        Assert.Contains("Producer", roleCodes);
        Assert.Contains("Mixed By", roleCodes);
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
