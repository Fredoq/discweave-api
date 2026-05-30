using DiscWeave.Domain.Catalog;
using DiscWeave.Domain.Collection;
using DiscWeave.Domain.Credits;
using DiscWeave.Domain.Playlists;
using DiscWeave.Domain.Relations;
using DiscWeave.Domain.Settings;
using DiscWeave.Domain.SharedKernel.Ids;
using DiscWeave.Domain.SharedKernel.Optional;
using DiscWeave.Infrastructure.Persistence.Queries;
using Microsoft.EntityFrameworkCore;

namespace DiscWeave.Infrastructure.Persistence.Search;

internal static partial class SearchDocumentBuilder
{
    private const string MediumMatchedField = "medium";
    private const string OwnershipStatusMatchedField = "ownershipStatus";

    public static async Task<IReadOnlyList<SearchDocument>> BuildAsync(
        DiscWeaveDbContext context,
        CollectionId collectionId,
        CancellationToken cancellationToken)
    {
        Artist[] artists = await context.Artists.AsNoTracking().Where(item => item.CollectionId == collectionId).ToArrayAsync(cancellationToken);
        Label[] labels = await context.Labels.AsNoTracking().Where(item => item.CollectionId == collectionId).ToArrayAsync(cancellationToken);
        Release[] releases = await context.Releases.AsNoTracking().Include("_genres").Include("_tags").Where(item => item.CollectionId == collectionId).ToArrayAsync(cancellationToken);
        Track[] tracks = await context.Tracks.AsNoTracking().Include("_genres").Include("_tags").Where(item => item.CollectionId == collectionId).ToArrayAsync(cancellationToken);
        OwnedItem[] ownedItems = await context.OwnedItems.AsNoTracking().Where(item => item.CollectionId == collectionId).ToArrayAsync(cancellationToken);
        Playlist[] playlists = await context.Playlists.AsNoTracking().Include(item => item.Entries).Where(item => item.CollectionId == collectionId).ToArrayAsync(cancellationToken);
        Credit[] credits = await context.Credits.AsNoTracking().Where(item => item.CollectionId == collectionId).ToArrayAsync(cancellationToken);
        ArtistRelation[] artistRelations = await context.ArtistRelations.AsNoTracking().Where(item => item.CollectionId == collectionId).ToArrayAsync(cancellationToken);
        TrackRelation[] trackRelations = await context.TrackRelations.AsNoTracking().Where(item => item.CollectionId == collectionId).ToArrayAsync(cancellationToken);
        CollectionDictionaryEntry[] entries = await context.CollectionDictionaryEntries.AsNoTracking().Where(item => item.CollectionId == collectionId).ToArrayAsync(cancellationToken);
        Dictionary<ReleaseId, OwnedItem[]> ownedItemsByReleaseId = BuildOwnedItemsByReleaseId(ownedItems);
        Dictionary<TrackId, OwnedItem[]> ownedItemsByTrackId = BuildOwnedItemsByTrackId(ownedItems);

        Data data = new(
            artists.ToDictionary(item => item.Id),
            labels.ToDictionary(item => item.Id),
            releases.ToDictionary(item => item.Id),
            tracks.ToDictionary(item => item.Id),
            ownedItems,
            playlists,
            ownedItemsByReleaseId,
            ownedItemsByTrackId,
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
            .. ownedItems.Select(item => OwnedItemDocument(item, data)),
            .. playlists.Select(item => PlaylistDocument(item, data))
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
            new SearchDocumentContent(artist.CollectionId, "artist", artist.Id.Value, artist.Name)
            {
                Subtitle = SearchResultCodes.ArtistType(artist),
                Summary = string.Join(", ", roles.Concat(relationTypes)),
                MatchedFields = ["name", "credit.role", "credit.contributor", "relation.type"],
                SearchParts = [artist.Name, .. targets, .. roles.Select(role => Label(data, DictionaryKind.CreditRole, role)), .. relationTypes.Select(type => Label(data, DictionaryKind.ArtistRelationType, type))],
                Roles = roles
            });
    }

