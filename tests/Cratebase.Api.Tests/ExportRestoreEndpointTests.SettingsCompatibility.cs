using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Cratebase.Api.Tests;

public sealed partial class ExportRestoreEndpointTests
{
    [Fact(DisplayName = "JSON restore treats missing v1 settings arrays as empty")]
    public async Task Json_restore_treats_missing_v1_settings_arrays_as_empty()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient adminClient = await host.CreateAuthenticatedClientAsync();
        JsonObject snapshot = JsonNode.Parse(await CreateSnapshotAsync(adminClient))!.AsObject();
        _ = snapshot.Remove("namingProfiles");
        _ = snapshot.Remove("tagRoleMappings");
        _ = snapshot.Remove("releaseNamingOverrides");
        HttpClient userClient = await CreateUserClientAsync(host, adminClient);

        using HttpResponseMessage restoreResponse = await PostRestoreAsync(userClient, snapshot.ToJsonString());
        string restoreJson = await restoreResponse.Content.ReadAsStringAsync();

        Assert.True(restoreResponse.StatusCode == HttpStatusCode.OK, restoreJson);
        using var restoreDocument = JsonDocument.Parse(restoreJson);
        Assert.Equal(0, restoreDocument.RootElement.GetProperty("namingProfiles").GetInt32());
        Assert.Equal(0, restoreDocument.RootElement.GetProperty("tagRoleMappings").GetInt32());
        Assert.Equal(0, restoreDocument.RootElement.GetProperty("releaseNamingOverrides").GetInt32());
    }
}
