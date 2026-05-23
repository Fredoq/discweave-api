using System.Net;
using System.Net.Http.Json;
using System.IO.Compression;
using System.Text.Json;

namespace Cratebase.Api.Tests;

public sealed class ExportEndpointTests : IClassFixture<PostgresFixture>
{
    private static readonly string[] PostPunkGenres = ["Post-punk"];
    private static readonly string[] FactoryTags = ["factory", "classic"];
    private readonly PostgresFixture _postgres;

    public ExportEndpointTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact(DisplayName = "JSON export returns a portable snapshot for the current collection")]
    public async Task Json_export_returns_a_portable_snapshot_for_the_current_collection()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();

        Guid labelId = await CreateLabelAsync(client, "Factory Records");
        Guid artistId = await CreateArtistAsync(client, "New Order");
        Guid releaseId = await CreateReleaseWithTrackAsync(client, labelId, artistId);
        Guid ownedItemId = await CreateOwnedItemAsync(client, releaseId);

        using HttpResponseMessage response = await client.GetAsync("/api/exports/json");
        string json = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, json);

        using var document = JsonDocument.Parse(json);
        Assert.DoesNotContain("collectionId", json, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, document.RootElement.GetProperty("formatVersion").GetInt32());

        JsonElement artist = Assert.Single(document.RootElement.GetProperty("artists").EnumerateArray());
        Assert.Equal(artistId, artist.GetProperty("id").GetGuid());
        Assert.Equal("New Order", artist.GetProperty("name").GetString());

        JsonElement label = Assert.Single(document.RootElement.GetProperty("labels").EnumerateArray());
        Assert.Equal(labelId, label.GetProperty("id").GetGuid());
        Assert.Equal("Factory Records", label.GetProperty("name").GetString());

        JsonElement release = Assert.Single(document.RootElement.GetProperty("releases").EnumerateArray());
        Assert.Equal(releaseId, release.GetProperty("id").GetGuid());
        Assert.Equal("Power, Corruption & Lies", release.GetProperty("title").GetString());
        Assert.Equal("album", release.GetProperty("type").GetString());
        Assert.Equal(labelId, release.GetProperty("labelId").GetGuid());
        Assert.Equal(1983, release.GetProperty("year").GetInt32());
        AssertStringArray(PostPunkGenres, release.GetProperty("genres"));
        AssertStringArray(FactoryTags, release.GetProperty("tags"));
        JsonElement releaseCredit = Assert.Single(release.GetProperty("artistCredits").EnumerateArray());
        Assert.Equal(artistId, releaseCredit.GetProperty("artistId").GetGuid());
        Assert.Equal("mainArtist", releaseCredit.GetProperty("role").GetString());
        JsonElement tracklistItem = Assert.Single(release.GetProperty("tracklist").EnumerateArray());
        Assert.Equal("Age of Consent", tracklistItem.GetProperty("title").GetString());

        JsonElement track = Assert.Single(document.RootElement.GetProperty("tracks").EnumerateArray());
        Assert.Equal(tracklistItem.GetProperty("trackId").GetGuid(), track.GetProperty("id").GetGuid());
        Assert.Equal("Age of Consent", track.GetProperty("title").GetString());
        Assert.Equal(316, track.GetProperty("durationSeconds").GetInt32());

        JsonElement ownedItem = Assert.Single(document.RootElement.GetProperty("ownedItems").EnumerateArray());
        Assert.Equal(ownedItemId, ownedItem.GetProperty("id").GetGuid());
        Assert.Equal(releaseId, ownedItem.GetProperty("targetId").GetGuid());
        Assert.Equal("owned", ownedItem.GetProperty("status").GetString());
        Assert.Equal("vinyl", ownedItem.GetProperty("medium").GetProperty("type").GetString());
        Assert.Equal("nearMint", ownedItem.GetProperty("condition").GetString());
        Assert.Equal("Shelf A", ownedItem.GetProperty("storageLocation").GetString());
    }

    [Fact(DisplayName = "CSV export returns a zip with normalized portable tables")]
    public async Task Csv_export_returns_a_zip_with_normalized_portable_tables()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();

        Guid labelId = await CreateLabelAsync(client, "Factory Records");
        Guid artistId = await CreateArtistAsync(client, "New Order");
        Guid formulaArtistId = await CreateArtistAsync(client, "=2+2");
        Guid releaseId = await CreateReleaseWithTrackAsync(client, labelId, artistId);
        _ = await CreateOwnedItemAsync(client, releaseId);

        using HttpResponseMessage response = await client.GetAsync("/api/exports/csv");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/zip", response.Content.Headers.ContentType?.MediaType);

        await using Stream stream = await response.Content.ReadAsStreamAsync();
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        string[] names = [.. archive.Entries.Select(entry => entry.FullName).Order(StringComparer.Ordinal)];
        Assert.Equal(
            [
                "artist_relations.csv",
                "artists.csv",
                "credits.csv",
                "dictionaries.csv",
                "import_patterns.csv",
                "labels.csv",
                "owned_items.csv",
                "playlist_entries.csv",
                "playlists.csv",
                "rating_criteria.csv",
                "ratings.csv",
                "release_labels.csv",
                "release_tracklist.csv",
                "releases.csv",
                "track_relations.csv",
                "tracks.csv"
            ],
            names);

        string releasesCsv = await ReadEntryAsync(archive, "releases.csv");
        Assert.Contains("id,title,type,label_id,year,release_date,is_various_artists,not_on_label,genres,tags", releasesCsv);
        Assert.Contains($"{releaseId},\"Power, Corruption & Lies\",album,{labelId},1983,", releasesCsv);

        string artistsCsv = await ReadEntryAsync(archive, "artists.csv");
        Assert.Contains($"{artistId},person,New Order", artistsCsv);
        Assert.Contains($"{formulaArtistId},person,'=2+2", artistsCsv);

        string ownedItemsCsv = await ReadEntryAsync(archive, "owned_items.csv");
        Assert.Contains("target_type,target_id,status,medium_type,medium_description", ownedItemsCsv);
        Assert.Contains($"release,{releaseId},owned,vinyl,LP", ownedItemsCsv);
    }

    [Fact(DisplayName = "Exports only include the current user's collection data")]
    public async Task Exports_only_include_the_current_users_collection_data()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient adminClient = await host.CreateAuthenticatedClientAsync();
        using HttpResponseMessage createUserResponse = await adminClient.PostAsJsonAsync(
            "/api/admin/users",
            new CreateUserRequest("collector@example.com", "Password1!", false));
        Assert.Equal(HttpStatusCode.Created, createUserResponse.StatusCode);

        Guid adminLabelId = await CreateLabelAsync(adminClient, "Hidden Label");
        Guid adminArtistId = await CreateArtistAsync(adminClient, "Hidden Artist");
        _ = await CreateReleaseWithTrackAsync(adminClient, adminLabelId, adminArtistId);

        HttpClient userClient = host.CreateClient();
        using HttpResponseMessage loginResponse = await userClient.PostAsJsonAsync(
            "/api/auth/login",
            new AuthRequest("collector@example.com", "Password1!"));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        _ = await CreateArtistAsync(userClient, "Visible Artist");

        using HttpResponseMessage jsonResponse = await userClient.GetAsync("/api/exports/json");
        string json = await jsonResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, jsonResponse.StatusCode);
        Assert.Contains("Visible Artist", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Hidden Artist", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Power, Corruption & Lies", json, StringComparison.Ordinal);

        using HttpResponseMessage csvResponse = await userClient.GetAsync("/api/exports/csv");
        Assert.Equal(HttpStatusCode.OK, csvResponse.StatusCode);
        await using Stream csvStream = await csvResponse.Content.ReadAsStreamAsync();
        using var archive = new ZipArchive(csvStream, ZipArchiveMode.Read);
        string artistsCsv = await ReadEntryAsync(archive, "artists.csv");
        string releasesCsv = await ReadEntryAsync(archive, "releases.csv");
        Assert.Contains("Visible Artist", artistsCsv, StringComparison.Ordinal);
        Assert.DoesNotContain("Hidden Artist", artistsCsv, StringComparison.Ordinal);
        Assert.DoesNotContain("Power, Corruption & Lies", releasesCsv, StringComparison.Ordinal);
    }

    private static async Task<Guid> CreateLabelAsync(HttpClient client, string name)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/labels", new { name });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateArtistAsync(HttpClient client, string name)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/artists", new { type = "person", name });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateReleaseWithTrackAsync(HttpClient client, Guid labelId, Guid artistId)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/releases",
            new
            {
                title = "Power, Corruption & Lies",
                type = "album",
                isVariousArtists = false,
                labelId,
                year = 1983,
                genres = PostPunkGenres,
                tags = FactoryTags,
                artistCredits = new[] { new { artistId, role = "mainArtist" } },
                tracklist = new[] { new { title = "Age of Consent", position = 1, durationSeconds = 316 } }
            });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateOwnedItemAsync(HttpClient client, Guid releaseId)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/owned-items",
            new
            {
                targetType = "release",
                targetId = releaseId,
                status = "owned",
                medium = new { type = "vinyl", description = "LP" },
                condition = "nearMint",
                storageLocation = "Shelf A"
            });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
    }

    private static async Task<string> ReadEntryAsync(ZipArchive archive, string name)
    {
        ZipArchiveEntry entry = archive.GetEntry(name)
            ?? throw new InvalidOperationException($"CSV export entry was not found: {name}");
        await using Stream stream = entry.Open();
        using var reader = new StreamReader(stream);

        return await reader.ReadToEndAsync();
    }

    private static void AssertStringArray(IReadOnlyList<string> expected, JsonElement actual)
    {
        string[] actualValues = [.. actual.EnumerateArray().Select(value => value.GetString() ?? string.Empty)];
        Assert.Equal(expected.Order(StringComparer.Ordinal), actualValues.Order(StringComparer.Ordinal));
    }

    private sealed record AuthRequest(string Email, string Password);

    private sealed record CreateUserRequest(string Email, string Password, bool IsAdmin);
}
