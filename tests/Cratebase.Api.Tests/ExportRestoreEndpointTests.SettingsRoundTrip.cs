using System.Net;
using System.Text.Json;

namespace Cratebase.Api.Tests;

public sealed partial class ExportRestoreEndpointTests
{
    [Fact(DisplayName = "JSON export and restore include naming profiles and release overrides")]
    public async Task Json_export_and_restore_include_naming_profiles_and_release_overrides()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient adminClient = await host.CreateAuthenticatedClientAsync();
        Guid labelId = await CreateLabelAsync(adminClient, "Factory Records");
        Guid artistId = await CreateArtistAsync(adminClient, "New Order");
        Guid releaseId = await CreateReleaseWithTrackAsync(adminClient, labelId, artistId);
        Guid profileId = await CreateNamingProfileAsync(adminClient, "WEB FLAC 24-bit");
        await PutReleaseNamingOverrideAsync(adminClient, releaseId, profileId);

        string snapshot = await ExportJsonAsync(adminClient);
        using var snapshotDocument = JsonDocument.Parse(snapshot);
        JsonElement namingProfiles = snapshotDocument.RootElement.GetProperty("namingProfiles");
        JsonElement releaseNamingOverrides = snapshotDocument.RootElement.GetProperty("releaseNamingOverrides");
        int profileCount = namingProfiles.GetArrayLength();
        JsonElement profile = namingProfiles.EnumerateArray().Single(item => item.GetProperty("id").GetGuid() == profileId);
        JsonElement overrideEntry = Assert.Single(releaseNamingOverrides.EnumerateArray());
        HttpClient userClient = await CreateUserClientAsync(host, adminClient);

        using HttpResponseMessage restoreResponse = await PostRestoreAsync(userClient, snapshot);
        string restoreJson = await restoreResponse.Content.ReadAsStringAsync();
        using var restoreDocument = JsonDocument.Parse(restoreJson);
        string restoredSnapshot = await ExportJsonAsync(userClient);
        using var restoredSnapshotDocument = JsonDocument.Parse(restoredSnapshot);
        JsonElement restoredNamingProfiles = restoredSnapshotDocument.RootElement.GetProperty("namingProfiles");
        JsonElement restoredReleaseNamingOverrides = restoredSnapshotDocument.RootElement.GetProperty("releaseNamingOverrides");
        JsonElement restoredProfile = restoredNamingProfiles.EnumerateArray().Single(item => item.GetProperty("id").GetGuid() == profileId);
        JsonElement restoredOverride = Assert.Single(restoredReleaseNamingOverrides.EnumerateArray());

        Assert.True(restoreResponse.StatusCode == HttpStatusCode.OK, restoreJson);
        Assert.Equal("WEB FLAC 24-bit", profile.GetProperty("name").GetString());
        Assert.Equal("{releaseArtists} - {title} ({year}) [{source} {format} {bitDepth}]", profile.GetProperty("releaseFolderTemplate").GetString());
        Assert.Equal(releaseId, overrideEntry.GetProperty("releaseId").GetGuid());
        Assert.Equal(profileId, overrideEntry.GetProperty("namingProfileId").GetGuid());
        Assert.Equal("WEB", overrideEntry.GetProperty("source").GetString());
        Assert.Equal(profileCount, restoreDocument.RootElement.GetProperty("namingProfiles").GetInt32());
        Assert.Equal(1, restoreDocument.RootElement.GetProperty("releaseNamingOverrides").GetInt32());
        Assert.Equal(profileCount, restoredNamingProfiles.GetArrayLength());
        Assert.Equal("WEB FLAC 24-bit", restoredProfile.GetProperty("name").GetString());
        Assert.Equal("{releaseArtists} - {title} ({year}) [{source} {format} {bitDepth}]", restoredProfile.GetProperty("releaseFolderTemplate").GetString());
        Assert.Equal(releaseId, restoredOverride.GetProperty("releaseId").GetGuid());
        Assert.Equal(profileId, restoredOverride.GetProperty("namingProfileId").GetGuid());
        Assert.Equal("WEB", restoredOverride.GetProperty("source").GetString());
    }
}
