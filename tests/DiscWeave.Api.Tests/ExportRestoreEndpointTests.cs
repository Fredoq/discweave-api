using System.Net;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DiscWeave.Api.Tests;

public sealed partial class ExportRestoreEndpointTests : IClassFixture<PostgresFixture>
{
    private const string RestoreConfirmationHeader = "X-DiscWeave-Confirm-Restore";
    private const string RestoreConfirmationValue = "restore-empty-collection";
    private static readonly string[] PostPunkGenres = ["Post-punk"];
    private static readonly string[] FactoryTags = ["factory", "classic"];
    private static readonly string[] OwnedStatuses = ["owned"];
    private static readonly string[] ReleaseTargetTypes = ["release"];
    private static readonly string[] VinylMedia = ["vinyl"];
    private readonly PostgresFixture _postgres;

    public ExportRestoreEndpointTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact(DisplayName = "JSON restore rebuilds an empty collection and preserves public ids")]
    public async Task Json_restore_rebuilds_an_empty_collection_and_preserves_public_ids()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient adminClient = await host.CreateAuthenticatedClientAsync();
        Guid labelId = await CreateLabelAsync(adminClient, "Factory Records");
        Guid artistId = await CreateArtistAsync(adminClient, "New Order");
        Guid releaseId = await CreateReleaseWithTrackAsync(adminClient, labelId, artistId);
        _ = await CreateOwnedItemAsync(adminClient, releaseId);
        string snapshot = await ExportJsonAsync(adminClient);
        HttpClient userClient = await CreateUserClientAsync(host, adminClient);

        using HttpResponseMessage restoreResponse = await PostRestoreAsync(userClient, snapshot);
        string restoreJson = await restoreResponse.Content.ReadAsStringAsync();
        string restoredSnapshot = await ExportJsonAsync(userClient);
        using var restoredDocument = JsonDocument.Parse(restoredSnapshot);

        Assert.True(restoreResponse.StatusCode == HttpStatusCode.OK, restoreJson);
        using var restoreDocument = JsonDocument.Parse(restoreJson);
        Assert.True(restoreDocument.RootElement.GetProperty("restored").GetBoolean());
        Assert.Equal(1, restoreDocument.RootElement.GetProperty("formatVersion").GetInt32());
        Assert.Equal(1, restoreDocument.RootElement.GetProperty("artists").GetInt32());
        Assert.Equal(1, restoreDocument.RootElement.GetProperty("labels").GetInt32());
        Assert.Equal(1, restoreDocument.RootElement.GetProperty("releases").GetInt32());
        Assert.Equal(1, restoreDocument.RootElement.GetProperty("tracks").GetInt32());
        Assert.Equal(1, restoreDocument.RootElement.GetProperty("ownedItems").GetInt32());
        Assert.DoesNotContain("collectionId", restoredSnapshot, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(artistId, Assert.Single(restoredDocument.RootElement.GetProperty("artists").EnumerateArray()).GetProperty("id").GetGuid());
        Assert.Equal(labelId, Assert.Single(restoredDocument.RootElement.GetProperty("labels").EnumerateArray()).GetProperty("id").GetGuid());
        Assert.Equal(releaseId, Assert.Single(restoredDocument.RootElement.GetProperty("releases").EnumerateArray()).GetProperty("id").GetGuid());
        JsonElement restoredTracklistItem = restoredDocument.RootElement.GetProperty("releases").EnumerateArray().Single().GetProperty("tracklist")[0];
        Assert.Equal("LP 1", restoredTracklistItem.GetProperty("disc").GetString());
        Assert.Equal("A", restoredTracklistItem.GetProperty("side").GetString());
        Assert.Contains("Age of Consent", restoredSnapshot, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "JSON restore accepts v1 release tracklists without disc and side")]
    public async Task Json_restore_accepts_v1_release_tracklists_without_disc_and_side()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient adminClient = await host.CreateAuthenticatedClientAsync();
        string snapshot = await CreateSnapshotAsync(adminClient);
        JsonObject document = JsonNode.Parse(snapshot)!.AsObject();
        JsonObject tracklistItem = document["releases"]!.AsArray()[0]!["tracklist"]!.AsArray()[0]!.AsObject();
        _ = tracklistItem.Remove("disc");
        _ = tracklistItem.Remove("side");
        HttpClient userClient = await CreateUserClientAsync(host, adminClient);

        using HttpResponseMessage restoreResponse = await PostRestoreAsync(userClient, document.ToJsonString());
        string restoreJson = await restoreResponse.Content.ReadAsStringAsync();

        Assert.True(restoreResponse.StatusCode == HttpStatusCode.OK, restoreJson);
    }

    [Fact(DisplayName = "JSON restore rebuilds settings playlists relations ratings and media variants")]
    public async Task Json_restore_rebuilds_settings_playlists_relations_ratings_and_media_variants()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient adminClient = await host.CreateAuthenticatedClientAsync();
        RichSnapshotIds ids = await CreateRichSnapshotDataAsync(adminClient);
        string snapshot = AddStoredCoverMetadata(await ExportJsonAsync(adminClient));
        HttpClient userClient = await CreateUserClientAsync(host, adminClient);

        using HttpResponseMessage restoreResponse = await PostRestoreAsync(userClient, snapshot);
        string restoreJson = await restoreResponse.Content.ReadAsStringAsync();
        string restoredSnapshot = await ExportJsonAsync(userClient);
        using var restoredDocument = JsonDocument.Parse(restoredSnapshot);

        Assert.True(restoreResponse.StatusCode == HttpStatusCode.OK, restoreJson);
        using var restoreDocument = JsonDocument.Parse(restoreJson);
        Assert.Equal(2, restoreDocument.RootElement.GetProperty("artists").GetInt32());
        Assert.Equal(1, restoreDocument.RootElement.GetProperty("labels").GetInt32());
        Assert.Equal(1, restoreDocument.RootElement.GetProperty("releases").GetInt32());
        Assert.Equal(2, restoreDocument.RootElement.GetProperty("tracks").GetInt32());
        Assert.Equal(5, restoreDocument.RootElement.GetProperty("ownedItems").GetInt32());
        Assert.Equal(2, restoreDocument.RootElement.GetProperty("playlists").GetInt32());
        Assert.Equal(2, restoreDocument.RootElement.GetProperty("artistRelations").GetInt32());
        Assert.Equal(1, restoreDocument.RootElement.GetProperty("trackRelations").GetInt32());
        Assert.Equal(1, restoreDocument.RootElement.GetProperty("ratings").GetInt32());
        Assert.Contains(ids.ManualPlaylistId.ToString(), restoredSnapshot, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(ids.SmartPlaylistId.ToString(), restoredSnapshot, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(ids.RatingId.ToString(), restoredSnapshot, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"releaseDate\":\"1983-05-02\"", restoredSnapshot, StringComparison.Ordinal);
        Assert.Contains("\"sourceType\":\"localUpload\"", restoredSnapshot, StringComparison.Ordinal);
        Assert.Contains("\"type\":\"digital\"", restoredSnapshot, StringComparison.Ordinal);
        Assert.Contains("\"type\":\"cd\"", restoredSnapshot, StringComparison.Ordinal);
        Assert.Contains("\"type\":\"cassette\"", restoredSnapshot, StringComparison.Ordinal);
        Assert.Contains("\"type\":\"other\"", restoredSnapshot, StringComparison.Ordinal);
        Assert.Contains("\"isActive\":false", restoredSnapshot, StringComparison.Ordinal);
        Assert.Equal(ids.GroupArtistId, restoredDocument.RootElement.GetProperty("artists").EnumerateArray()
            .Single(artist => artist.GetProperty("type").GetString() == "group")
            .GetProperty("id")
            .GetGuid());
    }

    [Fact(DisplayName = "JSON restore rejects populated collections")]
    public async Task Json_restore_rejects_populated_collections()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        Guid labelId = await CreateLabelAsync(client, "Factory Records");
        Guid artistId = await CreateArtistAsync(client, "New Order");
        _ = await CreateReleaseWithTrackAsync(client, labelId, artistId);
        string snapshot = await ExportJsonAsync(client);

        using HttpResponseMessage restoreResponse = await PostRestoreAsync(client, snapshot);
        using JsonDocument document = await ReadJsonAsync(restoreResponse);

        Assert.Equal(HttpStatusCode.Conflict, restoreResponse.StatusCode);
        Assert.Equal("export_restore.collection_not_empty", document.RootElement.GetProperty("code").GetString());
    }

    [Fact(DisplayName = "JSON restore requires an explicit confirmation header")]
    public async Task Json_restore_requires_an_explicit_confirmation_header()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient adminClient = await host.CreateAuthenticatedClientAsync();
        string snapshot = await CreateSnapshotAsync(adminClient);
        HttpClient userClient = await CreateUserClientAsync(host, adminClient);

        using HttpResponseMessage restoreResponse = await PostRestoreAsync(userClient, snapshot, confirm: false);
        using JsonDocument document = await ReadJsonAsync(restoreResponse);

        Assert.Equal(HttpStatusCode.BadRequest, restoreResponse.StatusCode);
        Assert.Equal("export_restore.confirmation_required", document.RootElement.GetProperty("code").GetString());
    }

    [Fact(DisplayName = "JSON restore rejects unsupported format versions")]
    public async Task Json_restore_rejects_unsupported_format_versions()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient adminClient = await host.CreateAuthenticatedClientAsync();
        JsonObject snapshot = JsonNode.Parse(await CreateSnapshotAsync(adminClient))!.AsObject();
        snapshot["formatVersion"] = 999;
        HttpClient userClient = await CreateUserClientAsync(host, adminClient);

        using HttpResponseMessage restoreResponse = await PostRestoreAsync(userClient, snapshot.ToJsonString());
        using JsonDocument document = await ReadJsonAsync(restoreResponse);

        Assert.Equal(HttpStatusCode.BadRequest, restoreResponse.StatusCode);
        Assert.Equal("export_restore.format_version_unsupported", document.RootElement.GetProperty("code").GetString());
    }

    [Fact(DisplayName = "JSON restore rejects invalid snapshot references")]
    public async Task Json_restore_rejects_invalid_snapshot_references()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient adminClient = await host.CreateAuthenticatedClientAsync();
        JsonObject snapshot = JsonNode.Parse(await CreateSnapshotAsync(adminClient))!.AsObject();
        JsonArray credits = snapshot["credits"]!.AsArray();
        credits[0]!["contributorArtistId"] = Guid.CreateVersion7();
        HttpClient userClient = await CreateUserClientAsync(host, adminClient);

        using HttpResponseMessage restoreResponse = await PostRestoreAsync(userClient, snapshot.ToJsonString());
        using JsonDocument document = await ReadJsonAsync(restoreResponse);

        Assert.Equal(HttpStatusCode.BadRequest, restoreResponse.StatusCode);
        Assert.Equal("export_restore.snapshot_invalid", document.RootElement.GetProperty("code").GetString());
    }

    [Fact(DisplayName = "JSON restore rejects invalid release dates")]
    public async Task Json_restore_rejects_invalid_release_dates()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient adminClient = await host.CreateAuthenticatedClientAsync();
        JsonObject snapshot = JsonNode.Parse(await CreateSnapshotAsync(adminClient))!.AsObject();
        JsonArray releases = snapshot["releases"]!.AsArray();
        releases[0]!["releaseDate"] = "not-a-date";
        HttpClient userClient = await CreateUserClientAsync(host, adminClient);

        using HttpResponseMessage restoreResponse = await PostRestoreAsync(userClient, snapshot.ToJsonString());
        using JsonDocument document = await ReadJsonAsync(restoreResponse);

        Assert.Equal(HttpStatusCode.BadRequest, restoreResponse.StatusCode);
        Assert.Equal("export_restore.snapshot_invalid", document.RootElement.GetProperty("code").GetString());
    }

    [Fact(DisplayName = "JSON restore treats database constraint failures as invalid snapshots")]
    public async Task Json_restore_treats_database_constraint_failures_as_invalid_snapshots()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient adminClient = await host.CreateAuthenticatedClientAsync();
        JsonObject snapshot = JsonNode.Parse(await CreateSnapshotAsync(adminClient))!.AsObject();
        JsonArray dictionaries = snapshot["dictionaries"]!.AsArray();
        JsonObject duplicateDictionary = JsonNode.Parse(dictionaries[0]!.ToJsonString())!.AsObject();
        duplicateDictionary["id"] = Guid.CreateVersion7();
        dictionaries.Add(duplicateDictionary);
        HttpClient userClient = await CreateUserClientAsync(host, adminClient);

        using HttpResponseMessage restoreResponse = await PostRestoreAsync(userClient, snapshot.ToJsonString());
        using JsonDocument document = await ReadJsonAsync(restoreResponse);

        Assert.Equal(HttpStatusCode.BadRequest, restoreResponse.StatusCode);
        Assert.Equal("export_restore.snapshot_invalid", document.RootElement.GetProperty("code").GetString());
    }

    [Fact(DisplayName = "JSON restore stays scoped to the authenticated collection")]
    public async Task Json_restore_stays_scoped_to_the_authenticated_collection()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient adminClient = await host.CreateAuthenticatedClientAsync();
        string snapshot = await CreateSnapshotAsync(adminClient);
        _ = await CreateArtistAsync(adminClient, "Admin Secret");
        HttpClient userClient = await CreateUserClientAsync(host, adminClient);

        using HttpResponseMessage restoreResponse = await PostRestoreAsync(userClient, snapshot);
        string restoreJson = await restoreResponse.Content.ReadAsStringAsync();
        _ = await CreateArtistAsync(userClient, "Collector Only");
        string adminSnapshot = await ExportJsonAsync(adminClient);
        string userSnapshot = await ExportJsonAsync(userClient);

        Assert.True(restoreResponse.StatusCode == HttpStatusCode.OK, restoreJson);
        Assert.Contains("Admin Secret", adminSnapshot, StringComparison.Ordinal);
        Assert.DoesNotContain("Collector Only", adminSnapshot, StringComparison.Ordinal);
        Assert.Contains("Collector Only", userSnapshot, StringComparison.Ordinal);
        Assert.DoesNotContain("Admin Secret", userSnapshot, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "Rich JSON and CSV exports stay scoped to the authenticated collection")]
    public async Task Rich_json_and_csv_exports_stay_scoped_to_the_authenticated_collection()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient adminClient = await host.CreateAuthenticatedClientAsync();
        _ = await CreateRichSnapshotDataAsync(adminClient);
        HttpClient userClient = await CreateUserClientAsync(host, adminClient);
        _ = await CreateArtistAsync(userClient, "Visible Export Artist");

        string userSnapshot = await ExportJsonAsync(userClient);
        using HttpResponseMessage csvResponse = await userClient.GetAsync("/api/exports/csv");
        Assert.Equal(HttpStatusCode.OK, csvResponse.StatusCode);
        await using Stream csvStream = await csvResponse.Content.ReadAsStreamAsync();
        using var archive = new ZipArchive(csvStream, ZipArchiveMode.Read);
        string csv = await ReadAllCsvEntriesAsync(archive);

        Assert.Contains("Visible Export Artist", userSnapshot, StringComparison.Ordinal);
        Assert.Contains("Visible Export Artist", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("Factory Records", userSnapshot, StringComparison.Ordinal);
        Assert.DoesNotContain("Restore sequence", userSnapshot, StringComparison.Ordinal);
        Assert.DoesNotContain("DAT safety copy", userSnapshot, StringComparison.Ordinal);
        Assert.DoesNotContain("Minimal Synth", userSnapshot, StringComparison.Ordinal);
        Assert.DoesNotContain("Factory Records", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("Restore sequence", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("DAT safety copy", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("Minimal Synth", csv, StringComparison.Ordinal);
    }

    private static async Task<string> ReadAllCsvEntriesAsync(ZipArchive archive)
    {
        var writer = new StringWriter();
        foreach (ZipArchiveEntry entry in archive.Entries.OrderBy(entry => entry.FullName, StringComparer.Ordinal))
        {
            await using Stream stream = entry.Open();
            using var reader = new StreamReader(stream);
            await writer.WriteLineAsync(await reader.ReadToEndAsync());
        }

        return writer.ToString();
    }
}
