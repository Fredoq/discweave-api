using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cratebase.Domain.SharedKernel.Ids;

namespace Cratebase.Api.Tests;

public sealed class InviteRegistrationEndpointTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _postgres;

    public InviteRegistrationEndpointTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact(DisplayName = "Registration after bootstrap requires an invite")]
    public async Task Registration_after_bootstrap_requires_an_invite()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient adminClient = host.CreateClient();
        HttpClient invitedClient = host.CreateClient();

        using HttpResponseMessage bootstrapResponse = await adminClient.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest("owner@example.com", "Password1!", null));
        using HttpResponseMessage registerResponse = await invitedClient.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest("collector@example.com", "Password1!", null));
        using JsonDocument registerDocument = await ReadJsonAsync(registerResponse);

        Assert.Equal(HttpStatusCode.Created, bootstrapResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, registerResponse.StatusCode);
        Assert.Equal("auth.invite_required", registerDocument.RootElement.GetProperty("code").GetString());
    }

    [Fact(DisplayName = "Valid invite redemption creates a normal signed in user")]
    public async Task Valid_invite_redemption_creates_a_normal_signed_in_user()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient adminClient = host.CreateClient();
        HttpClient invitedClient = host.CreateClient();

        using HttpResponseMessage bootstrapResponse = await adminClient.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest("owner@example.com", "Password1!", null));
        using HttpResponseMessage createInviteResponse = await adminClient.PostAsJsonAsync(
            "/api/admin/invites",
            new CreateInviteRequest(null, "Beta collector"));
        using JsonDocument createInviteDocument = await ReadJsonAsync(createInviteResponse);
        string inviteCode = createInviteDocument.RootElement.GetProperty("code").GetString() ?? string.Empty;

        using HttpResponseMessage registerResponse = await invitedClient.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest("collector@example.com", "Password1!", inviteCode.ToLowerInvariant().Replace("-", " ")));
        using JsonDocument registerDocument = await ReadJsonAsync(registerResponse);
        using HttpResponseMessage sessionResponse = await invitedClient.GetAsync("/api/auth/session");
        using JsonDocument sessionDocument = await ReadJsonAsync(sessionResponse);
        using HttpResponseMessage listInvitesResponse = await adminClient.GetAsync("/api/admin/invites");
        using JsonDocument listInvitesDocument = await ReadJsonAsync(listInvitesResponse);
        CollectionId? userCollectionId = await host.FindDefaultCollectionIdForUserAsync("collector@example.com");

        Assert.Equal(HttpStatusCode.Created, bootstrapResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, createInviteResponse.StatusCode);
        Assert.StartsWith("CRATE-", inviteCode, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        Assert.True(registerDocument.RootElement.GetProperty("isAuthenticated").GetBoolean());
        Assert.Equal("collector@example.com", registerDocument.RootElement.GetProperty("email").GetString());
        Assert.Equal(["User"], registerDocument.RootElement.GetProperty("roles").EnumerateArray().Select(role => role.GetString()));
        Assert.False(registerDocument.RootElement.TryGetProperty("collectionId", out _));
        Assert.False(registerDocument.RootElement.TryGetProperty("defaultCollectionId", out _));
        Assert.Equal(HttpStatusCode.OK, sessionResponse.StatusCode);
        Assert.True(sessionDocument.RootElement.GetProperty("isAuthenticated").GetBoolean());
        Assert.True(userCollectionId.HasValue);
        Assert.True(await host.CollectionExistsAsync(userCollectionId.Value));

        JsonElement invite = Assert.Single(listInvitesDocument.RootElement.EnumerateArray());
        Assert.Equal("redeemed", invite.GetProperty("status").GetString());
        Assert.Equal("collector@example.com", invite.GetProperty("redeemedEmail").GetString());
        Assert.False(invite.TryGetProperty("code", out _));
        Assert.False(invite.TryGetProperty("codeHash", out _));
    }

    [Fact(DisplayName = "Unavailable invites cannot be redeemed")]
    public async Task Unavailable_invites_cannot_be_redeemed()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient adminClient = host.CreateClient();
        HttpClient firstClient = host.CreateClient();
        HttpClient secondClient = host.CreateClient();
        HttpClient thirdClient = host.CreateClient();
        HttpClient fourthClient = host.CreateClient();

        using HttpResponseMessage bootstrapResponse = await adminClient.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest("owner@example.com", "Password1!", null));
        CreateInviteResult usedInvite = await CreateInviteAsync(adminClient);
        using HttpResponseMessage firstRegisterResponse = await firstClient.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest("first@example.com", "Password1!", usedInvite.Code));
        using HttpResponseMessage reuseResponse = await secondClient.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest("second@example.com", "Password1!", usedInvite.Code));
        using JsonDocument reuseDocument = await ReadJsonAsync(reuseResponse);

        const string expiredCode = "CRATE-ABCD-EFGH-JKLM-MNPQ";
        DateTimeOffset createdAt = DateTimeOffset.UtcNow.AddDays(-2);
        await host.SeedInviteForUserAsync(
            "owner@example.com",
            HashInviteCode(expiredCode),
            createdAt,
            createdAt.AddDays(1));
        using HttpResponseMessage expiredResponse = await thirdClient.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest("third@example.com", "Password1!", expiredCode));
        using JsonDocument expiredDocument = await ReadJsonAsync(expiredResponse);

        CreateInviteResult revokedInvite = await CreateInviteAsync(adminClient);
        using HttpResponseMessage revokeResponse = await adminClient.PostAsync($"/api/admin/invites/{revokedInvite.Id}/revoke", null);
        using HttpResponseMessage revokedResponse = await fourthClient.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest("fourth@example.com", "Password1!", revokedInvite.Code));
        using JsonDocument revokedDocument = await ReadJsonAsync(revokedResponse);

        Assert.Equal(HttpStatusCode.Created, bootstrapResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, firstRegisterResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, reuseResponse.StatusCode);
        Assert.Equal("auth.invite_unavailable", reuseDocument.RootElement.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, expiredResponse.StatusCode);
        Assert.Equal("auth.invite_unavailable", expiredDocument.RootElement.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.OK, revokeResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, revokedResponse.StatusCode);
        Assert.Equal("auth.invite_unavailable", revokedDocument.RootElement.GetProperty("code").GetString());
    }

    [Fact(DisplayName = "Invite note length is validated before persistence")]
    public async Task Invite_note_length_is_validated_before_persistence()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient adminClient = host.CreateClient();

        using HttpResponseMessage bootstrapResponse = await adminClient.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest("owner@example.com", "Password1!", null));
        using HttpResponseMessage createInviteResponse = await adminClient.PostAsJsonAsync(
            "/api/admin/invites",
            new CreateInviteRequest(null, new string('a', 513)));
        using JsonDocument createInviteDocument = await ReadJsonAsync(createInviteResponse);

        Assert.Equal(HttpStatusCode.Created, bootstrapResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, createInviteResponse.StatusCode);
        Assert.Equal("invite.note_too_long", createInviteDocument.RootElement.GetProperty("code").GetString());
    }

    [Fact(DisplayName = "Invite expiration must be in the future")]
    public async Task Invite_expiration_must_be_in_the_future()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient adminClient = host.CreateClient();

        using HttpResponseMessage bootstrapResponse = await adminClient.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest("owner@example.com", "Password1!", null));
        using HttpResponseMessage createInviteResponse = await adminClient.PostAsJsonAsync(
            "/api/admin/invites",
            new CreateInviteRequest(DateTimeOffset.UtcNow.AddMinutes(-1), null));
        using JsonDocument createInviteDocument = await ReadJsonAsync(createInviteResponse);

        Assert.Equal(HttpStatusCode.Created, bootstrapResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, createInviteResponse.StatusCode);
        Assert.Equal("invite.expires_at_invalid", createInviteDocument.RootElement.GetProperty("code").GetString());
    }

    private static async Task<CreateInviteResult> CreateInviteAsync(HttpClient adminClient)
    {
        using HttpResponseMessage response = await adminClient.PostAsJsonAsync("/api/admin/invites", new CreateInviteRequest(null, null));
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        Guid inviteId = document.RootElement.GetProperty("id").GetGuid();
        string code = document.RootElement.GetProperty("code").GetString() ?? throw new InvalidOperationException("Invite code was missing");

        return new CreateInviteResult(inviteId, code);
    }

    private static string HashInviteCode(string code)
    {
        string normalized = new([.. code.Where(character => character is not '-' && !char.IsWhiteSpace(character)).Select(char.ToUpperInvariant)]);
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));

        return Convert.ToHexString(hash);
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
    }

    private sealed record RegisterRequest(string Email, string Password, string? InviteCode);

    private sealed record CreateInviteRequest(DateTimeOffset? ExpiresAt, string? Note);

    private sealed record CreateInviteResult(Guid Id, string Code);
}
