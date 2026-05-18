using Cratebase.Application.Security;
using Cratebase.Infrastructure.Persistence;
using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace Cratebase.Api.Features.Exports;

public static partial class ExportsEndpointRouteBuilderExtensions
{
    private static readonly char[] CsvSpecialChars = [',', '"', '\n', '\r'];

    private static async Task<IResult> ExportCsvAsync(
        CratebaseDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        ExportSnapshotResponse snapshot = await BuildSnapshotAsync(
            context,
            currentCollection.CollectionId,
            cancellationToken);

        byte[] archive = WriteCsvArchive(snapshot);
        return Results.File(archive, "application/zip", "cratebase-export-csv.zip");
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
                JoinValues(release.Tags)
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
            track.VersionNote ?? string.Empty
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

    private static string JoinValues(IEnumerable<string> values)
    {
        return string.Join("|", values);
    }

    private static string[] ReleaseHeader()
    {
        return ["id", "title", "type", "label_id", "year", "release_date", "is_various_artists", "not_on_label", "genres", "tags"];
    }

    private static string[] ReleaseLabelHeader()
    {
        return ["release_id", "label_id", "name", "catalog_number", "has_no_catalog_number"];
    }

    private static string[] ReleaseTracklistHeader()
    {
        return ["release_id", "track_id", "position", "title", "duration_seconds", "version_note"];
    }

    private static string[] TrackHeader()
    {
        return ["id", "title", "duration_seconds", "genres", "tags"];
    }

    private static string[] OwnedItemHeader()
    {
        return ["id", "target_type", "target_id", "status", "medium_type", "medium_description", "medium_path", "medium_format", "medium_disc_count", "condition", "storage_location"];
    }

    private static string[] CreditHeader()
    {
        return ["id", "contributor_artist_id", "contributor_name", "target_type", "target_id", "role"];
    }

    private static string[] ArtistRelationHeader()
    {
        return ["id", "source_artist_id", "target_artist_id", "type", "start_year", "end_year"];
    }

    private static string[] TrackRelationHeader()
    {
        return ["id", "source_track_id", "target_track_id", "type"];
    }

    private static string[] DictionaryHeader()
    {
        return ["id", "kind", "code", "name", "sort_order", "is_active", "is_builtin", "is_protected", "media_profile"];
    }

    private static string[] ImportPatternHeader()
    {
        return ["id", "kind", "template", "sort_order", "is_active", "is_builtin"];
    }

    private static string[] RatingCriterionHeader()
    {
        return ["id", "code", "name", "target_types", "sort_order", "is_active", "is_builtin", "is_protected"];
    }

    private static string[] RatingHeader()
    {
        return ["id", "criterion_id", "target_type", "target_id", "value"];
    }
}
