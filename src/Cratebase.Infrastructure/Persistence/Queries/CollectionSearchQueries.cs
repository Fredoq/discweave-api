using System.Data;
using System.Data.Common;
using System.Globalization;
using Cratebase.Application.Search;
using Cratebase.Application.Security;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Infrastructure.Persistence.Search;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Cratebase.Infrastructure.Persistence.Queries;

public sealed class CollectionSearchQueries : ICollectionSearchQueries
{
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

        var parameters = SearchSqlParameters.From(_collectionId, query);
        await using DbCommand countCommand = await CreateCommandAsync(CountSql, parameters, cancellationToken);
        int total = Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);

        await using DbCommand searchCommand = await CreateCommandAsync(SearchSql, parameters, cancellationToken);
        await using DbDataReader reader = await searchCommand.ExecuteReaderAsync(cancellationToken);
        List<SearchResultReadModel> results = [];
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(ReadResult(reader));
        }

        return new CollectionSearchResult(results, query.Limit, query.Offset, total);
    }

    private async Task<DbCommand> CreateCommandAsync(
        string sql,
        SearchSqlParameters parameters,
        CancellationToken cancellationToken)
    {
        DbConnection connection = _context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        DbCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        parameters.AddTo(command);

        return command;
    }

    private static SearchResultReadModel ReadResult(DbDataReader reader)
    {
        var facets = new SearchResultFacetsReadModel(
            [.. SearchDocumentText.UnpackFacet(reader.GetString(reader.GetOrdinal("role_facet"))).Select(DisplayRole)],
            SearchDocumentText.UnpackFacet(reader.GetString(reader.GetOrdinal("media_facet"))),
            [.. SearchDocumentText.UnpackFacet(reader.GetString(reader.GetOrdinal("status_facet"))).Select(DisplayStatus)],
            SearchDocumentText.UnpackFacet(reader.GetString(reader.GetOrdinal("tag_facet"))),
            reader.IsDBNull(reader.GetOrdinal("label_id")) ? null : reader.GetGuid(reader.GetOrdinal("label_id")),
            [.. SearchDocumentText.UnpackFacet(reader.GetString(reader.GetOrdinal("collector_signal_facet"))).Select(DisplaySignal)]);

        return new SearchResultReadModel(
            reader.GetGuid(reader.GetOrdinal("entity_id")),
            reader.GetString(reader.GetOrdinal("entity_type")),
            reader.GetString(reader.GetOrdinal("title")),
            reader.IsDBNull(reader.GetOrdinal("subtitle")) ? null : reader.GetString(reader.GetOrdinal("subtitle")),
            reader.IsDBNull(reader.GetOrdinal("summary")) ? null : reader.GetString(reader.GetOrdinal("summary")),
            SearchDocumentText.Unpack(reader.GetString(reader.GetOrdinal("matched_fields"))),
            SearchDocumentText.Unpack(reader.GetString(reader.GetOrdinal("snippets"))),
            facets,
            Convert.ToDecimal(reader.GetDouble(reader.GetOrdinal("rank")), CultureInfo.InvariantCulture));
    }

    private static string DisplayRole(string role)
    {
        return role switch
        {
            "mainartist" => "mainArtist",
            "featuredartist" => "featuredArtist",
            _ => role
        };
    }

    private static string DisplayStatus(string status)
    {
        return status == "needsdigitization" ? "needsDigitization" : status;
    }

    private static string DisplaySignal(string signal)
    {
        return signal switch
        {
            "physicalwithoutdigital" => "physicalWithoutDigital",
            "lossywithoutlossless" => "lossyWithoutLossless",
            "wantednotowned" => "wantedNotOwned",
            "needsdigitization" => "needsDigitization",
            _ => signal
        };
    }

    private const string SearchSql =
        """
        WITH search_input AS (
            SELECT CASE WHEN @has_query THEN websearch_to_tsquery('simple', @query) ELSE NULL::tsquery END AS query
        )
        SELECT
            document.entity_id,
            document.entity_type,
            document.title,
            document.subtitle,
            document.summary,
            document.matched_fields,
            document.snippets,
            document.role_facet,
            document.media_facet,
            document.status_facet,
            document.tag_facet,
            document.label_id,
            document.label_id_facet,
            document.collector_signal_facet,
            CASE WHEN @has_query THEN
                (ts_rank(document.search_vector, search_input.query) * 10.0) +
                CASE WHEN document.search_vector @@ search_input.query THEN 0.0 ELSE similarity(document.search_text, @query) END +
                CASE WHEN lower(document.title) = lower(@query) THEN 5.0 ELSE 0.0 END
            ELSE 1.0 END AS rank
        FROM search_documents document
        CROSS JOIN search_input
        WHERE document.collection_id = @collection_id
            AND (@entity_type = '' OR document.entity_type = @entity_type)
            AND (@role_pattern = '' OR document.role_facet LIKE @role_pattern)
            AND (@media_pattern = '' OR document.media_facet LIKE @media_pattern)
            AND (@status_pattern = '' OR document.status_facet LIKE @status_pattern)
            AND (@tag_pattern = '' OR document.tag_facet LIKE @tag_pattern)
            AND (@label_id IS NULL OR document.label_id = @label_id OR document.label_id_facet LIKE @label_id_pattern)
            AND (
                @saved_view = '' OR
                @saved_view = 'all' OR
                (@saved_view = 'credits' AND document.role_facet <> '') OR
                (@saved_view = 'remixes' AND document.entity_type = 'track' AND document.role_facet LIKE '%|remixer|%') OR
                (@saved_view = 'productions' AND document.entity_type = 'release' AND document.role_facet LIKE '%|producer|%') OR
                (@saved_view = 'labels' AND document.entity_type = 'label') OR
                (@saved_view = 'needsdigitization' AND document.status_facet LIKE '%|needsdigitization|%') OR
                (@saved_view = 'physicalwithoutdigital' AND document.collector_signal_facet LIKE '%|physicalwithoutdigital|%') OR
                (@saved_view = 'lossywithoutlossless' AND document.collector_signal_facet LIKE '%|lossywithoutlossless|%') OR
                (@saved_view = 'mp3notlossless' AND document.collector_signal_facet LIKE '%|lossywithoutlossless|%') OR
                (@saved_view = 'wantednotowned' AND document.collector_signal_facet LIKE '%|wantednotowned|%')
            )
            AND (
                @has_query = false OR
                document.search_vector @@ search_input.query OR
                document.search_text ILIKE @query_like ESCAPE '\' OR
                similarity(document.search_text, @query) >= 0.18
            )
        ORDER BY rank DESC, document.title ASC, document.entity_type ASC, document.entity_id ASC
        LIMIT @limit OFFSET @offset
        """;

    private const string CountSql =
        """
        WITH search_input AS (
            SELECT CASE WHEN @has_query THEN websearch_to_tsquery('simple', @query) ELSE NULL::tsquery END AS query
        )
        SELECT count(*)
        FROM search_documents document
        CROSS JOIN search_input
        WHERE document.collection_id = @collection_id
            AND (@entity_type = '' OR document.entity_type = @entity_type)
            AND (@role_pattern = '' OR document.role_facet LIKE @role_pattern)
            AND (@media_pattern = '' OR document.media_facet LIKE @media_pattern)
            AND (@status_pattern = '' OR document.status_facet LIKE @status_pattern)
            AND (@tag_pattern = '' OR document.tag_facet LIKE @tag_pattern)
            AND (@label_id IS NULL OR document.label_id = @label_id OR document.label_id_facet LIKE @label_id_pattern)
            AND (
                @saved_view = '' OR
                @saved_view = 'all' OR
                (@saved_view = 'credits' AND document.role_facet <> '') OR
                (@saved_view = 'remixes' AND document.entity_type = 'track' AND document.role_facet LIKE '%|remixer|%') OR
                (@saved_view = 'productions' AND document.entity_type = 'release' AND document.role_facet LIKE '%|producer|%') OR
                (@saved_view = 'labels' AND document.entity_type = 'label') OR
                (@saved_view = 'needsdigitization' AND document.status_facet LIKE '%|needsdigitization|%') OR
                (@saved_view = 'physicalwithoutdigital' AND document.collector_signal_facet LIKE '%|physicalwithoutdigital|%') OR
                (@saved_view = 'lossywithoutlossless' AND document.collector_signal_facet LIKE '%|lossywithoutlossless|%') OR
                (@saved_view = 'mp3notlossless' AND document.collector_signal_facet LIKE '%|lossywithoutlossless|%') OR
                (@saved_view = 'wantednotowned' AND document.collector_signal_facet LIKE '%|wantednotowned|%')
            )
            AND (
                @has_query = false OR
                document.search_vector @@ search_input.query OR
                document.search_text ILIKE @query_like ESCAPE '\' OR
                similarity(document.search_text, @query) >= 0.18
            )
        """;

    private sealed record SearchSqlParameters(
        Guid CollectionId,
        bool HasQuery,
        string Query,
        string QueryLike,
        string EntityType,
        string RolePattern,
        string MediaPattern,
        string StatusPattern,
        string TagPattern,
        Guid? LabelId,
        string LabelIdPattern,
        string SavedView,
        int Limit,
        int Offset)
    {
        public static SearchSqlParameters From(CollectionId collectionId, CollectionSearchQuery query)
        {
            string normalizedQuery = query.Query.Trim();

            return new SearchSqlParameters(
                collectionId.Value,
                normalizedQuery.Length > 0,
                normalizedQuery,
                $"%{EscapeLike(normalizedQuery)}%",
                query.EntityType?.Trim() ?? string.Empty,
                FacetPattern(query.Role),
                FacetPattern(query.Media),
                FacetPattern(query.Status),
                FacetPattern(query.Tag),
                query.LabelId,
                LabelFacetPattern(query.LabelId),
                SearchDocumentText.NormalizeFacet(query.SavedView ?? string.Empty),
                query.Limit,
                query.Offset);
        }

        public void AddTo(DbCommand command)
        {
            Add(command, "collection_id", CollectionId);
            Add(command, "has_query", HasQuery);
            Add(command, "query", Query);
            Add(command, "query_like", QueryLike);
            Add(command, "entity_type", EntityType);
            Add(command, "role_pattern", RolePattern);
            Add(command, "media_pattern", MediaPattern);
            Add(command, "status_pattern", StatusPattern);
            Add(command, "tag_pattern", TagPattern);
            Add(command, "label_id", LabelId, DbType.Guid);
            Add(command, "label_id_pattern", LabelIdPattern);
            Add(command, "saved_view", SavedView);
            Add(command, "limit", Limit);
            Add(command, "offset", Offset);
        }

        private static string FacetPattern(string? value)
        {
            string normalized = SearchDocumentText.NormalizeFacet(value ?? string.Empty);
            return normalized.Length == 0 ? string.Empty : $"%|{normalized}|%";
        }

        private static string LabelFacetPattern(Guid? labelId)
        {
            return labelId.HasValue
                ? $"%|{SearchDocumentText.NormalizeFacet(labelId.Value.ToString("D"))}|%"
                : string.Empty;
        }

        private static string EscapeLike(string value)
        {
            return value
                .Replace(@"\", @"\\", StringComparison.Ordinal)
                .Replace("%", @"\%", StringComparison.Ordinal)
                .Replace("_", @"\_", StringComparison.Ordinal);
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
}