    private static SearchDocument LabelDocument(Label label, Data data)
    {
        Release[] releases = [.. data.Releases.Values.Where(release => ReleaseLabelIds(release).Contains(label.Id))];
        OwnedItem[] ownedItems = [.. data.OwnedItems.Where(item => releases.Any(release => item.Target is ReleaseOwnedItemTarget target && target.ReleaseId == release.Id))];

        return ToDocument(
            new SearchDocumentContent(label.CollectionId, "label", label.Id.Value, label.Name)
            {
                Subtitle = "label",
                Summary = $"{releases.Length} releases",
                MatchedFields = ["name", "label"],
                SearchParts = [label.Name, .. releases.Select(release => release.Summary.Title), .. ownedItems.Select(item => item.Holding.Medium.Code)],
                Media = [.. ownedItems.Select(item => item.Holding.Medium.Code).Distinct(StringComparer.OrdinalIgnoreCase)],
                Statuses = [.. ownedItems.Select(item => StatusCode(item.Holding.Status)).Distinct(StringComparer.OrdinalIgnoreCase)],
                LabelId = label.Id.Value,
                LabelIds = [label.Id.Value],
                Signals = CollectorSignals(ownedItems)
            });
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
            new SearchDocumentContent(release.CollectionId, "release", release.Id.Value, release.Summary.Title)
            {
                Subtitle = labelNames.FirstOrDefault() ?? "release",
                Summary = string.Join(", ", tags),
                MatchedFields = ["title", "release.type", "label", "genre", "tag", "credit.role", "credit.contributor", MediumMatchedField, OwnershipStatusMatchedField],
                SearchParts = [release.Summary.Title, release.Summary.Metadata.Type, Label(data, DictionaryKind.ReleaseType, release.Summary.Metadata.Type), .. labelNames, .. tags, .. credits.SelectMany(credit => new[] { credit.Contributor.Name, Label(data, DictionaryKind.CreditRole, credit.Role), credit.Role }), .. ownedItems.SelectMany(item => OwnedItemSearchParts(item, data))],
                Roles = roles,
                Media = [.. ownedItems.Select(item => item.Holding.Medium.Code).Distinct(StringComparer.OrdinalIgnoreCase)],
                Statuses = [.. ownedItems.Select(item => StatusCode(item.Holding.Status)).Distinct(StringComparer.OrdinalIgnoreCase)],
                Tags = tags,
                LabelId = primaryLabelId,
                LabelIds = [.. labelIds.Select(id => id.Value)],
                Signals = CollectorSignals(ownedItems)
            });
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
            new SearchDocumentContent(track.CollectionId, "track", track.Id.Value, track.Title)
            {
                Subtitle = releases.FirstOrDefault()?.Summary.Title,
                Summary = string.Join(", ", tags),
                MatchedFields = ["title", "genre", "tag", "credit.role", "credit.contributor", "relation.type", MediumMatchedField, OwnershipStatusMatchedField],
                SearchParts = [track.Title, .. tags, .. releases.Select(release => release.Summary.Title), .. credits.SelectMany(credit => new[] { credit.Contributor.Name, Label(data, DictionaryKind.CreditRole, credit.Role), credit.Role }), .. relations.Select(relation => Label(data, DictionaryKind.TrackRelationType, relation.RelationType)), .. ownedItems.SelectMany(item => OwnedItemSearchParts(item, data))],
                Roles = roles,
                Media = [.. ownedItems.Select(item => item.Holding.Medium.Code).Distinct(StringComparer.OrdinalIgnoreCase)],
                Statuses = [.. ownedItems.Select(item => StatusCode(item.Holding.Status)).Distinct(StringComparer.OrdinalIgnoreCase)],
                Tags = tags,
                Signals = CollectorSignals(ownedItems)
            });
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
            new SearchDocumentContent(item.CollectionId, "ownedItem", item.Id.Value, title)
            {
                Subtitle = $"{status} on {medium}",
                MatchedFields = [OwnershipStatusMatchedField, MediumMatchedField, "title"],
                SearchParts = [title, .. OwnedItemSearchParts(item, data)],
                Media = [medium],
                Statuses = [status],
                Signals = CollectorSignals(TargetOwnedItems(item, data))
            });
    }

    private static SearchDocument PlaylistDocument(Playlist playlist, Data data)
    {
        SmartPlaylistRules rules = playlist.Rules;
        string[] referencedTitles =
        [
            .. playlist.Entries.Select(entry => PlaylistEntryTitle(entry, data)).Where(value => value.Length > 0)
        ];
        string[] ruleParts =
        [
            .. rules.Tags,
            .. rules.Genres,
            .. rules.Media,
            .. rules.OwnershipStatuses,
            OptionalIntText(rules.YearFrom),
            OptionalIntText(rules.YearTo)
        ];
        string description = OptionalStringText(playlist.Description);

        return ToDocument(
            new SearchDocumentContent(playlist.CollectionId, "playlist", playlist.Id.Value, playlist.Name)
            {
                Subtitle = playlist.Type == PlaylistType.Manual ? "manual playlist" : "smart playlist",
                Summary = description.Length == 0 ? null : description,
                MatchedFields = ["name", "playlist", "track", "release", "tag", "genre", MediumMatchedField, OwnershipStatusMatchedField],
                SearchParts = [playlist.Name, description, playlist.Type.ToString(), .. referencedTitles, .. ruleParts],
                Media = [.. rules.Media],
                Statuses = [.. rules.OwnershipStatuses],
                Tags = [.. rules.Tags.Concat(rules.Genres)]
            });
    }

    private static string OptionalStringText(IOptionalValue<string> value)
    {
        return value is PresentOptionalValue<string> present ? present.Value : string.Empty;
    }

    private static string OptionalIntText(IOptionalValue<int> value)
    {
        return value is PresentOptionalValue<int> present
            ? present.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : string.Empty;
    }

    private static string PlaylistEntryTitle(PlaylistEntry entry, Data data)
    {
        return entry switch
        {
            { Kind: PlaylistEntry.ReleaseKind, ReleaseId: PresentOptionalValue<ReleaseId> releaseId } when data.Releases.TryGetValue(releaseId.Value, out Release? release) => release.Summary.Title,
            { Kind: PlaylistEntry.TrackKind, TrackId: PresentOptionalValue<TrackId> trackId } when data.Tracks.TryGetValue(trackId.Value, out Track? track) => track.Title,
            _ => string.Empty
        };
    }

    private static OwnedItem[] TargetOwnedItems(OwnedItem item, Data data)
    {
        return item.Target switch
        {
            ReleaseOwnedItemTarget target when data.OwnedItemsByReleaseId.TryGetValue(target.ReleaseId, out OwnedItem[]? items) => items,
            TrackOwnedItemTarget target when data.OwnedItemsByTrackId.TryGetValue(target.TrackId, out OwnedItem[]? items) => items,
            _ => [item]
        };
    }

    private static Dictionary<ReleaseId, OwnedItem[]> BuildOwnedItemsByReleaseId(IReadOnlyList<OwnedItem> ownedItems)
    {
        return ownedItems
            .Where(item => item.Target is ReleaseOwnedItemTarget)
            .GroupBy(item => ((ReleaseOwnedItemTarget)item.Target).ReleaseId)
            .ToDictionary(group => group.Key, group => group.ToArray());
    }

    private static Dictionary<TrackId, OwnedItem[]> BuildOwnedItemsByTrackId(IReadOnlyList<OwnedItem> ownedItems)
    {
        return ownedItems
            .Where(item => item.Target is TrackOwnedItemTarget)
            .GroupBy(item => ((TrackOwnedItemTarget)item.Target).TrackId)
            .ToDictionary(group => group.Key, group => group.ToArray());
    }

    private static SearchDocument ToDocument(SearchDocumentContent content)
    {
        return new SearchDocument
        {
            CollectionId = content.CollectionId,
            EntityType = content.EntityType,
            EntityId = content.EntityId,
            Title = content.Title,
            Subtitle = content.Subtitle,
            Summary = content.Summary,
            SearchText = string.Join(' ', content.SearchParts.Where(part => !string.IsNullOrWhiteSpace(part))),
            MatchedFields = SearchDocumentText.Pack(content.MatchedFields),
            Snippets = SearchDocumentText.Pack(content.SearchParts.Where(part => !string.IsNullOrWhiteSpace(part)).Take(6)),
            RoleFacet = SearchDocumentText.Facet(content.Roles),
            MediaFacet = SearchDocumentText.Facet(content.Media),
            StatusFacet = SearchDocumentText.Facet(content.Statuses),
            TagFacet = SearchDocumentText.Facet(content.Tags),
            LabelId = content.LabelId,
            LabelIdFacet = SearchDocumentText.Facet(content.LabelIds.Select(id => id.ToString("D"))),
            CollectorSignalFacet = SearchDocumentText.Facet(content.Signals)
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

}
