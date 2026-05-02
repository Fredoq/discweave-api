using Cratebase.Domain.Catalog;
using Cratebase.Domain.Credits;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;
using System.Text.Json;

namespace Cratebase.Api.Tests;

internal sealed class ApiTestHost : IAsyncDisposable
{
    private readonly WebApplicationFactory<Program> _factory;

    private ApiTestHost(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    public CollectionId DefaultCollectionId { get; private set; }

    public static async Task<ApiTestHost> CreateAsync(PostgresFixture postgres, CancellationToken cancellationToken = default)
    {
        string connectionString = await postgres.CreateDatabaseAsync(cancellationToken);
        WebApplicationFactory<Program> factory = new ConfiguredApiFactory(connectionString);

        var host = new ApiTestHost(factory);
        await host.MigrateAsync(cancellationToken);

        return host;
    }

    public HttpClient CreateClient()
    {
        return _factory.CreateClient();
    }

    public async Task<HttpClient> CreateAuthenticatedClientAsync(CancellationToken cancellationToken = default)
    {
        HttpClient client = CreateClient();
        string email = $"test-{Guid.CreateVersion7()}@example.com";

        using HttpResponseMessage registerResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            new AuthRequest(email, "Password1!"),
            cancellationToken);
        _ = registerResponse.EnsureSuccessStatusCode();

        await using Stream responseStream = await registerResponse.Content.ReadAsStreamAsync(cancellationToken);
        using JsonDocument document = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken);
        DefaultCollectionId = new CollectionId(document.RootElement.GetProperty("defaultCollectionId").GetGuid());

        return client;
    }

    public async Task<ArtistId> SeedArtistAsync(Artist artist, CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        CratebaseDbContext context = scope.ServiceProvider.GetRequiredService<CratebaseDbContext>();
        _ = context.Artists.Add(artist);
        _ = await context.SaveChangesAsync(cancellationToken);

        return artist.Id;
    }

    public async Task<Artist?> FindArtistAsync(ArtistId artistId, CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        CratebaseDbContext context = scope.ServiceProvider.GetRequiredService<CratebaseDbContext>();

        return await context.Artists.SingleOrDefaultAsync(artist => artist.Id == artistId, cancellationToken);
    }

    public async Task SeedReleaseCreditAsync(Artist artist, CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        CratebaseDbContext context = scope.ServiceProvider.GetRequiredService<CratebaseDbContext>();
        var release = Release.Create(artist.CollectionId, ReleaseId.New(), "Confusion");
        _ = context.Releases.Add(release);
        _ = context.Credits.Add(Credit.Create(artist.CollectionId, CreditId.New(), CreditContributor.FromArtist(artist), CreditTarget.ForRelease(release.Id), CreditRole.Producer));
        _ = await context.SaveChangesAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        _factory.Dispose();
        await ValueTask.CompletedTask;
    }

    private async Task MigrateAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        CratebaseDbContext context = scope.ServiceProvider.GetRequiredService<CratebaseDbContext>();
        await context.Database.MigrateAsync(cancellationToken);
    }

    private sealed class ConfiguredApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _connectionString;

        public ConfiguredApiFactory(string connectionString)
        {
            _connectionString = connectionString;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            _ = builder.UseSetting("ConnectionStrings:Cratebase", _connectionString);
        }
    }

    private sealed record AuthRequest(string Email, string Password);
}
