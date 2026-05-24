using System.Globalization;
using System.Text;
using Cratebase.Domain.Playlists;

namespace Cratebase.Api.Features.Playlists;

internal static partial class PlaylistMapper
{
    private const string GenreRuleAlias = "genre";
    private const string ReleaseAlias = "release";
    private const string ReleaseIdColumn = "release_id";
    private const string TrackAlias = "track";
    private const string TrackIdColumn = "track_id";

    private static string BuildSmartReleaseResultsSql(SmartPlaylistRules rules)
    {
        var sql = new StringBuilder(
            """
            SELECT release.release_id, release.title, release.release_year
            FROM releases release
            WHERE release.collection_id = @collection_id
            """);
        _ = sql.AppendLine();
        AppendReleaseRuleFilters(sql, rules);
        _ = sql.AppendLine("ORDER BY release.title, release.release_id");
        _ = sql.AppendLine("LIMIT @limit");
        return sql.ToString();
    }

    private static string BuildSmartTrackResultsSql(SmartPlaylistRules rules)
    {
        var sql = new StringBuilder(
            """
            SELECT track.track_id, track.title
            FROM tracks track
            WHERE track.collection_id = @collection_id
            """);
        _ = sql.AppendLine();
        AppendTrackRuleFilters(sql, rules);
        _ = sql.AppendLine("ORDER BY track.title, track.track_id");
        _ = sql.AppendLine("LIMIT @limit");
        return sql.ToString();
    }

    private static void AppendReleaseRuleFilters(StringBuilder sql, SmartPlaylistRules rules)
    {
        AppendCatalogingRuleFilter(sql, rules.Tags, "release_tags", ReleaseAlias, ReleaseIdColumn, "tag", "tag");
        AppendCatalogingRuleFilter(sql, rules.Genres, "release_genres", ReleaseAlias, ReleaseIdColumn, GenreRuleAlias, GenreRuleAlias);
        AppendOwnedItemRuleFilters(sql, rules, ReleaseAlias, "target_release_id", ReleaseIdColumn);
        AppendReleaseYearFilters(sql, rules, ReleaseAlias);
    }

    private static void AppendTrackRuleFilters(StringBuilder sql, SmartPlaylistRules rules)
    {
        AppendCatalogingRuleFilter(sql, rules.Tags, "track_tags", TrackAlias, TrackIdColumn, "tag", "tag");
        AppendCatalogingRuleFilter(sql, rules.Genres, "track_genres", TrackAlias, TrackIdColumn, GenreRuleAlias, GenreRuleAlias);
        AppendOwnedItemRuleFilters(sql, rules, TrackAlias, "target_track_id", TrackIdColumn);
        AppendTrackYearFilter(sql, rules);
    }

    private static void AppendCatalogingRuleFilter(
        StringBuilder sql,
        IReadOnlyList<string> ruleValues,
        string tableName,
        string entityAlias,
        string entityIdColumn,
        string rowAlias,
        string parameterPrefix)
    {
        string[] values = NormalizeRuleValues(ruleValues);
        if (ruleValues.Count == 0 || AppendNoMatchesWhenEmpty(sql, values))
        {
            return;
        }

        _ = sql.AppendLine(
            "AND EXISTS (SELECT 1 FROM " +
            tableName +
            " " +
            rowAlias +
            " WHERE " +
            rowAlias +
            ".collection_id = " +
            entityAlias +
            ".collection_id AND " +
            rowAlias +
            "." +
            entityIdColumn +
            " = " +
            entityAlias +
            "." +
            entityIdColumn +
            " AND lower(" +
            rowAlias +
            ".name) IN (" +
            ParameterList(parameterPrefix, values.Length) +
            "))");
    }

    private static void AppendOwnedItemRuleFilters(
        StringBuilder sql,
        SmartPlaylistRules rules,
        string entityAlias,
        string targetColumn,
        string entityIdColumn)
    {
        string[] media = NormalizeRuleValues(rules.Media);
        if (rules.Media.Count > 0 && !AppendNoMatchesWhenEmpty(sql, media))
        {
            _ = sql.AppendLine(
                "AND EXISTS (SELECT 1 FROM owned_items item WHERE item.collection_id = " +
                entityAlias +
                ".collection_id AND item." +
                targetColumn +
                " = " +
                entityAlias +
                "." +
                entityIdColumn +
                " AND lower(item.medium_type) IN (" +
                ParameterList("medium", media.Length) +
                "))");
        }

        string[] statuses = StatusRuleValues(rules);
        if (rules.OwnershipStatuses.Count > 0 && !AppendNoMatchesWhenEmpty(sql, statuses))
        {
            _ = sql.AppendLine(
                "AND EXISTS (SELECT 1 FROM owned_items item WHERE item.collection_id = " +
                entityAlias +
                ".collection_id AND item." +
                targetColumn +
                " = " +
                entityAlias +
                "." +
                entityIdColumn +
                " AND item.ownership_status IN (" +
                ParameterList("status", statuses.Length) +
                "))");
        }
    }

    private static void AppendReleaseYearFilters(StringBuilder sql, SmartPlaylistRules rules, string releaseAlias)
    {
        if (rules.YearFrom.HasValue)
        {
            _ = sql.AppendLine("AND " + releaseAlias + ".release_year >= @year_from");
        }

        if (rules.YearTo.HasValue)
        {
            _ = sql.AppendLine("AND " + releaseAlias + ".release_year <= @year_to");
        }
    }

    private static void AppendTrackYearFilter(StringBuilder sql, SmartPlaylistRules rules)
    {
        if (!rules.YearFrom.HasValue && !rules.YearTo.HasValue)
        {
            return;
        }

        _ = sql.AppendLine(
            """
            AND EXISTS (
                SELECT 1
                FROM release_tracks release_track
                INNER JOIN releases release
                    ON release.collection_id = release_track.collection_id
                    AND release.release_id = release_track.release_id
                WHERE release_track.collection_id = track.collection_id
                    AND release_track.track_id = track.track_id
            """);
        AppendReleaseYearFilters(sql, rules, "release");
        _ = sql.AppendLine(")");
    }

    private static string ParameterList(string prefix, int count)
    {
        return string.Join(
            ", ",
            Enumerable.Range(0, count).Select(index => "@" + prefix + "_" + index.ToString(CultureInfo.InvariantCulture)));
    }

    private static bool AppendNoMatchesWhenEmpty(StringBuilder sql, string[] values)
    {
        if (values.Length > 0)
        {
            return false;
        }

        _ = sql.AppendLine("AND false");
        return true;
    }
}
