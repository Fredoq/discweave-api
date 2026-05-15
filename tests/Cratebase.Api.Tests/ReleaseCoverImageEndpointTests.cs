using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Cratebase.Api.Tests;

public sealed class ReleaseCoverImageEndpointTests : IClassFixture<PostgresFixture>
{
    private const string CoverStorageRootSetting = "ReleaseCovers:StorageRoot";
    private readonly PostgresFixture _postgres;

    public ReleaseCoverImageEndpointTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact(DisplayName = "Release cover image can be uploaded fetched replaced and deleted")]
    public async Task Release_cover_image_can_be_uploaded_fetched_replaced_and_deleted()
    {
        using var tempDirectory = TempDirectory.Create();
        await using ApiTestHost host = await ApiTestHost.CreateAsync(
            _postgres,
            new Dictionary<string, string?> { [CoverStorageRootSetting] = tempDirectory.Path });
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        Guid releaseId = await CreateReleaseAsync(client, "Selected Ambient Works 85-92");
        byte[] pngBytes = TinyPngBytes();

        using HttpResponseMessage uploadResponse = await client.PutAsync(
            $"/api/releases/{releaseId}/cover-image",
            CreateMultipart(pngBytes, "SAW Front.PNG", "image/png"));
        using JsonDocument uploadDocument = await ReadJsonAsync(uploadResponse);
        string firstStoredPath = Assert.Single(Directory.GetFiles(tempDirectory.Path, "*", SearchOption.AllDirectories));

        using HttpResponseMessage coverResponse = await client.GetAsync($"/api/releases/{releaseId}/cover-image");
        byte[] returnedBytes = await coverResponse.Content.ReadAsByteArrayAsync();
        using HttpResponseMessage releaseResponse = await client.GetAsync($"/api/releases/{releaseId}");
        using JsonDocument releaseDocument = await ReadJsonAsync(releaseResponse);

        Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);
        AssertCoverResponse(uploadDocument.RootElement, releaseId, "image/png", "SAW Front.PNG", pngBytes.Length);
        Assert.Equal(HttpStatusCode.OK, coverResponse.StatusCode);
        Assert.Equal("image/png", coverResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal(pngBytes, returnedBytes);
        Assert.Equal(HttpStatusCode.OK, releaseResponse.StatusCode);
        AssertCoverResponse(releaseDocument.RootElement.GetProperty("coverImage"), releaseId, "image/png", "SAW Front.PNG", pngBytes.Length);

        byte[] webpBytes = TinyWebpBytes();
        using HttpResponseMessage replaceResponse = await client.PutAsync(
            $"/api/releases/{releaseId}/cover-image",
            CreateMultipart(webpBytes, "replacement.webp", "image/webp"));
        using JsonDocument replaceDocument = await ReadJsonAsync(replaceResponse);
        string replacementPath = Assert.Single(Directory.GetFiles(tempDirectory.Path, "*", SearchOption.AllDirectories));

        Assert.Equal(HttpStatusCode.OK, replaceResponse.StatusCode);
        AssertCoverResponse(replaceDocument.RootElement, releaseId, "image/webp", "replacement.webp", webpBytes.Length);
        Assert.NotEqual(firstStoredPath, replacementPath);
        Assert.False(File.Exists(firstStoredPath));

        using HttpResponseMessage deleteWithoutConfirmationResponse = await client.DeleteAsync($"/api/releases/{releaseId}/cover-image");
        using HttpRequestMessage deleteRequest = new(HttpMethod.Delete, $"/api/releases/{releaseId}/cover-image");
        deleteRequest.Headers.Add("X-Cratebase-Confirm-Delete", $"release-cover:{releaseId}");
        using HttpResponseMessage deleteResponse = await client.SendAsync(deleteRequest);
        using HttpResponseMessage missingCoverResponse = await client.GetAsync($"/api/releases/{releaseId}/cover-image");

        Assert.Equal(HttpStatusCode.BadRequest, deleteWithoutConfirmationResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Empty(Directory.GetFiles(tempDirectory.Path, "*", SearchOption.AllDirectories));
        Assert.Equal(HttpStatusCode.NotFound, missingCoverResponse.StatusCode);
    }

    [Theory(DisplayName = "Release cover upload rejects invalid files")]
    [MemberData(nameof(InvalidCoverFiles))]
    public async Task Release_cover_upload_rejects_invalid_files(byte[] content, string fileName, string contentType)
    {
        using var tempDirectory = TempDirectory.Create();
        await using ApiTestHost host = await ApiTestHost.CreateAsync(
            _postgres,
            new Dictionary<string, string?> { [CoverStorageRootSetting] = tempDirectory.Path });
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        Guid releaseId = await CreateReleaseAsync(client, "Invalid Cover Test");

        using HttpResponseMessage response = await client.PutAsync(
            $"/api/releases/{releaseId}/cover-image",
            CreateMultipart(content, fileName, contentType));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(Directory.GetFiles(tempDirectory.Path, "*", SearchOption.AllDirectories));
    }

    [Fact(DisplayName = "Release cover endpoints are collection isolated")]
    public async Task Release_cover_endpoints_are_collection_isolated()
    {
        using var tempDirectory = TempDirectory.Create();
        await using ApiTestHost host = await ApiTestHost.CreateAsync(
            _postgres,
            new Dictionary<string, string?> { [CoverStorageRootSetting] = tempDirectory.Path });
        (HttpClient adminClient, HttpClient userClient) = await CreateAuthenticatedClientsAsync(host);
        Guid adminReleaseId = await CreateReleaseAsync(adminClient, "Admin Release");
        using HttpResponseMessage adminUploadResponse = await adminClient.PutAsync(
            $"/api/releases/{adminReleaseId}/cover-image",
            CreateMultipart(TinyPngBytes(), "admin.png", "image/png"));

        using HttpResponseMessage userUploadResponse = await userClient.PutAsync(
            $"/api/releases/{adminReleaseId}/cover-image",
            CreateMultipart(TinyPngBytes(), "user.png", "image/png"));
        using HttpResponseMessage userGetResponse = await userClient.GetAsync($"/api/releases/{adminReleaseId}/cover-image");
        using HttpRequestMessage deleteRequest = new(HttpMethod.Delete, $"/api/releases/{adminReleaseId}/cover-image");
        deleteRequest.Headers.Add("X-Cratebase-Confirm-Delete", $"release-cover:{adminReleaseId}");
        using HttpResponseMessage userDeleteResponse = await userClient.SendAsync(deleteRequest);

        Assert.Equal(HttpStatusCode.OK, adminUploadResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, userUploadResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, userGetResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, userDeleteResponse.StatusCode);
    }

    public static TheoryData<byte[], string, string> InvalidCoverFiles()
    {
        return new TheoryData<byte[], string, string>
        {
            { TinyWebpBytes(), "cover.webm", "video/webm" },
            { "not-an-image"u8.ToArray(), "cover.jpg", "image/jpeg" },
            { new byte[(10 * 1024 * 1024) + 1], "large.png", "image/png" }
        };
    }

    private static MultipartFormDataContent CreateMultipart(byte[] content, string fileName, string contentType)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        form.Add(file, "file", fileName);

        return form;
    }

    private static void AssertCoverResponse(
        JsonElement coverElement,
        Guid releaseId,
        string contentType,
        string originalFileName,
        int sizeBytes)
    {
        Assert.Equal($"/api/releases/{releaseId}/cover-image", coverElement.GetProperty("url").GetString());
        Assert.Equal(contentType, coverElement.GetProperty("contentType").GetString());
        Assert.Equal(originalFileName, coverElement.GetProperty("originalFileName").GetString());
        Assert.Equal(sizeBytes, coverElement.GetProperty("sizeBytes").GetInt64());
        Assert.Equal("localUpload", coverElement.GetProperty("sourceType").GetString());
    }

    private static async Task<Guid> CreateReleaseAsync(HttpClient client, string title)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/releases",
            new
            {
                title,
                type = "album",
                isVariousArtists = true,
                notOnLabel = true,
                genres = Array.Empty<string>(),
                tags = Array.Empty<string>()
            });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<(HttpClient AdminClient, HttpClient UserClient)> CreateAuthenticatedClientsAsync(ApiTestHost host)
    {
        HttpClient adminClient = host.CreateClient();
        using HttpResponseMessage registerResponse = await adminClient.PostAsJsonAsync(
            "/api/auth/register",
            new AuthRequest("owner@example.com", "Password1!"));
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        using HttpResponseMessage createUserResponse = await adminClient.PostAsJsonAsync(
            "/api/admin/users",
            new CreateUserRequest("collector@example.com", "Password1!", false));
        Assert.Equal(HttpStatusCode.Created, createUserResponse.StatusCode);

        HttpClient userClient = host.CreateClient();
        using HttpResponseMessage loginResponse = await userClient.PostAsJsonAsync(
            "/api/auth/login",
            new AuthRequest("collector@example.com", "Password1!"));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        return (adminClient, userClient);
    }

    private static byte[] TinyPngBytes()
    {
        return [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01];
    }

    private static byte[] TinyWebpBytes()
    {
        return [0x52, 0x49, 0x46, 0x46, 0x06, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50, 0x01];
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        string content = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(content);
    }

    private sealed record AuthRequest(string Email, string Password);

    private sealed record CreateUserRequest(string Email, string Password, bool IsAdmin);

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
                "cratebase-release-cover-endpoint-tests",
                Guid.NewGuid().ToString("N"));
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
