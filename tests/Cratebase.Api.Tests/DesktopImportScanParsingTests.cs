using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Cratebase.Api.Tests;

public sealed class DesktopImportScanParsingTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _postgres;

    public DesktopImportScanParsingTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact(DisplayName = "Desktop scan groups disc folders and selects prioritized covers")]
    public async Task Desktop_scan_groups_disc_folders_and_selects_prioritized_covers()
    {
        using var root = TempImportRoot.Create();
        string releaseDirectory = Path.Combine(root.Path, "[BP2016, 2022-00-00] VA - Structures");
        string discOne = Path.Combine(releaseDirectory, "CD 1");
        string discTwo = Path.Combine(releaseDirectory, "CD 2");
        _ = Directory.CreateDirectory(discOne);
        _ = Directory.CreateDirectory(discTwo);

        string trackOne = Path.Combine(discOne, "01 Steve Bicknell - Disguise of Beings.flac");
        string trackTwo = Path.Combine(discTwo, "02 Dj Sports, C.K. & pH 1 - Second Wave.m4a");
        string cover = Path.Combine(releaseDirectory, "cover.png");
        string front = Path.Combine(releaseDirectory, "front.jpg");
        string hidden = Path.Combine(releaseDirectory, ".DS_Store");
        string notes = Path.Combine(releaseDirectory, "notes.txt");
        await File.WriteAllTextAsync(trackOne, "flac");
        await File.WriteAllTextAsync(trackTwo, "m4a");
        await File.WriteAllTextAsync(cover, "png");
        await File.WriteAllTextAsync(front, "jpg");
        await File.WriteAllTextAsync(hidden, "hidden");
        await File.WriteAllTextAsync(notes, "notes");
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/imports/desktop-folder-scans",
            new
            {
                sourceRoot = root.Path,
                ignoredFileCount = 1,
                files = new object[]
                {
                    AudioFile(root.Path, trackOne, "flac"),
                    AudioFile(root.Path, trackTwo, "m4a"),
                    CoverFile(root.Path, front, "front.jpg", ".jpg", "image/jpeg", 5),
                    CoverFile(root.Path, cover, "cover.png", ".png", "image/png", 5),
                    UnknownFile(root.Path, hidden),
                    UnknownFile(root.Path, notes)
                }
            });
        using JsonDocument document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(1, document.RootElement.GetProperty("draftCount").GetInt32());
        Assert.Equal(2, document.RootElement.GetProperty("trackCount").GetInt32());
        Assert.Equal(3, document.RootElement.GetProperty("ignoredFileCount").GetInt32());

        JsonElement draft = document.RootElement.GetProperty("drafts")[0];
        Assert.Equal("Structures", draft.GetProperty("title").GetString());
        Assert.Equal("BP2016", draft.GetProperty("catalogNumber").GetString());
        Assert.True(draft.GetProperty("isVariousArtists").GetBoolean());
        Assert.Equal("compilation", draft.GetProperty("type").GetString());
        Assert.Equal(2022, draft.GetProperty("year").GetInt32());
        Assert.Equal(JsonValueKind.Null, draft.GetProperty("releaseDate").ValueKind);
        Assert.EndsWith("cover.png", draft.GetProperty("coverPath").GetString(), StringComparison.Ordinal);
        Assert.Equal(2, draft.GetProperty("issues").GetArrayLength());
        Assert.Contains(
            draft.GetProperty("issues").EnumerateArray(),
            issue => issue.GetProperty("code").GetString() == "import.release_date_partial");
        Assert.Contains(
            draft.GetProperty("issues").EnumerateArray(),
            issue => issue.GetProperty("code").GetString() == "import.cover_multiple_candidates");

        JsonElement tracks = draft.GetProperty("tracks");
        Assert.Equal("Disguise of Beings", tracks[0].GetProperty("title").GetString());
        Assert.Equal("flac", tracks[0].GetProperty("format").GetString());
        Assert.Equal("Steve Bicknell", tracks[0].GetProperty("artistNames")[0].GetString());
        Assert.Equal("Second Wave", tracks[1].GetProperty("title").GetString());
        Assert.Equal("m4a", tracks[1].GetProperty("format").GetString());
        Assert.Equal("Dj Sports", tracks[1].GetProperty("artistNames")[0].GetString());
        Assert.Equal("C.K. & pH 1", tracks[1].GetProperty("artistNames")[1].GetString());
    }

    [Fact(DisplayName = "Desktop scan rejects invalid cover artifacts")]
    public async Task Desktop_scan_rejects_invalid_cover_artifacts()
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

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/imports/desktop-folder-scans",
            new
            {
                sourceRoot = root.Path,
                ignoredFileCount = 0,
                files = new object[]
                {
                    AudioFile(root.Path, audioPath, "flac"),
                    CoverFile(root.Path, coverPath, "cover.jpg", ".jpg", "image/jpeg", 5, contentBase64: "not base64")
                }
            });
        using JsonDocument document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("release_import.cover_invalid", document.RootElement.GetProperty("code").GetString());
    }

    private static object AudioFile(string rootPath, string filePath, string format)
    {
        return new
        {
            filePath,
            relativePath = Path.GetRelativePath(rootPath, filePath),
            format,
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

    private static object CoverFile(
        string rootPath,
        string filePath,
        string fileName,
        string extension,
        string contentType,
        long sizeBytes,
        string contentBase64 = "Y292ZXI=")
    {
        return new
        {
            filePath,
            relativePath = Path.GetRelativePath(rootPath, filePath),
            format = (string?)null,
            sizeBytes,
            lastModifiedAt = DateTimeOffset.UtcNow,
            audioMetadata = (object?)null,
            coverArtifact = new
            {
                fileName,
                extension,
                contentType,
                sizeBytes,
                contentBase64
            }
        };
    }

    private static object UnknownFile(string rootPath, string filePath)
    {
        return new
        {
            filePath,
            relativePath = Path.GetRelativePath(rootPath, filePath),
            format = (string?)null,
            sizeBytes = 1,
            lastModifiedAt = DateTimeOffset.UtcNow,
            audioMetadata = (object?)null,
            coverArtifact = (object?)null
        };
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
            return new TempImportRoot(Directory.CreateTempSubdirectory("cratebase-import-scan-test-").FullName);
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
