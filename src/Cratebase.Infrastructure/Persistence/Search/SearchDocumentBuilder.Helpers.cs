using Cratebase.Domain.Catalog;
using Cratebase.Domain.Collection;
using Cratebase.Domain.Credits;
using Cratebase.Domain.Relations;
using Cratebase.Domain.Settings;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Infrastructure.Persistence.Queries;

namespace Cratebase.Infrastructure.Persistence.Search;

internal static partial class SearchDocumentBuilder
{
    private static string[] OwnedItemSearchParts(OwnedItem item, Data data)
    {
        return [StatusCode(item.Holding.Status), item.Holding.Medium.Code, Label(data, DictionaryKind.MediaType, item.Holding.Medium.Code), MediumDescription(item.Holding.Medium), item.Holding.Details.StorageLocation.Match(value => value.Name, () => string.Empty), item.Holding.Details.Condition.Match(value => value.ToString(), () => string.Empty)];
    }

    private static string[] CollectorSignals(IReadOnlyList<OwnedItem> items)
    {
        bool hasDigital = items.Any(item => item.Holding.Medium is DigitalFile);
        bool hasPhysical = items.Any(item => item.Holding.Medium is not DigitalFile);
        bool hasLossless = items.Any(item => item.Holding.Medium is DigitalFile digital && IsLossless(digital.Format));
        bool hasLossy = items.Any(item => item.Holding.Medium is DigitalFile digital && !IsLossless(digital.Format));
        List<string> signals = [.. items.Select(item => item.Holding.Medium.Code), .. items.Select(item => StatusCode(item.Holding.Status))];
        if (hasPhysical && !hasDigital)
        {
            signals.Add("physicalWithoutDigital");
        }

        if (hasLossy && !hasLossless)
        {
            signals.Add("lossyWithoutLossless");
        }

        if (items.Any(item => item.Holding.Status == OwnershipStatus.Wanted) && !items.Any(item => item.Holding.Status == OwnershipStatus.Owned))
        {
            signals.Add("wantedNotOwned");
        }

        return [.. signals];
    }

    private static string CreditTargetTitle(Credit credit, Data data)
    {
        return credit.Target switch
        {
            ReleaseCreditTarget target when data.Releases.TryGetValue(target.ReleaseId, out Release? release) => release.Summary.Title,
            TrackCreditTarget target when data.Tracks.TryGetValue(target.TrackId, out Track? track) => track.Title,
            _ => string.Empty
        };
    }

    private static string Label(Data data, DictionaryKind kind, string code)
    {
        return data.Dictionaries.LabelOrCode(kind, code);
    }

    private static string StatusCode(OwnershipStatus status)
    {
        return status switch
        {
            OwnershipStatus.Owned => "owned",
            OwnershipStatus.Wanted => "wanted",
            OwnershipStatus.Sold => "sold",
            OwnershipStatus.NeedsDigitization => "needsDigitization",
            _ => throw new InvalidOperationException("Ownership status is not supported")
        };
    }

    private static string MediumDescription(IMedium medium)
    {
        return medium switch
        {
            DigitalFile file => file.Format.ToString(),
            VinylRecord vinyl => vinyl.FormatDescription,
            CompactDisc disc => $"{disc.DiscCount} discs",
            CassetteTape cassette => cassette.TapeType,
            OtherMedium other => other.Name,
            _ => medium.Code
        };
    }

    private static bool IsLossless(AudioFileFormat format)
    {
        return format is AudioFileFormat.Flac or AudioFileFormat.Wav or AudioFileFormat.Aiff or AudioFileFormat.Alac;
    }

    private sealed record Data(
        Dictionary<ArtistId, Artist> Artists,
        Dictionary<LabelId, Label> Labels,
        Dictionary<ReleaseId, Release> Releases,
        Dictionary<TrackId, Track> Tracks,
        IReadOnlyList<OwnedItem> OwnedItems,
        IReadOnlyList<Credit> Credits,
        IReadOnlyList<ArtistRelation> ArtistRelations,
        IReadOnlyList<TrackRelation> TrackRelations,
        DictionarySearchLookup Dictionaries);

    private sealed record SearchDocumentContent(
        CollectionId CollectionId,
        string EntityType,
        Guid EntityId,
        string Title)
    {
        public string? Subtitle { get; init; }

        public string? Summary { get; init; }

        public string[] MatchedFields { get; init; } = [];

        public string[] SearchParts { get; init; } = [];

        public string[] Roles { get; init; } = [];

        public string[] Media { get; init; } = [];

        public string[] Statuses { get; init; } = [];

        public string[] Tags { get; init; } = [];

        public Guid? LabelId { get; init; }

        public string[] Signals { get; init; } = [];

        public Guid[] LabelIds { get; init; } = [];
    }
}
