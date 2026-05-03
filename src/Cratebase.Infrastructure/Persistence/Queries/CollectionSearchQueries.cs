using Cratebase.Application.Search;
using Cratebase.Application.Security;
using Cratebase.Domain.Catalog;
using Cratebase.Domain.Collection;
using Cratebase.Domain.Credits;
using Cratebase.Domain.SharedKernel.Ids;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Infrastructure.Persistence.Queries;

public sealed class CollectionSearchQueries : ICollectionSearchQueries
{
    private const string ReleaseResultType = "release";
    private const string TrackResultType = "track";

    private readonly CratebaseDbContext _context;
    private readonly CollectionId _collectionId;

    public CollectionSearchQueries(CratebaseDbContext context, ICurrentCollection currentCollection)
    {
        _context = context;
        _collectionId = currentCollection.CollectionId;
    }

    public async Task<CollectionSearchResult> SearchAsync(CollectionSearchQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        string term = query.Query.Trim();
        SearchResultAccumulator accumulator = new();

        Artist[] artists = await _context.Artists.AsNoTracking()
            .Where(artist => artist.CollectionId == _collectionId)
            .OrderBy(artist => artist.Name)
            .ToArrayAsync(cancellationToken);
        Label[] labels = await _context.Labels.AsNoTracking()
            .Where(label => label.CollectionId == _collectionId)
            .OrderBy(label => label.Name)
            .ToArrayAsync(cancellationToken);
        Release[] releases = await _context.Releases.AsNoTracking()
            .Include("_genres")
            .Include("_tags")
            .Where(release => release.CollectionId == _collectionId)
            .OrderBy(release => release.Summary.Title)
            .ToArrayAsync(cancellationToken);
        Track[] tracks = await _context.Tracks.AsNoTracking()
            .Include("_genres")
            .Include("_tags")
            .Where(track => track.CollectionId == _collectionId)
            .OrderBy(track => track.Title)
            .ToArrayAsync(cancellationToken);
        Credit[] credits = await _context.Credits.AsNoTracking()
            .Where(credit => credit.CollectionId == _collectionId)
            .ToArrayAsync(cancellationToken);
        OwnedItem[] ownedItems = await _context.OwnedItems.AsNoTracking()
            .Where(item => item.CollectionId == _collectionId)
            .ToArrayAsync(cancellationToken);

        Dictionary<LabelId, string> labelNames = labels.ToDictionary(label => label.Id, label => label.Name);
        Dictionary<ReleaseId, Release> releasesById = releases.ToDictionary(release => release.Id);
        Dictionary<TrackId, Track> tracksById = tracks.ToDictionary(track => track.Id);

        AddArtists(term, accumulator, artists);
        AddLabels(term, accumulator, labels);
        AddReleases(term, accumulator, releases, labelNames);
        AddTracks(term, accumulator, tracks);
        AddCredits(term, accumulator, credits, releasesById, tracksById);
        AddOwnedItems(term, accumulator, ownedItems, releasesById, tracksById);

        SearchResultReadModel[] results =
        [
            .. accumulator.Results
                .OrderByDescending(result => result.Score)
                .ThenBy(result => SearchResultCodes.TypeRank(result.Type))
                .ThenBy(result => result.Title, StringComparer.OrdinalIgnoreCase)
                .ThenBy(result => result.Id)
                .Select(result => result.ToReadModel())
        ];
        SearchResultReadModel[] page = [.. results.Skip(query.Offset).Take(query.Limit)];

        return new CollectionSearchResult(page, query.Limit, query.Offset, results.Length);
    }

    private static void AddArtists(string term, SearchResultAccumulator accumulator, IReadOnlyList<Artist> artists)
    {
        foreach (Artist artist in artists.Where(artist => ContainsTerm(artist.Name, term)))
        {
            accumulator.Add(artist.Id.Value, "artist", artist.Name, SearchResultCodes.ArtistType(artist), "name", 100);
        }
    }

    private static void AddLabels(string term, SearchResultAccumulator accumulator, IReadOnlyList<Label> labels)
    {
        foreach (Label label in labels.Where(label => ContainsTerm(label.Name, term)))
        {
            accumulator.Add(label.Id.Value, "label", label.Name, null, "name", 90);
        }
    }

    private static void AddReleases(
        string term,
        SearchResultAccumulator accumulator,
        IReadOnlyList<Release> releases,
        Dictionary<LabelId, string> labelNames)
    {
        foreach (Release release in releases)
        {
            SearchTarget target = new(release.Id.Value, ReleaseResultType, release.Summary.Title, ReleaseSubtitle(release, labelNames));
            AddIfContains(accumulator, term, target, "title", release.Summary.Title, 100);

            if (TryGetReleaseLabelName(release, labelNames, out string? labelName) && labelName is not null)
            {
                AddIfContains(accumulator, term, target, "label", labelName, 80);
            }

            foreach (Genre genre in release.Cataloging.Genres)
            {
                AddIfContains(accumulator, term, target, "genre", genre.Name, 60);
            }

            foreach (Tag tag in release.Cataloging.Tags)
            {
                AddIfContains(accumulator, term, target, "tag", tag.Name, 70);
            }
        }
    }

