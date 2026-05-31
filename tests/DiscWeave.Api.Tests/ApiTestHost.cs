using DiscWeave.Domain.Catalog;
using DiscWeave.Domain.Credits;
using DiscWeave.Domain.SharedKernel.Ids;
using DiscWeave.Infrastructure.Identity;
using DiscWeave.Infrastructure.Persistence;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DiscWeave.Api.Tests;

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
        return await CreateAsync(postgres, new Dictionary<string, string?>(), cancellationToken: cancellationToken);
    }

    public static async Task<ApiTestHost> CreateAsync(
        PostgresFixture postgres,
        Action<IServiceCollection> configureServices,
        CancellationToken cancellationToken = default)
    {
        return await CreateAsync(postgres, new Dictionary<string, string?>(), configureServices: configureServices, cancellationToken: cancellationToken);
    }

    public static async Task<ApiTestHost> CreateAsync(
        PostgresFixture postgres,
        string environmentName,
        CancellationToken cancellationToken = default)
    {
        return await CreateAsync(postgres, new Dictionary<string, string?>(), environmentName, cancellationToken: cancellationToken);
    }

    public static async Task<ApiTestHost> CreateAsync(
        PostgresFixture postgres,
        IReadOnlyDictionary<string, string?> settings,
        string environmentName = "Development",
        Action<IServiceCollection>? configureServices = null,
        CancellationToken cancellationToken = default)
    {
        string connectionString = await postgres.CreateDatabaseAsync(cancellationToken);
        WebApplicationFactory<Program> factory = new ConfiguredApiFactory(connectionString, settings, environmentName, configureServices);

        var host = new ApiTestHost(factory);
        await host.MigrateAsync(cancellationToken);

        return host;
    }

    public HttpClient CreateClient()
    {
        return _factory.CreateClient();
    }

    public HttpClient CreateClient(Uri baseAddress)
    {
        return _factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = baseAddress });
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

        DefaultCollectionId = await FindDefaultCollectionIdForUserAsync(email, cancellationToken)
            ?? throw new InvalidOperationException("Authenticated test user default collection was not created");

        return client;
    }

    public async Task<CollectionId?> FindDefaultCollectionIdForUserAsync(string email, CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        DiscWeaveDbContext context = scope.ServiceProvider.GetRequiredService<DiscWeaveDbContext>();
        DiscWeaveUser? user = await context.Users.SingleOrDefaultAsync(user => user.Email == email, cancellationToken);

        return user?.DefaultCollectionId;
    }

    public async Task<bool> CollectionExistsAsync(CollectionId collectionId, CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        DiscWeaveDbContext context = scope.ServiceProvider.GetRequiredService<DiscWeaveDbContext>();

        return await context.MusicCollections.AnyAsync(collection => collection.Id == collectionId, cancellationToken);
    }

    public async Task SeedInviteForUserAsync(
        string creatorEmail,
        string codeHash,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        DiscWeaveDbContext context = scope.ServiceProvider.GetRequiredService<DiscWeaveDbContext>();
        DiscWeaveUser creator = await context.Users.SingleAsync(user => user.Email == creatorEmail, cancellationToken);

        _ = context.Invites.Add(Invite.Create(Guid.CreateVersion7(), codeHash, creator.Id, null, expiresAt, createdAt));
        _ = await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<ArtistId> SeedArtistAsync(Artist artist, CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        DiscWeaveDbContext context = scope.ServiceProvider.GetRequiredService<DiscWeaveDbContext>();
        _ = context.Artists.Add(artist);
        _ = await context.SaveChangesAsync(cancellationToken);

        return artist.Id;
    }

    public async Task<Artist?> FindArtistAsync(ArtistId artistId, CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        DiscWeaveDbContext context = scope.ServiceProvider.GetRequiredService<DiscWeaveDbContext>();

        return await context.Artists.SingleOrDefaultAsync(artist => artist.Id == artistId, cancellationToken);
    }

    public async Task SeedReleaseCreditAsync(Artist artist, CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        DiscWeaveDbContext context = scope.ServiceProvider.GetRequiredService<DiscWeaveDbContext>();
        var release = Release.Create(artist.CollectionId, ReleaseId.New(), "Confusion");
        _ = context.Releases.Add(release);
        _ = context.Credits.Add(Credit.Create(artist.CollectionId, CreditId.New(), CreditContributor.FromArtist(artist), CreditTarget.ForRelease(release.Id), CreditRole.Producer));
        _ = await context.SaveChangesAsync(cancellationToken);
    }

    public async Task SeedReleasePageAsync(
        int releaseCount,
        int tracksPerRelease,
        CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        DiscWeaveDbContext context = scope.ServiceProvider.GetRequiredService<DiscWeaveDbContext>();
        var artist = Person.Create(DefaultCollectionId, ArtistId.New(), "Seed Page Artist");
        _ = context.Artists.Add(artist);

        for (int releaseIndex = 0; releaseIndex < releaseCount; releaseIndex++)
        {
            var release = Release.Create(DefaultCollectionId, ReleaseId.New(), $"Seed Page Release {releaseIndex:000}");
            _ = context.Releases.Add(release);
            _ = context.Credits.Add(Credit.Create(DefaultCollectionId, CreditId.New(), CreditContributor.FromArtist(artist), CreditTarget.ForRelease(release.Id), CreditRole.MainArtist));

            List<ReleaseTrack> tracklist = [];
            for (int trackIndex = 0; trackIndex < tracksPerRelease; trackIndex++)
            {
                var track = Track.Create(DefaultCollectionId, TrackId.New(), $"Seed Page Track {releaseIndex:000}-{trackIndex:00}");
                _ = context.Tracks.Add(track);
                _ = context.Credits.Add(Credit.Create(DefaultCollectionId, CreditId.New(), CreditContributor.FromArtist(artist), CreditTarget.ForTrack(track.Id), CreditRole.MainArtist));
                tracklist.Add(ReleaseTrack.Create(track.Id, TrackPosition.FromNumber(trackIndex + 1)));
            }

            release.ReplaceTracklist(tracklist);
        }

        _ = await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Guid> SeedDigitalOwnedItemWithoutFormatAsync(Guid releaseId, CancellationToken cancellationToken = default)
    {
        var ownedItemId = Guid.CreateVersion7();
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        DiscWeaveDbContext context = scope.ServiceProvider.GetRequiredService<DiscWeaveDbContext>();
        _ = await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO owned_items (
                collection_id,
                owned_item_id,
                digital_file_path,
                medium_type,
                ownership_status,
                target_release_id,
                target_type)
            VALUES (
                {DefaultCollectionId.Value},
                {ownedItemId},
                {"/music/missing-format-file"},
                {"digital"},
                {"Owned"},
                {releaseId},
                {"release"})
            """,
            cancellationToken);

        return ownedItemId;
    }

    public async Task<DigitalImportIdentity?> FindDigitalImportIdentityAsync(Guid ownedItemId, CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        DiscWeaveDbContext context = scope.ServiceProvider.GetRequiredService<DiscWeaveDbContext>();

        return await context.OwnedItems.AsNoTracking()
            .Where(item => item.Id == new OwnedItemId(ownedItemId))
            .Select(item => new DigitalImportIdentity(
                EF.Property<string?>(item, "_importIdentityPath"),
                EF.Property<long?>(item, "_importIdentitySizeBytes"),
                EF.Property<DateTimeOffset?>(item, "_importIdentityLastModifiedAt"),
                EF.Property<string?>(item, "_importIdentityContentHash")))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();
        await ValueTask.CompletedTask;
    }

    private async Task MigrateAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        DiscWeaveDbContext context = scope.ServiceProvider.GetRequiredService<DiscWeaveDbContext>();
        await context.Database.MigrateAsync(cancellationToken);
    }

    private sealed class ConfiguredApiFactory(
        string connectionString,
        IReadOnlyDictionary<string, string?> settings,
        string environmentName,
        Action<IServiceCollection>? configureServices)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            _ = builder.UseSetting("ConnectionStrings:DiscWeave", connectionString);
            _ = builder.UseEnvironment(environmentName);
            _ = builder.ConfigureAppConfiguration((context, configuration) =>
            {
                _ = context;
                if (settings.Count > 0)
                {
                    _ = configuration.AddInMemoryCollection(settings);
                }
            });
            _ = builder.ConfigureServices(services => configureServices?.Invoke(services));
        }
    }

    private sealed record AuthRequest(string Email, string Password);
}

internal sealed record DigitalImportIdentity(
    string? Path,
    long? SizeBytes,
    DateTimeOffset? LastModifiedAt,
    string? ContentHash);
