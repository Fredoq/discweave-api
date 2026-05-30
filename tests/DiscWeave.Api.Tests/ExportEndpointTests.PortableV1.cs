using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace DiscWeave.Api.Tests;

public sealed partial class ExportEndpointTests
{
    [Fact(DisplayName = "CSV export includes release cover metadata without image payload")]
    public async Task Csv_export_includes_release_cover_metadata_without_image_payload()
    {
        using var tempDirectory = TempDirectory.Create();
        await using ApiTestHost host = await ApiTestHost.CreateAsync(
            _postgres,
            new Dictionary<string, string?> { ["ReleaseCovers:StorageRoot"] = tempDirectory.Path });
        HttpClient client = await host.CreateAuthenticatedClientAsync();

        Guid labelId = await CreateLabelAsync(client, "Factory Records");
        Guid artistId = await CreateArtistAsync(client, "New Order");
        Guid releaseId = await CreateReleaseWithTrackAsync(client, labelId, artistId);
        byte[] coverBytes = TinyPngBytes();
        using HttpResponseMessage uploadResponse = await client.PutAsync(
            $"/api/releases/{releaseId}/cover-image",
            CreateMultipart(coverBytes, "Power Cover.PNG", "image/png"));
        Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);

        using HttpResponseMessage response = await client.GetAsync("/api/exports/csv");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using Stream stream = await response.Content.ReadAsStreamAsync();
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        string releasesCsv = await ReadEntryAsync(archive, "releases.csv");

        Assert.Contains(
            "cover_image_url,cover_image_content_type,cover_image_original_file_name,cover_image_size_bytes,cover_image_source_type",
            releasesCsv,
            StringComparison.Ordinal);
        Assert.Contains($"/api/releases/{releaseId}/cover-image", releasesCsv, StringComparison.Ordinal);
        Assert.Contains("image/png", releasesCsv, StringComparison.Ordinal);
        Assert.Contains("Power Cover.PNG", releasesCsv, StringComparison.Ordinal);
        Assert.Contains($",{coverBytes.Length},localUpload", releasesCsv, StringComparison.Ordinal);
        Assert.DoesNotContain(Convert.ToBase64String(coverBytes), releasesCsv, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "Exports include confirmed desktop import catalog data without local payloads")]
    public async Task Exports_include_confirmed_desktop_import_catalog_data_without_local_payloads()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        const string coverBase64 = "Y292ZXI=";
        using JsonDocument scan = await PostDesktopScanAsync(
            client,
            "/music/source",
            DesktopAudioFile(
                "/music/source",
                "/music/source/[AA 01, 2016] Steven Julien - Fallen/01 Begins.flac"),
            DesktopCoverFile(
                "/music/source",
                "/music/source/[AA 01, 2016] Steven Julien - Fallen/cover.jpg",
                coverBase64));
        await ConfirmOnlyDesktopDraftAsync(client, scan);

        using HttpResponseMessage jsonResponse = await client.GetAsync("/api/exports/json");
        string json = await jsonResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, jsonResponse.StatusCode);
        using HttpResponseMessage csvResponse = await client.GetAsync("/api/exports/csv");
        Assert.Equal(HttpStatusCode.OK, csvResponse.StatusCode);
        await using Stream csvStream = await csvResponse.Content.ReadAsStreamAsync();
        using var archive = new ZipArchive(csvStream, ZipArchiveMode.Read);
        string tracksCsv = await ReadEntryAsync(archive, "tracks.csv");
        string ownedItemsCsv = await ReadEntryAsync(archive, "owned_items.csv");

        Assert.Contains("Fallen", json, StringComparison.Ordinal);
        Assert.Contains("Begins", json, StringComparison.Ordinal);
        Assert.Contains("\"format\":\"flac\"", json, StringComparison.Ordinal);
        Assert.Contains("Begins", tracksCsv, StringComparison.Ordinal);
        Assert.Contains("flac", ownedItemsCsv, StringComparison.Ordinal);
        Assert.DoesNotContain("collectionId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("contentBase64", json, StringComparison.Ordinal);
        Assert.DoesNotContain(coverBase64, json, StringComparison.Ordinal);
        Assert.DoesNotContain(coverBase64, tracksCsv, StringComparison.Ordinal);
        Assert.DoesNotContain(coverBase64, ownedItemsCsv, StringComparison.Ordinal);
    }

    private static MultipartFormDataContent CreateMultipart(byte[] content, string fileName, string contentType)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        form.Add(file, "file", fileName);

        return form;
    }

    private static byte[] TinyPngBytes()
    {
        return [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01];
    }

    private static async Task<JsonDocument> PostDesktopScanAsync(HttpClient client, string rootPath, params object[] files)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/imports/desktop-folder-scans",
            new
            {
                sourceRoot = rootPath,
                ignoredFileCount = 0,
                files
            });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return await ReadJsonAsync(response);
    }

    private static object DesktopAudioFile(string rootPath, string filePath)
    {
        return new
        {
            filePath,
            relativePath = Path.GetRelativePath(rootPath, filePath),
            format = "flac",
            sizeBytes = 9,
            lastModifiedAt = DateTimeOffset.UtcNow,
            contentHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            audioMetadata = new
            {
                title = (string?)null,
                artists = Array.Empty<string>(),
                albumTitle = (string?)null,
                albumArtists = new[] { "Steven Julien" },
                catalogNumber = (string?)null,
                releaseDate = "2016",
                year = (int?)2016,
                durationSeconds = (int?)null,
                trackNumber = (int?)1
            },
            coverArtifact = (object?)null
        };
    }

    private static object DesktopCoverFile(string rootPath, string filePath, string contentBase64)
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
                fileName = Path.GetFileName(filePath),
                extension = Path.GetExtension(filePath),
                contentType = "image/jpeg",
                sizeBytes = 5,
                contentBase64
            }
        };
    }

    private static async Task ConfirmOnlyDesktopDraftAsync(HttpClient client, JsonDocument scan)
    {
        Guid sessionId = scan.RootElement.GetProperty("id").GetGuid();
        Guid draftId = scan.RootElement.GetProperty("drafts")[0].GetProperty("id").GetGuid();
        using HttpResponseMessage response = await client.PostAsync($"/api/imports/{sessionId}/drafts/{draftId}/confirm", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private sealed class TempDirectory : IDisposable
    {
        private TempDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TempDirectory Create()
        {
            string path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "discweave-export-endpoint-tests",
                Guid.CreateVersion7().ToString("N"));
            _ = Directory.CreateDirectory(path);

            return new TempDirectory(path);
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
