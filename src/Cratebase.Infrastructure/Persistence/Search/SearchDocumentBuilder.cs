using Cratebase.Domain.Catalog;
using Cratebase.Domain.Collection;
using Cratebase.Domain.Credits;
using Cratebase.Domain.Relations;
using Cratebase.Domain.Settings;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Infrastructure.Persistence.Queries;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Infrastructure.Persistence.Search;

internal static class SearchDocumentBuilder
{
    public static async Task<IReadOnlyList<SearchDocument>> BuildAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        CancellationToken cancellationToken)
    {
        Artist[] artists = await context.Artists.AsNoTracking().Where(item => item.CollectionId == collectionId).ToArrayAsync(cancellationToken);
        Label[] labels = await context.Labels.AsNoTracking().Where(item => item.CollectionId == collectionId).ToArrayAsync(cancellationToken);
        Release[] releases = await context.Releases.AsNoTracking().Include("_genres").Include("_tags").Where(item => item.CollectionId == collectionId).ToArrayAsync(cancellationToken);
        Track[] tracks = await context.Tracks.AsNoTracking().Include("_genres").Include("_tags").Where(item => item.CollectionId == collectionId).ToArrayAsync(cancellationToken);
        OwnedItem[] ownedItems = await context.OwnedItems.AsNoTracking().Where(item => item.CollectionId == collectionId).ToArrayAsync(cancellationToken);
        Credit[] credits = await context.Credits.AsNoTracking().Where(item => item.CollectionId == collectionId).ToArrayAsync(cancellationToken);
        ArtistRelation[] artistRelations = await context.ArtistRelations.AsNoTracking().Where(item => item.CollectionId == collectionId).ToArrayAsync(cancellationToken);
        TrackRelation[] trackRelations = await context.TrackRelations.AsNoTracking().Where(item => item.CollectionId == collectionId).ToArrayAsync(cancellationToken);
        CollectionDictionaryEntry[] entries = await context.CollectionDictionaryEntries.AsNoTracking().Where(item => item.CollectionId == collectionId).ToArrayAsync(cancellationToken);

        Data data = new(
            artists.ToDictionary(item => item.Id),
            labels.ToDictionary(item => item.Id),
            releases.ToDictionary(item => item.Id),
            tracks.ToDictionary(item => item.Id),
            ownedItems,
            credits,
            artistRelations,
            trackRelations,
            DictionarySearchLookup.From(entries));

        return
        [
            .. artists.Select(item => ArtistDocument(item, data)),
            .. labels.Select(item => LabelDocument(item, data)),
            .. releases.Select(item => ReleaseDocument(item, data)),
            .. tracks.Select(item => TrackDocument(item, data)),
            .. ownedItems.Select(item => OwnedItemDocument(item, data))
        ];
    }

    private static SearchDocument ArtistDocument(Artist artist, Data data)
    {
        Credit[] credits = [.. data.Credits.Where(credit => credit.Contributor.ArtistId == artist.Id)];
        ArtistRelation[] relations = [.. data.ArtistRelations.Where(relation => relation.SourceArtistId == artist.Id || relation.TargetArtistId == artist.Id)];
        string[] roles = [.. credits.Select(credit => credit.Role).Distinct(StringComparer.OrdinalIgnoreCase)];
        string[] relationTypes = [.. relations.Select(relation => relation.Type).Distinct(StringComparer.OrdinalIgnoreCase)];
        string[] targets = [.. credits.Select(credit => CreditTargetTitle(credit, data)).Where(value => value.Length > 0)];

        return ToDocument(
            artist.CollectionId,
            "artist",
            artist.Id.Value,
            artist.Name,
            SearchResultCodes.ArtistType(artist),
            string.Join(", ", roles.Concat(relationTypes)),
            ["name", "credit.role", "credit.contributor", "relation.type"],
            [artist.Name, .. targets, .. roles.Select(role => Label(data, DictionaryKind.CreditRole, role)), .. relationTypes.Select(type => Label(data, DictionaryKind.ArtistRelationType, type))],
            roles,
            [],
            [],
            [],
            null,
            []);
    }

    private static SearchDocument LabelDocument(Label label, Data data)
    {
        Release[] releases = [.. data.Releases.Values.Where(release => ReleaseLabelIds(release).Contains(label.Id))];
        OwnedItem[] ownedItems = [.. data.OwnedItems.Where(item => releases.Any(release => item.Target is ReleaseOwnedItemTarget target && target.ReleaseId == release.Id))];

        return ToDocument(
            label.CollectionId,
            "label",
            label.Id.Value,
            label.Name,
            "label",
            $"{releases.Length} releases",
            ["name", "label"],
            [label.Name, .. releases.Select(release => release.Summary.Title), .. ownedItems.Select(item => item.Holding.Medium.Code)],
            [],
            [.. ownedItems.Select(item => item.Holding.Medium.Code).Distinct(StringComparer.OrdinalIgnoreCase)],
            [.. ownedItems.Select(item => StatusCode(item.Holding.Status)).Distinct(StringComparer.OrdinalIgnoreCase)],
            [],
            label.Id.Value,
            CollectorSignals(ownedItems));
    }

    private static SearchDocument ReleaseDocument(Release release, Data data)
    {
        LabelId[] labelIds = [.. ReleaseLabelIds(release)];
        string[] labelNames = [.. labelIds.Select(id => data.Labels.GetValueOrDefault(id)?.Name).Where(value => value is not null).Select(value => value!)];
        Credit[] credits = [.. data.Credits.Where(credit => credit.Target is ReleaseCreditTarget target && target.ReleaseId == release.Id)];
        OwnedItem[] ownedItems = [.. data.OwnedItems.Where(item => item.Target is ReleaseOwnedItemTarget target && target.ReleaseId == release.Id)];
        string[] roles = [.. credits.Select(credit => credit.Role).Distinct(StringComparer.OrdinalIgnoreCase)];
        string[] tags = [.. release.Cataloging.Tags.Select(tag => tag.Name).Concat(release.Cataloging.Genres.Select(genre => genre.Name)).Distinct(StringComparer.OrdinalIgnoreCase)];
        Guid? primaryLabelId = labelIds.Length == 0 ? null : labelIds[0].Value;

        return ToDocument(
            release.CollectionId,
            "release",
            release.Id.Value,
            release.Summary.Title,
            labelNames.FirstOrDefault() ?? "release",
            string.Join(", ", tags),
            ["title", "release.type", "label", "genre", "tag", "credit.role", "credit.contributor", "medium", "ownershipStatus"],
            [release.Summary.Title, release.Summary.Metadata.Type, Label(data, DictionaryKind.ReleaseType, release.Summary.Metadata.Type), .. labelNames, .. tags, .. credits.SelectMany(credit => new[] { credit.Contributor.Name, Label(data, DictionaryKind.CreditRole, credit.Role), credit.Role }), .. ownedItems.SelectMany(item => OwnedItemSearchParts(item, data))],
            roles,
            [.. ownedItems.Select(item => item.Holding.Medium.Code).Distinct(StringComparer.OrdinalIgnoreCase)],
            [.. ownedItems.Select(item => StatusCode(item.Holding.Status)).Distinct(StringComparer.OrdinalIgnoreCase)],
            tags,
            primaryLabelId,
            CollectorSignals(ownedItems));
    }

    private static SearchDocument TrackDocument(Track track, Data data)
    {
        Credit[] credits = [.. data.Credits.Where(credit => credit.Target is TrackCreditTarget target && target.TrackId == track.Id)];
        OwnedItem[] ownedItems = [.. data.OwnedItems.Where(item => item.Target is TrackOwnedItemTarget target && target.TrackId == track.Id)];
        TrackRelation[] relations = [.. data.TrackRelations.Where(relation => relation.SourceTrackId == track.Id || relation.TargetTrackId == track.Id)];
        Release[] releases = [.. data.Releases.Values.Where(release => release.Tracklist.Any(item => item.TrackId == track.Id))];
        string[] roles = [.. credits.Select(credit => credit.Role).Distinct(StringComparer.OrdinalIgnoreCase)];
        string[] tags = [.. track.Cataloging.Tags.Select(tag => tag.Name).Concat(track.Cataloging.Genres.Select(genre => genre.Name)).Distinct(StringComparer.OrdinalIgnoreCase)];

        return ToDocument(
            track.CollectionId,
            "track",
            track.Id.Value,
            track.Title,
            releases.FirstOrDefault()?.Summary.Title,
            string.Join(", ", tags),
            ["title", "genre", "tag", "credit.role", "credit.contributor", "relation.type", "medium", "ownershipStatus"],
            [track.Title, .. tags, .. releases.Select(release => release.Summary.Title), .. credits.SelectMany(credit => new[] { credit.Contributor.Name, Label(data, DictionaryKind.CreditRole, credit.Role), credit.Role }), .. relations.Select(relation => Label(data, DictionaryKind.TrackRelationType, relation.RelationType)), .. ownedItems.SelectMany(item => OwnedItemSearchParts(item, data))],
            roles,
            [.. ownedItems.Select(item => item.Holding.Medium.Code).Distinct(StringComparer.OrdinalIgnoreCase)],
            [.. ownedItems.Select(item => StatusCode(item.Holding.Status)).Distinct(StringComparer.OrdinalIgnoreCase)],
            tags,
            null,
            CollectorSignals(ownedItems));
    }

    private static SearchDocument OwnedItemDocument(OwnedItem item, Data data)
    {
        string title = item.Target switch
        {
            ReleaseOwnedItemTarget target when data.Releases.TryGetValue(target.ReleaseId, out Release? release) => release.Summary.Title,
            TrackOwnedItemTarget target when data.Tracks.TryGetValue(target.TrackId, out Track? track) => track.Title,
            _ => "Owned item"
        };
        string status = StatusCode(item.Holding.Status);
        string medium = item.Holding.Medium.Code;

        return ToDocument(
            item.CollectionId,
            "ownedItem",
            item.Id.Value,
            title,
            $"{status} on {medium}",
            null,
            ["ownershipStatus", "medium", "title"],
            [title, .. OwnedItemSearchParts(item, data)],
            [],
            [medium],
            [status],
            [],
            null,
            CollectorSignals([item]));
    }

    private static SearchDocument ToDocument(CollectionId collectionId, string entityType, Guid entityId, string title, string? subtitle, string? summary, string[] matchedFields, string[] searchParts, string[] roles, string[] media, string[] statuses, string[] tags, Guid? labelId, string[] signals)
    {
        return new SearchDocument
        {
            CollectionId = collectionId,
            EntityType = entityType,
            EntityId = entityId,
            Title = title,
            Subtitle = subtitle,
            Summary = summary,
            SearchText = string.Join(' ', searchParts.Where(part => !string.IsNullOrWhiteSpace(part))),
            MatchedFields = SearchDocumentText.Pack(matchedFields),
            Snippets = SearchDocumentText.Pack(searchParts.Where(part => !string.IsNullOrWhiteSpace(part)).Take(6)),
            RoleFacet = SearchDocumentText.Facet(roles),
            MediaFacet = SearchDocumentText.Facet(media),
            StatusFacet = SearchDocumentText.Facet(statuses),
            TagFacet = SearchDocumentText.Facet(tags),
            LabelId = labelId,
            CollectorSignalFacet = SearchDocumentText.Facet(signals)
        };
    }

    private static IEnumerable<LabelId> ReleaseLabelIds(Release release)
    {
        foreach (ReleaseLabel label in release.Labels)
        {
            yield return label.LabelId;
        }

        if (release.Summary.Metadata.LabelId.HasValue)
        {
            yield return release.Summary.Metadata.LabelId.Match(value => value, () => default);
        }
    }

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
}
