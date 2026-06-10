using DiscWeave.Application.Security;
using DiscWeave.Infrastructure.Persistence;
using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace DiscWeave.Api.Features.Exports;

public static partial class ExportsEndpointRouteBuilderExtensions
{
    private static readonly char[] CsvSpecialChars = [',', '"', '\n', '\r'];

    private static async Task<IResult> ExportCsvAsync(
        DiscWeaveDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        ExportSnapshotResponse snapshot = await BuildSnapshotAsync(
            context,
            currentCollection.CollectionId,
            cancellationToken);

        byte[] archive = WriteCsvArchive(snapshot);
        return Results.File(archive, "application/zip", "discweave-export-csv.zip");
    }

    private static byte[] WriteCsvArchive(ExportSnapshotResponse snapshot)
    {
        using var archiveStream = new MemoryStream();
        using (var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddCsvEntry(archive, "artists.csv", ["id", "type", "name"], snapshot.Artists.Select(artist => new[] { artist.Id.ToString(), artist.Type, artist.Name }));
            AddCsvEntry(archive, "labels.csv", ["id", "name"], snapshot.Labels.Select(label => new[] { label.Id.ToString(), label.Name }));
            AddCsvEntry(archive, "releases.csv", ReleaseHeader(), snapshot.Releases.Select(release => new[]
            {
                release.Id.ToString(),
                release.Title,
                release.Type,
                release.LabelId?.ToString() ?? string.Empty,
                Invariant(release.Year),
                release.ReleaseDate ?? string.Empty,
                release.IsVariousArtists.ToString(),
                release.NotOnLabel.ToString(),
                JoinValues(release.Genres),
                JoinValues(release.Tags),
                release.CoverImage?.Url ?? string.Empty,
                release.CoverImage?.ContentType ?? string.Empty,
                release.CoverImage?.OriginalFileName ?? string.Empty,
                Invariant(release.CoverImage?.SizeBytes),
                release.CoverImage?.SourceType ?? string.Empty
            }));
            AddCsvEntry(archive, "release_labels.csv", ReleaseLabelHeader(), ReleaseLabelRows(snapshot));
            AddCsvEntry(archive, "release_tracklist.csv", ReleaseTracklistHeader(), ReleaseTracklistRows(snapshot));
            AddCsvEntry(archive, "tracks.csv", TrackHeader(), snapshot.Tracks.Select(track => new[]
            {
                track.Id.ToString(),
                track.Title,
                Invariant(track.DurationSeconds),
                JoinValues(track.Genres),
                JoinValues(track.Tags)
            }));
            AddCsvEntry(archive, "owned_items.csv", OwnedItemHeader(), snapshot.OwnedItems.Select(item => new[]
            {
                item.Id.ToString(),
                item.TargetType,
                item.TargetId.ToString(),
                item.Status,
                item.Medium.Type,
                item.Medium.Description,
                item.Medium.Path ?? string.Empty,
                item.Medium.Format ?? string.Empty,
                Invariant(item.Medium.DiscCount),
                item.Condition ?? string.Empty,
                item.StorageLocation ?? string.Empty
            }));
            AddCsvEntry(archive, "playlists.csv", PlaylistHeader(), snapshot.Playlists.Select(playlist => new[]
            {
                playlist.Id.ToString(),
                playlist.Name,
                playlist.Type,
                playlist.Description ?? string.Empty,
                JoinValues(playlist.Rules.Tags),
                JoinValues(playlist.Rules.Genres),
                JoinValues(playlist.Rules.Media),
                JoinValues(playlist.Rules.OwnershipStatuses),
                Invariant(playlist.Rules.YearFrom),
                Invariant(playlist.Rules.YearTo)
            }));
            AddCsvEntry(archive, "playlist_entries.csv", PlaylistEntryHeader(), PlaylistEntryRows(snapshot));
            AddCsvEntry(archive, "credits.csv", CreditHeader(), snapshot.Credits.Select(credit => new[]
            {
                credit.Id.ToString(),
                credit.ContributorArtistId.ToString(),
                credit.ContributorName,
                credit.TargetType,
                credit.TargetId.ToString(),
                credit.Role
            }));
            AddCsvEntry(archive, "artist_relations.csv", ArtistRelationHeader(), snapshot.ArtistRelations.Select(relation => new[]
            {
                relation.Id.ToString(),
                relation.SourceArtistId.ToString(),
                relation.TargetArtistId.ToString(),
                relation.Type,
                Invariant(relation.StartYear),
                Invariant(relation.EndYear)
            }));
            AddCsvEntry(archive, "track_relations.csv", TrackRelationHeader(), snapshot.TrackRelations.Select(relation => new[]
            {
                relation.Id.ToString(),
                relation.SourceTrackId.ToString(),
                relation.TargetTrackId.ToString(),
                relation.Type
            }));
            AddCsvEntry(archive, "dictionaries.csv", DictionaryHeader(), snapshot.Dictionaries.Select(entry => new[]
            {
                entry.Id.ToString(),
                entry.Kind,
                entry.Code,
                entry.Name,
                Invariant(entry.SortOrder),
                entry.IsActive.ToString(),
                entry.IsBuiltin.ToString(),
                entry.IsProtected.ToString(),
                entry.MediaProfile ?? string.Empty
            }));
            AddCsvEntry(archive, "import_patterns.csv", ImportPatternHeader(), snapshot.ImportPatterns.Select(pattern => new[]
            {
                pattern.Id.ToString(),
                pattern.Kind,
                pattern.Template,
                Invariant(pattern.SortOrder),
                pattern.IsActive.ToString(),
                pattern.IsBuiltin.ToString()
            }));
            AddCsvEntry(archive, "rating_criteria.csv", RatingCriterionHeader(), snapshot.RatingCriteria.Select(criterion => new[]
            {
                criterion.Id.ToString(),
                criterion.Code,
                criterion.Name,
                JoinValues(criterion.TargetTypes),
                Invariant(criterion.SortOrder),
                criterion.IsActive.ToString(),
                criterion.IsBuiltin.ToString(),
                criterion.IsProtected.ToString()
            }));
            AddCsvEntry(archive, "ratings.csv", RatingHeader(), snapshot.Ratings.Select(rating => new[]
            {
                rating.Id.ToString(),
                rating.CriterionId.ToString(),
                rating.TargetType,
                rating.TargetId.ToString(),
                Invariant(rating.Value)
            }));
        }

        return archiveStream.ToArray();
    }

