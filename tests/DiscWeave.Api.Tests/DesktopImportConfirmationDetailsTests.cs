using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace DiscWeave.Api.Tests;

public sealed class DesktopImportConfirmationDetailsTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _postgres;

    public DesktopImportConfirmationDetailsTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact(DisplayName = "Desktop import confirmation applies cover labels credits and track edits")]
    public async Task Desktop_import_confirmation_applies_cover_labels_credits_and_track_edits()
    {
        using var root = TempImportRoot.Create();
        string releaseDirectory = Path.Combine(root.Path, "[AA 01, 2016-07-15] Steven Julien - Fallen");
        _ = Directory.CreateDirectory(releaseDirectory);
        string audioPath = Path.Combine(releaseDirectory, "01 Begins.flac");
        string coverPath = Path.Combine(releaseDirectory, "cover.jpg");
        await File.WriteAllTextAsync(audioPath, "flac");
        await File.WriteAllTextAsync(coverPath, "cover");
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        Guid existingArtistId = await CreateArtistAsync(client, "Existing Import Artist");
        Guid labelId = await CreateLabelAsync(client, "Existing Import Label");

        using JsonDocument scan = await PostScanAsync(client, root.Path, audioPath, coverPath);
        Guid sessionId = scan.RootElement.GetProperty("id").GetGuid();
        JsonElement draft = scan.RootElement.GetProperty("drafts")[0];
        Guid draftId = draft.GetProperty("id").GetGuid();
        Guid trackId = draft.GetProperty("tracks")[0].GetProperty("id").GetGuid();

        using HttpResponseMessage updateResponse = await client.PutAsJsonAsync(
            $"/api/imports/{sessionId}/drafts/{draftId}",
            ReviewedDraftPayload(existingArtistId, labelId, trackId));
        using JsonDocument update = await ReadJsonAsync(updateResponse);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.Equal("Edited Fallen", update.RootElement.GetProperty("drafts")[0].GetProperty("title").GetString());

        using HttpResponseMessage confirmResponse = await client.PostAsync($"/api/imports/{sessionId}/drafts/{draftId}/confirm", null);
        using JsonDocument confirm = await ReadJsonAsync(confirmResponse);
        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);
        Assert.Equal("confirmed", confirm.RootElement.GetProperty("drafts")[0].GetProperty("status").GetString());

        using HttpResponseMessage releaseResponse = await client.GetAsync("/api/releases?search=Edited%20Fallen&limit=10&offset=0");
        using JsonDocument releases = await ReadJsonAsync(releaseResponse);
        Guid releaseId = releases.RootElement.GetProperty("items")[0].GetProperty("id").GetGuid();
        using HttpResponseMessage releaseCreditsResponse = await client.GetAsync($"/api/credits?targetType=release&targetId={releaseId}&limit=10&offset=0");
        using JsonDocument releaseCredits = await ReadJsonAsync(releaseCreditsResponse);
        using HttpResponseMessage tracksResponse = await client.GetAsync("/api/tracks?search=Edited%20Begins&limit=10&offset=0");
        using JsonDocument tracks = await ReadJsonAsync(tracksResponse);
        Guid confirmedTrackId = tracks.RootElement.GetProperty("items")[0].GetProperty("id").GetGuid();
        using HttpResponseMessage trackCreditsResponse = await client.GetAsync($"/api/credits?targetType=track&targetId={confirmedTrackId}&limit=10&offset=0");
        using JsonDocument trackCredits = await ReadJsonAsync(trackCreditsResponse);

        Assert.Equal(HttpStatusCode.OK, releaseResponse.StatusCode);
        Assert.Equal(1, releases.RootElement.GetProperty("total").GetInt32());
        JsonElement releaseTrack = releases.RootElement.GetProperty("items")[0].GetProperty("tracklist")[0];
        Assert.Equal("CD 1", releaseTrack.GetProperty("disc").GetString());
        Assert.Equal("A", releaseTrack.GetProperty("side").GetString());
        Assert.Equal(HttpStatusCode.OK, releaseCreditsResponse.StatusCode);
        Assert.Equal(2, releaseCredits.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.OK, tracksResponse.StatusCode);
        Assert.Equal(1, tracks.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(321, tracks.RootElement.GetProperty("items")[0].GetProperty("durationSeconds").GetInt32());
        Assert.Equal(HttpStatusCode.OK, trackCreditsResponse.StatusCode);
        Assert.Equal(2, trackCredits.RootElement.GetProperty("total").GetInt32());
    }

    [Fact(DisplayName = "Desktop import draft update validates dates and track ids")]
    public async Task Desktop_import_draft_update_validates_dates_and_track_ids()
    {
        using var root = TempImportRoot.Create();
        string releaseDirectory = Path.Combine(root.Path, "[AA 01, 2016-07-15] Steven Julien - Fallen");
        _ = Directory.CreateDirectory(releaseDirectory);
        string audioPath = Path.Combine(releaseDirectory, "01 Begins.flac");
        string coverPath = Path.Combine(releaseDirectory, "cover.jpg");
        await File.WriteAllTextAsync(audioPath, "flac");
        await File.WriteAllTextAsync(coverPath, "cover");
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        Guid existingArtistId = await CreateArtistAsync(client, "Validation Artist");
        Guid labelId = await CreateLabelAsync(client, "Validation Label");

        using JsonDocument scan = await PostScanAsync(client, root.Path, audioPath, coverPath);
        Guid sessionId = scan.RootElement.GetProperty("id").GetGuid();
        Guid draftId = scan.RootElement.GetProperty("drafts")[0].GetProperty("id").GetGuid();

        using HttpResponseMessage invalidDateResponse = await client.PutAsJsonAsync(
            $"/api/imports/{sessionId}/drafts/{draftId}",
            ReviewedDraftPayload(existingArtistId, labelId, Guid.CreateVersion7(), releaseDate: "2016/07/15"));
        using JsonDocument invalidDate = await ReadJsonAsync(invalidDateResponse);
        using HttpResponseMessage missingTrackResponse = await client.PutAsJsonAsync(
            $"/api/imports/{sessionId}/drafts/{draftId}",
            ReviewedDraftPayload(existingArtistId, labelId, Guid.CreateVersion7()));
        using JsonDocument missingTrack = await ReadJsonAsync(missingTrackResponse);

        Assert.Equal(HttpStatusCode.BadRequest, invalidDateResponse.StatusCode);
        Assert.Equal("release_import.release_date_invalid", invalidDate.RootElement.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, missingTrackResponse.StatusCode);
        Assert.Equal("release_import.track_not_found", missingTrack.RootElement.GetProperty("code").GetString());
    }

    private static object ReviewedDraftPayload(Guid artistId, Guid labelId, Guid trackId, string releaseDate = "2016-07-15")
    {
        return new
        {
            title = "Edited Fallen",
            type = "album",
            catalogNumber = "CAT-001",
            labelName = (string?)null,
            releaseDate,
            year = 2016,
            isVariousArtists = false,
            notOnLabel = false,
            artistNames = Array.Empty<string>(),
            artistCredits = new object[]
            {
                new { artistId = (Guid?)artistId, name = (string?)null, role = "producer" },
                new { artistId = (Guid?)null, name = "New Import Guest", role = "featuredArtist" }
            },
            labels = new object[]
            {
                new { labelId = (Guid?)labelId, name = (string?)null, catalogNumber = "CAT-001", hasNoCatalogNumber = false }
            },
            selectedArtistIds = Array.Empty<Guid>(),
            genres = new[] { "Techno" },
            tags = new[] { "imported" },
            coverPath = (string?)null,
            tracks = new object[]
            {
                new
                {
                    id = trackId,
                    position = (int?)1,
                    disc = "  CD 1  ",
                    side = " A ",
                    title = "Edited Begins",
                    durationSeconds = (int?)321,
                    artistNames = Array.Empty<string>(),
                    artistCredits = new object[]
                    {
                        new { artistId = (Guid?)artistId, name = (string?)null, role = "mainArtist" },
                        new { artistId = (Guid?)null, name = "New Import Remixer", role = "remixer" }
                    },
                    selectedArtistIds = Array.Empty<Guid>(),
                    selectedTrackId = (Guid?)null,
                    isSkipped = false
                }
            }
        };
    }

    private static async Task<JsonDocument> PostScanAsync(HttpClient client, string rootPath, string audioPath, string coverPath)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/imports/desktop-folder-scans",
            new
            {
                sourceRoot = rootPath,
                ignoredFileCount = 0,
                files = new object[]
                {
                    AudioFile(rootPath, audioPath),
                    CoverFile(rootPath, coverPath)
                }
            });
        JsonDocument document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return document;
    }

    private static object AudioFile(string rootPath, string filePath)
    {
        return new
        {
            filePath,
            relativePath = Path.GetRelativePath(rootPath, filePath),
            format = "flac",
            sizeBytes = 10,
            lastModifiedAt = DateTimeOffset.UtcNow,
            audioMetadata = new
            {
                title = (string?)null,
                artists = Array.Empty<string>(),
                albumTitle = (string?)null,
                albumArtists = Array.Empty<string>(),
                catalogNumber = (string?)null,
                releaseDate = (string?)null,
                year = (int?)null,
                durationSeconds = (int?)null,
                trackNumber = (int?)null
            },
            coverArtifact = (object?)null
        };
    }

    private static object CoverFile(string rootPath, string filePath)
    {
        return new
        {
            filePath,
            relativePath = Path.GetRelativePath(rootPath, filePath),
            format = (string?)null,
            sizeBytes = 5,
            lastModifiedAt = DateTimeOffset.UtcNow,
            audioMetadata = (object?)null,
            coverArtifact = new
            {
                fileName = "cover.jpg",
                extension = ".jpg",
                contentType = "image/jpeg",
                sizeBytes = 5,
                contentBase64 = "Y292ZXI="
            }
        };
    }

    private static async Task<Guid> CreateArtistAsync(HttpClient client, string name)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/artists", new { type = "person", name });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateLabelAsync(HttpClient client, string name)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/labels", new { name });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        Stream stream = await response.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(stream);
    }

    private sealed class TempImportRoot : IDisposable
    {
        private TempImportRoot(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TempImportRoot Create()
        {
            return new TempImportRoot(Directory.CreateTempSubdirectory("discweave-import-confirm-test-").FullName);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
