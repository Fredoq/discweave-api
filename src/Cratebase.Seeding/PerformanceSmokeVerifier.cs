using System.Diagnostics;
using Cratebase.Application.Search;
using Cratebase.Application.Security;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Infrastructure.Persistence;
using Cratebase.Infrastructure.Persistence.Queries;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Seeding;

public static class PerformanceSmokeVerifier
{
    private const string ImportIdentityPathProperty = "_importIdentityPath";

    public static async Task VerifyAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        TimeSpan budget,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(output);

        var searchQueries = new CollectionSearchQueries(context, new SeedCurrentCollection(collectionId));
        PerformanceProbe[] probes =
        [
            new("release list", token => VerifyReleaseListAsync(context, collectionId, token)),
            new("search", token => VerifySearchAsync(searchQueries, token)),
            new("relations", token => VerifyRelationsAsync(context, collectionId, token)),
            new("playlists", token => VerifyPlaylistsAsync(context, collectionId, token)),
            new("import deduplication", token => VerifyImportDeduplicationAsync(context, collectionId, token)),
            new("export read", token => VerifyExportReadAsync(context, collectionId, token))
        ];

        foreach (PerformanceProbe probe in probes)
        {
            await RunProbeAsync(probe, budget, output, cancellationToken);
        }
    }

    private static async Task RunProbeAsync(
        PerformanceProbe probe,
        TimeSpan budget,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        int total = await probe.ExecuteAsync(cancellationToken);
        stopwatch.Stop();

        if (total == 0)
        {
            await output.WriteLineAsync($"FAIL performance smoke {probe.Name} returned no results");
            throw new InvalidOperationException($"Performance smoke probe '{probe.Name}' returned no results");
        }

        string status = stopwatch.Elapsed <= budget ? "PASS" : "WARN";
        await output.WriteLineAsync(
            $"{status} performance smoke {probe.Name} {stopwatch.ElapsedMilliseconds} ms total={total}");
    }

    private static async Task<int> VerifyReleaseListAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        CancellationToken cancellationToken)
    {
        return await context.Releases.AsNoTracking()
            .Where(release => release.CollectionId == collectionId)
            .OrderBy(release => release.Id)
            .Take(50)
            .CountAsync(cancellationToken);
    }

    private static async Task<int> VerifySearchAsync(
        CollectionSearchQueries searchQueries,
        CancellationToken cancellationToken)
    {
        CollectionSearchResult result = await searchQueries.SearchAsync(
            new CollectionSearchQuery("Seed Release 00001", "release", null, null, null, null, null, null, 10, 0),
            cancellationToken);

        return result.Total;
    }

    private static async Task<int> VerifyRelationsAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        CancellationToken cancellationToken)
    {
        int artistRelations = await context.ArtistRelations.AsNoTracking()
            .Where(relation => relation.CollectionId == collectionId)
            .OrderBy(relation => relation.Id)
            .Take(50)
            .CountAsync(cancellationToken);
        int trackRelations = await context.TrackRelations.AsNoTracking()
            .Where(relation => relation.CollectionId == collectionId)
            .OrderBy(relation => relation.Id)
            .Take(50)
            .CountAsync(cancellationToken);

        return artistRelations + trackRelations;
    }

    private static async Task<int> VerifyPlaylistsAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        CancellationToken cancellationToken)
    {
        return await context.Playlists.AsNoTracking()
            .Where(playlist => playlist.CollectionId == collectionId)
            .OrderBy(playlist => playlist.Id)
            .Take(50)
            .CountAsync(cancellationToken);
    }

    private static async Task<int> VerifyImportDeduplicationAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        CancellationToken cancellationToken)
    {
        return await context.OwnedItems.AsNoTracking()
            .Where(item =>
                item.CollectionId == collectionId &&
                EF.Property<string?>(item, ImportIdentityPathProperty) != null)
            .OrderBy(item => item.Id)
            .Take(50)
            .CountAsync(cancellationToken);
    }

    private static async Task<int> VerifyExportReadAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        CancellationToken cancellationToken)
    {
        int artists = await context.Artists.AsNoTracking()
            .Where(artist => artist.CollectionId == collectionId)
            .Take(50)
            .CountAsync(cancellationToken);
        int labels = await context.Labels.AsNoTracking()
            .Where(label => label.CollectionId == collectionId)
            .Take(50)
            .CountAsync(cancellationToken);
        int releases = await context.Releases.AsNoTracking()
            .Where(release => release.CollectionId == collectionId)
            .Take(50)
            .CountAsync(cancellationToken);
        int tracks = await context.Tracks.AsNoTracking()
            .Where(track => track.CollectionId == collectionId)
            .Take(50)
            .CountAsync(cancellationToken);
        int ownedItems = await context.OwnedItems.AsNoTracking()
            .Where(item => item.CollectionId == collectionId)
            .Take(50)
            .CountAsync(cancellationToken);

        return artists + labels + releases + tracks + ownedItems;
    }

    private sealed record PerformanceProbe(string Name, Func<CancellationToken, Task<int>> ExecuteAsync);

    private sealed class SeedCurrentCollection : ICurrentCollection
    {
        public SeedCurrentCollection(CollectionId collectionId)
        {
            CollectionId = collectionId;
        }

        public CollectionId CollectionId { get; }
    }
}