    private static IEnumerable<string[]> ReleaseLabelRows(ExportSnapshotResponse snapshot)
    {
        return snapshot.Releases.SelectMany(release => release.Labels.Select(label => new[]
        {
            release.Id.ToString(),
            label.LabelId?.ToString() ?? string.Empty,
            label.Name,
            label.CatalogNumber ?? string.Empty,
            label.HasNoCatalogNumber.ToString()
        }));
    }

    private static IEnumerable<string[]> ReleaseTracklistRows(ExportSnapshotResponse snapshot)
    {
        return snapshot.Releases.SelectMany(release => release.Tracklist.Select(track => new[]
        {
            release.Id.ToString(),
            track.TrackId.ToString(),
            Invariant(track.Position),
            track.Title,
            Invariant(track.DurationSeconds),
            track.VersionNote ?? string.Empty,
            track.Disc ?? string.Empty,
            track.Side ?? string.Empty
        }));
    }

    private static void AddCsvEntry(ZipArchive archive, string name, IReadOnlyList<string> header, IEnumerable<string[]> rows)
    {
        ZipArchiveEntry entry = archive.CreateEntry(name);
        using Stream stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        writer.WriteLine(WriteCsvRow(header));
        foreach (string[] row in rows)
        {
            writer.WriteLine(WriteCsvRow(row));
        }
    }

    private static string WriteCsvRow(IEnumerable<string> values)
    {
        return string.Join(",", values.Select(EscapeCsvField));
    }

    private static string EscapeCsvField(string? value)
    {
        string field = value ?? string.Empty;
        if (field.Length > 0 && field[0] is '=' or '+' or '-' or '@' or '\t')
        {
            field = $"'{field}";
        }

        return field.IndexOfAny(CsvSpecialChars) >= 0
            ? $"\"{field.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
            : field;
    }

    private static string Invariant(int value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    private static string Invariant(int? value)
    {
        return value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static string Invariant(long? value)
    {
        return value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static string JoinValues(IEnumerable<string> values)
    {
        return string.Join("|", values);
    }
}
