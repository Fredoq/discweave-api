using System.Data;
using System.Data.Common;
using System.Globalization;
using Cratebase.Domain.Playlists;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Domain.SharedKernel.Optional;
using Cratebase.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Cratebase.Api.Features.Playlists;

internal static partial class PlaylistMapper
{
    private static async Task<PlaylistItemResponse[]> QuerySmartReleaseResultsAsync(
        Playlist playlist,
        CratebaseDbContext context,
        int limit,
        CancellationToken cancellationToken)
    {
        await using DbCommand command = await CreateSmartPlaylistCommandAsync(
            context,
            BuildSmartReleaseResultsSql(playlist.Rules),
            playlist.CollectionId,
            playlist.Rules,
            limit,
            cancellationToken);
        await using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        var results = new List<PlaylistItemResponse>();
        int releaseIdOrdinal = reader.GetOrdinal("release_id");
        int titleOrdinal = reader.GetOrdinal("title");
        int releaseYearOrdinal = reader.GetOrdinal("release_year");
        while (await reader.ReadAsync(cancellationToken))
        {
            string? year = await reader.IsDBNullAsync(releaseYearOrdinal, cancellationToken)
                ? null
                : reader.GetInt32(releaseYearOrdinal).ToString(CultureInfo.InvariantCulture);
            results.Add(new PlaylistItemResponse(
                PlaylistEntry.ReleaseKind,
                reader.GetGuid(releaseIdOrdinal),
                reader.GetString(titleOrdinal),
                year));
        }

        return [.. results];
    }

    private static async Task<PlaylistItemResponse[]> QuerySmartTrackResultsAsync(
        Playlist playlist,
        CratebaseDbContext context,
        int limit,
        CancellationToken cancellationToken)
    {
        await using DbCommand command = await CreateSmartPlaylistCommandAsync(
            context,
            BuildSmartTrackResultsSql(playlist.Rules),
            playlist.CollectionId,
            playlist.Rules,
            limit,
            cancellationToken);
        await using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        var results = new List<PlaylistItemResponse>();
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new PlaylistItemResponse(
                PlaylistEntry.TrackKind,
                reader.GetGuid(reader.GetOrdinal("track_id")),
                reader.GetString(reader.GetOrdinal("title")),
                null));
        }

        return [.. results];
    }

    private static async Task<DbCommand> CreateSmartPlaylistCommandAsync(
        CratebaseDbContext context,
        string sql,
        CollectionId collectionId,
        SmartPlaylistRules rules,
        int limit,
        CancellationToken cancellationToken)
    {
        DbConnection connection = context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        DbCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = context.Database.CurrentTransaction?.GetDbTransaction();
        Add(command, "collection_id", collectionId.Value, DbType.Guid);
        AddRuleValueParameters(command, "tag", NormalizeRuleValues(rules.Tags));
        AddRuleValueParameters(command, "genre", NormalizeRuleValues(rules.Genres));
        AddRuleValueParameters(command, "medium", NormalizeRuleValues(rules.Media));
        AddRuleValueParameters(command, "status", StatusRuleValues(rules));
        Add(command, "year_from", OptionalIntOrDbNull(rules.YearFrom), DbType.Int32);
        Add(command, "year_to", OptionalIntOrDbNull(rules.YearTo), DbType.Int32);
        Add(command, "limit", limit);
        return command;
    }

    private static string[] NormalizeRuleValues(IEnumerable<string> values)
    {
        return
        [
            .. values
                .Select(value => value.Trim().ToLowerInvariant())
                .Where(value => value.Length > 0)
        ];
    }

    private static void AddRuleValueParameters(DbCommand command, string prefix, string[] values)
    {
        for (int index = 0; index < values.Length; index++)
        {
            Add(command, prefix + "_" + index.ToString(CultureInfo.InvariantCulture), values[index]);
        }
    }

    private static string[] StatusRuleValues(SmartPlaylistRules rules)
    {
        return
        [
            .. rules.OwnershipStatuses
                .Select(StatusFromCode)
                .Where(status => status.HasValue)
                .Select(status => status.GetValueOrDefault().ToString())
        ];
    }

    private static object OptionalIntOrDbNull(IOptionalValue<int> value)
    {
        return value is PresentOptionalValue<int> present ? present.Value : DBNull.Value;
    }

    private static void Add(DbCommand command, string name, object? value, DbType? dbType = null)
    {
        DbParameter parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        if (dbType.HasValue)
        {
            parameter.DbType = dbType.Value;
        }

        _ = command.Parameters.Add(parameter);
    }
}
