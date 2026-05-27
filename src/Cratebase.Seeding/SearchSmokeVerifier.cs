using System.Diagnostics;
using Cratebase.Application.Search;
using Cratebase.Application.Security;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Infrastructure.Persistence;
using Cratebase.Infrastructure.Persistence.Queries;

namespace Cratebase.Seeding;

public static class SearchSmokeVerifier
{
    private static readonly SearchProbe[] Probes =
    [
        new("title search", new CollectionSearchQuery("Seed Release 00001", "release", null, null, null, null, null, null, 10, 0)),
        new("producer role", new CollectionSearchQuery(string.Empty, "release", "producer", null, null, null, null, null, 10, 0)),
        new("remixes view", new CollectionSearchQuery(string.Empty, null, null, null, null, null, null, "remixes", 10, 0)),
        new("labels view", new CollectionSearchQuery(string.Empty, null, null, null, null, null, null, "labels", 10, 0)),
        new("ownership status", new CollectionSearchQuery(string.Empty, null, null, null, "owned", null, null, null, 10, 0)),
        new("media filter", new CollectionSearchQuery(string.Empty, null, null, "vinyl", null, null, null, null, 10, 0)),
        new("tag filter", new CollectionSearchQuery(string.Empty, null, null, null, null, null, "crate-01", null, 10, 0)),
        new("physical without digital", new CollectionSearchQuery(string.Empty, null, null, null, null, null, null, "physicalWithoutDigital", 10, 0)),
        new("lossy without lossless", new CollectionSearchQuery(string.Empty, null, null, null, null, null, null, "lossyWithoutLossless", 10, 0)),
        new("wanted not owned", new CollectionSearchQuery(string.Empty, null, null, null, null, null, null, "wantedNotOwned", 10, 0)),
        new("needs digitization", new CollectionSearchQuery(string.Empty, null, null, null, null, null, null, "needsDigitization", 10, 0))
    ];

    public static async Task VerifyAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        TimeSpan budget,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(output);

        var queries = new CollectionSearchQueries(context, new SeedCurrentCollection(collectionId));
        foreach (SearchProbe probe in Probes)
        {
            var stopwatch = Stopwatch.StartNew();
            CollectionSearchResult result = await queries.SearchAsync(probe.Query, cancellationToken);
            stopwatch.Stop();

            if (result.Total == 0)
            {
                await output.WriteLineAsync($"FAIL search smoke {probe.Name} returned no results");
                throw new InvalidOperationException($"Search smoke probe '{probe.Name}' returned no results");
            }

            string status = stopwatch.Elapsed <= budget ? "PASS" : "WARN";
            await output.WriteLineAsync(
                $"{status} search smoke {probe.Name} {stopwatch.ElapsedMilliseconds} ms total={result.Total}");
        }
    }

    private sealed record SearchProbe(string Name, CollectionSearchQuery Query);

    private sealed class SeedCurrentCollection : ICurrentCollection
    {
        public SeedCurrentCollection(CollectionId collectionId)
        {
            CollectionId = collectionId;
        }

        public CollectionId CollectionId { get; }
    }
}