    private static void AddTracks(string term, SearchResultAccumulator accumulator, IReadOnlyList<Track> tracks)
    {
        foreach (Track track in tracks)
        {
            SearchTarget target = new(track.Id.Value, TrackResultType, track.Title, null);
            AddIfContains(accumulator, term, target, "title", track.Title, 100);

            foreach (Genre genre in track.Cataloging.Genres)
            {
                AddIfContains(accumulator, term, target, "genre", genre.Name, 60);
            }

            foreach (Tag tag in track.Cataloging.Tags)
            {
                AddIfContains(accumulator, term, target, "tag", tag.Name, 70);
            }
        }
    }

    private static void AddCredits(
        string term,
        SearchResultAccumulator accumulator,
        IReadOnlyList<Credit> credits,
        Dictionary<ReleaseId, Release> releases,
        Dictionary<TrackId, Track> tracks)
    {
        foreach (Credit credit in credits)
        {
            CreditTarget target = credit.Target;
            string role = SearchResultCodes.ToCreditRoleCode(credit.Role);
            bool roleMatches = ContainsTerm(role, term);
            bool contributorMatches = ContainsTerm(credit.Contributor.Name, term);
            if (!roleMatches && !contributorMatches)
            {
                continue;
            }

            (Guid id, string type, string title) = target switch
            {
                ReleaseCreditTarget releaseTarget when releases.TryGetValue(releaseTarget.ReleaseId, out Release? release) => (release.Id.Value, ReleaseResultType, release.Summary.Title),
                TrackCreditTarget trackTarget when tracks.TryGetValue(trackTarget.TrackId, out Track? track) => (track.Id.Value, TrackResultType, track.Title),
                _ => (Guid.Empty, string.Empty, string.Empty)
            };
            if (id == Guid.Empty)
            {
                continue;
            }

            string subtitle = $"{role}: {credit.Contributor.Name}";
            if (roleMatches)
            {
                accumulator.Add(id, type, title, subtitle, "credit.role", 75);
            }

            if (contributorMatches)
            {
                accumulator.Add(id, type, title, subtitle, "credit.contributor", 65);
            }
        }
    }

    private static void AddOwnedItems(
        string term,
        SearchResultAccumulator accumulator,
        IReadOnlyList<OwnedItem> ownedItems,
        Dictionary<ReleaseId, Release> releases,
        Dictionary<TrackId, Track> tracks)
    {
        foreach (OwnedItem item in ownedItems)
        {
            string status = SearchResultCodes.ToOwnershipStatusCode(item.Holding.Status);
            string medium = SearchResultCodes.ToMediumCode(item.Holding.Medium);
            bool statusMatches = ContainsTerm(status, term);
            bool mediumMatches = ContainsTerm(medium, term);
            if (!statusMatches && !mediumMatches)
            {
                continue;
            }

            string title = OwnedItemTitle(item.Target, releases, tracks);
            string subtitle = $"{status} on {medium}";
            if (statusMatches)
            {
                accumulator.Add(item.Id.Value, "ownedItem", title, subtitle, "ownershipStatus", 55);
            }

            if (mediumMatches)
            {
                accumulator.Add(item.Id.Value, "ownedItem", title, subtitle, "medium", 55);
            }
        }
    }

    private static void AddIfContains(
        SearchResultAccumulator accumulator,
        string term,
        SearchTarget target,
        string matchedField,
        string value,
        int score)
    {
        if (ContainsTerm(value, term))
        {
            accumulator.Add(target.Id, target.Type, target.Title, target.Subtitle, matchedField, score);
        }
    }

    private static bool ContainsTerm(string value, string term)
    {
        return value.Contains(term, StringComparison.OrdinalIgnoreCase);
    }

    private static string ReleaseSubtitle(Release release, Dictionary<LabelId, string> labelNames)
    {
        return TryGetReleaseLabelName(release, labelNames, out string? labelName) && labelName is not null ? labelName : "release";
    }

    private static bool TryGetReleaseLabelName(Release release, Dictionary<LabelId, string> labelNames, out string? labelName)
    {
        labelName = null;
        if (!release.Summary.Metadata.LabelId.HasValue)
        {
            return false;
        }

        LabelId labelId = release.Summary.Metadata.LabelId.Match(value => value, () => default);
        return labelNames.TryGetValue(labelId, out labelName);
    }

    private static string OwnedItemTitle(OwnedItemTarget target, Dictionary<ReleaseId, Release> releases, Dictionary<TrackId, Track> tracks)
    {
        return target switch
        {
            ReleaseOwnedItemTarget releaseTarget when releases.TryGetValue(releaseTarget.ReleaseId, out Release? release) => release.Summary.Title,
            TrackOwnedItemTarget trackTarget when tracks.TryGetValue(trackTarget.TrackId, out Track? track) => track.Title,
            _ => "Owned item"
        };
    }

    private readonly record struct SearchTarget(Guid Id, string Type, string Title, string? Subtitle);
}
