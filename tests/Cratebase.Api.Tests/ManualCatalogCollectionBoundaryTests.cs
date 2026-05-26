using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Cratebase.Api.Tests;

public sealed class ManualCatalogCollectionBoundaryTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _postgres;

    public ManualCatalogCollectionBoundaryTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact(DisplayName = "Manual release writes reject cross collection labels credits and tracks")]
    public async Task Manual_release_writes_reject_cross_collection_labels_credits_and_tracks()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        (HttpClient adminClient, HttpClient userClient) = await CreateAuthenticatedClientsAsync(host);
        Guid adminArtistId = await CreateArtistAsync(adminClient, "Foreign Artist");
        Guid adminLabelId = await CreateLabelAsync(adminClient, "Foreign Label");
        Guid adminTrackId = await CreateTrackAsync(adminClient, "Foreign Track");
        Guid userArtistId = await CreateArtistAsync(userClient, "User Artist");
        Guid userReleaseId = await CreateReleaseAsync(userClient, "User Release", userArtistId);

        using JsonDocument createWithForeignLabel = await SendJsonAsync(
            adminLabelId,
            userClient.PostAsJsonAsync(
                "/api/releases",
                new { title = "Foreign Label Release", isVariousArtists = true, labelId = adminLabelId }),
            HttpStatusCode.Conflict);
        using JsonDocument createWithForeignCredit = await SendJsonAsync(
            adminArtistId,
            userClient.PostAsJsonAsync(
                "/api/releases",
                new
                {
                    title = "Foreign Credit Release",
                    isVariousArtists = false,
                    artistCredits = new[] { new { artistId = adminArtistId, role = "mainArtist" } }
                }),
            HttpStatusCode.BadRequest);
        using JsonDocument updateWithForeignTrack = await SendJsonAsync(
            adminTrackId,
            userClient.PutAsJsonAsync(
                $"/api/releases/{userReleaseId}",
                new
                {
                    title = "User Release",
                    isVariousArtists = false,
                    artistCredits = new[] { new { artistId = userArtistId, role = "mainArtist" } },
                    tracklist = new[] { new { trackId = adminTrackId, position = 1 } }
                }),
            HttpStatusCode.BadRequest);

        Assert.Equal("release.label_conflict", createWithForeignLabel.RootElement.GetProperty("code").GetString());
        Assert.Equal("release.artist_conflict", createWithForeignCredit.RootElement.GetProperty("code").GetString());
        Assert.Equal("release_track.track_conflict", updateWithForeignTrack.RootElement.GetProperty("code").GetString());
    }

    [Fact(DisplayName = "Manual track writes reject cross collection credits and release appearances")]
    public async Task Manual_track_writes_reject_cross_collection_credits_and_release_appearances()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        (HttpClient adminClient, HttpClient userClient) = await CreateAuthenticatedClientsAsync(host);
        Guid adminArtistId = await CreateArtistAsync(adminClient, "Foreign Producer");
        Guid adminReleaseId = await CreateReleaseAsync(adminClient, "Foreign Release");
        Guid userTrackId = await CreateTrackAsync(userClient, "User Track");

        using JsonDocument createWithForeignCredit = await SendJsonAsync(
            adminArtistId,
            userClient.PostAsJsonAsync(
                "/api/tracks",
                new
                {
                    title = "Foreign Credit Track",
                    credits = new[] { new { artistId = adminArtistId, role = "producer" } }
                }),
            HttpStatusCode.BadRequest);
        using JsonDocument updateWithForeignAppearance = await SendJsonAsync(
            adminReleaseId,
            userClient.PutAsJsonAsync(
                $"/api/tracks/{userTrackId}",
                new
                {
                    title = "User Track",
                    releaseAppearances = new[] { new { releaseId = adminReleaseId, position = 1 } }
                }),
            HttpStatusCode.BadRequest);

        Assert.Equal("track.artist_conflict", createWithForeignCredit.RootElement.GetProperty("code").GetString());
        Assert.Equal("track.release_conflict", updateWithForeignAppearance.RootElement.GetProperty("code").GetString());
    }

    [Fact(DisplayName = "Manual owned item writes reject cross collection targets")]
    public async Task Manual_owned_item_writes_reject_cross_collection_targets()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        (HttpClient adminClient, HttpClient userClient) = await CreateAuthenticatedClientsAsync(host);
        Guid adminReleaseId = await CreateReleaseAsync(adminClient, "Foreign Owned Target");
        Guid adminTrackId = await CreateTrackAsync(adminClient, "Foreign Track Target");
        Guid userReleaseId = await CreateReleaseAsync(userClient, "User Owned Target");
        Guid userOwnedItemId = await CreateOwnedItemAsync(userClient, userReleaseId);

        using JsonDocument createWithForeignTarget = await SendJsonAsync(
            adminReleaseId,
            userClient.PostAsJsonAsync(
                "/api/owned-items",
                new
                {
                    targetType = "release",
                    targetId = adminReleaseId,
                    status = "owned",
                    medium = new { type = "vinyl", description = "LP" }
                }),
            HttpStatusCode.Conflict);
        using JsonDocument updateWithForeignTarget = await SendJsonAsync(
            adminTrackId,
            userClient.PutAsJsonAsync(
                $"/api/owned-items/{userOwnedItemId}",
                new { targetType = "track", targetId = adminTrackId, status = "owned" }),
            HttpStatusCode.Conflict);

        Assert.Equal("owned_item.target_conflict", createWithForeignTarget.RootElement.GetProperty("code").GetString());
        Assert.Equal("owned_item.target_conflict", updateWithForeignTarget.RootElement.GetProperty("code").GetString());
    }

    private static async Task<(HttpClient AdminClient, HttpClient UserClient)> CreateAuthenticatedClientsAsync(ApiTestHost host)
    {
        HttpClient adminClient = host.CreateClient();
        using HttpResponseMessage registerResponse = await adminClient.PostAsJsonAsync(
            "/api/auth/register",
            new { email = "owner@example.com", password = "Password1!" });
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        using HttpResponseMessage createUserResponse = await adminClient.PostAsJsonAsync(
            "/api/admin/users",
            new { email = "collector@example.com", password = "Password1!", isAdmin = false });
        Assert.Equal(HttpStatusCode.Created, createUserResponse.StatusCode);

        HttpClient userClient = host.CreateClient();
        using HttpResponseMessage loginResponse = await userClient.PostAsJsonAsync(
            "/api/auth/login",
            new { email = "collector@example.com", password = "Password1!" });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        return (adminClient, userClient);
    }

    private static async Task<Guid> CreateArtistAsync(HttpClient client, string name)
    {
        using JsonDocument document = await SendJsonAsync(Guid.Empty, client.PostAsJsonAsync("/api/artists", new { type = "person", name }), HttpStatusCode.Created);
        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateLabelAsync(HttpClient client, string name)
    {
        using JsonDocument document = await SendJsonAsync(Guid.Empty, client.PostAsJsonAsync("/api/labels", new { name }), HttpStatusCode.Created);
        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateReleaseAsync(HttpClient client, string title, Guid? artistId = null)
    {
        object request = artistId is { } value
            ? new { title, isVariousArtists = false, artistCredits = new[] { new { artistId = value, role = "mainArtist" } } }
            : new { title, isVariousArtists = true };
        using JsonDocument document = await SendJsonAsync(Guid.Empty, client.PostAsJsonAsync("/api/releases", request), HttpStatusCode.Created);
        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateTrackAsync(HttpClient client, string title)
    {
        using JsonDocument document = await SendJsonAsync(Guid.Empty, client.PostAsJsonAsync("/api/tracks", new { title }), HttpStatusCode.Created);
        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateOwnedItemAsync(HttpClient client, Guid releaseId)
    {
        using JsonDocument document = await SendJsonAsync(
            Guid.Empty,
            client.PostAsJsonAsync(
                "/api/owned-items",
                new
                {
                    targetType = "release",
                    targetId = releaseId,
                    status = "owned",
                    medium = new { type = "vinyl", description = "LP" }
                }),
            HttpStatusCode.Created);
        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<JsonDocument> SendJsonAsync(Guid forbiddenId, Task<HttpResponseMessage> request, HttpStatusCode expectedStatus)
    {
        using HttpResponseMessage response = await request;
        string content = await response.Content.ReadAsStringAsync();
        Assert.Equal(expectedStatus, response.StatusCode);
        if (forbiddenId != Guid.Empty)
        {
            Assert.DoesNotContain(forbiddenId.ToString(), content, StringComparison.OrdinalIgnoreCase);
        }

        return JsonDocument.Parse(content);
    }
}
