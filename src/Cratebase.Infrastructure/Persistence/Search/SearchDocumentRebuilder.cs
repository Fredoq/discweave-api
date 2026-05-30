using Cratebase.Domain.SharedKernel.Ids;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;

namespace Cratebase.Infrastructure.Persistence.Search;

internal static class SearchDocumentRebuilder
{
    public static async Task RebuildAsync(
        CratebaseDbContext context,
        IReadOnlyCollection<CollectionId> collectionIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (CollectionId collectionId in collectionIds.Distinct())
        {
            await RebuildAsync(context, collectionId, cancellationToken);
        }
    }

    public static async Task RebuildAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<SearchDocument> documents = Deduplicate(
            await SearchDocumentBuilder.BuildAsync(context, collectionId, cancellationToken));

        await using IDbContextTransaction? transaction = await BeginTransactionIfNeededAsync(context, cancellationToken);

        await LockCollectionSearchDocumentsAsync(context, collectionId, cancellationToken);

        _ = await context.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM search_documents WHERE collection_id = {collectionId.Value}",
            cancellationToken);

        if (documents.Count > 0)
        {
            await CopyDocumentsAsync(context, documents, cancellationToken);
        }

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }
    }

    private static async Task<IDbContextTransaction?> BeginTransactionIfNeededAsync(
        CratebaseDbContext context,
        CancellationToken cancellationToken)
    {
        return context.Database.CurrentTransaction is not null
            ? null
            : await context.Database.BeginTransactionAsync(cancellationToken);
    }

    private static async Task LockCollectionSearchDocumentsAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        CancellationToken cancellationToken)
    {
        byte[] bytes = collectionId.Value.ToByteArray();
        int lockKeyOne = BitConverter.ToInt32(bytes, 0);
        int lockKeyTwo = BitConverter.ToInt32(bytes, 4);

        _ = await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({lockKeyOne}, {lockKeyTwo})",
            cancellationToken);
    }

    private static IReadOnlyList<SearchDocument> Deduplicate(IReadOnlyList<SearchDocument> documents)
    {
        return
        [
            .. documents
                .GroupBy(
                    document => new
                    {
                        document.CollectionId,
                        document.EntityType,
                        document.EntityId
                    })
                .Select(group => group.First())
        ];
    }

    private static async Task CopyDocumentsAsync(
        CratebaseDbContext context,
        IReadOnlyList<SearchDocument> documents,
        CancellationToken cancellationToken)
    {
        if (context.Database.GetDbConnection() is not NpgsqlConnection connection)
        {
            throw new InvalidOperationException("Search document rebuild requires an Npgsql connection.");
        }

        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using NpgsqlBinaryImporter writer = await connection.BeginBinaryImportAsync(
            """
            COPY search_documents (
                collection_id,
                entity_type,
                entity_id,
                title,
                subtitle,
                summary,
                search_text,
                matched_fields,
                snippets,
                role_facet,
                media_facet,
                status_facet,
                tag_facet,
                label_id,
                label_id_facet,
                collector_signal_facet)
            FROM STDIN (FORMAT BINARY)
            """,
            cancellationToken);

        foreach (SearchDocument document in documents)
        {
            await writer.StartRowAsync(cancellationToken);
            await writer.WriteAsync(document.CollectionId.Value, NpgsqlDbType.Uuid, cancellationToken);
            await writer.WriteAsync(document.EntityType, NpgsqlDbType.Text, cancellationToken);
            await writer.WriteAsync(document.EntityId, NpgsqlDbType.Uuid, cancellationToken);
            await writer.WriteAsync(document.Title, NpgsqlDbType.Text, cancellationToken);
            await WriteNullableTextAsync(writer, document.Subtitle, cancellationToken);
            await WriteNullableTextAsync(writer, document.Summary, cancellationToken);
            await writer.WriteAsync(document.SearchText, NpgsqlDbType.Text, cancellationToken);
            await writer.WriteAsync(document.MatchedFields, NpgsqlDbType.Text, cancellationToken);
            await writer.WriteAsync(document.Snippets, NpgsqlDbType.Text, cancellationToken);
            await writer.WriteAsync(document.RoleFacet, NpgsqlDbType.Text, cancellationToken);
            await writer.WriteAsync(document.MediaFacet, NpgsqlDbType.Text, cancellationToken);
            await writer.WriteAsync(document.StatusFacet, NpgsqlDbType.Text, cancellationToken);
            await writer.WriteAsync(document.TagFacet, NpgsqlDbType.Text, cancellationToken);
            await WriteNullableGuidAsync(writer, document.LabelId, cancellationToken);
            await writer.WriteAsync(document.LabelIdFacet, NpgsqlDbType.Text, cancellationToken);
            await writer.WriteAsync(document.CollectorSignalFacet, NpgsqlDbType.Text, cancellationToken);
        }

        _ = await writer.CompleteAsync(cancellationToken);
    }

    private static async Task WriteNullableTextAsync(
        NpgsqlBinaryImporter writer,
        string? value,
        CancellationToken cancellationToken)
    {
        if (value is null)
        {
            await writer.WriteNullAsync(cancellationToken);
            return;
        }

        await writer.WriteAsync(value, NpgsqlDbType.Text, cancellationToken);
    }

    private static async Task WriteNullableGuidAsync(
        NpgsqlBinaryImporter writer,
        Guid? value,
        CancellationToken cancellationToken)
    {
        if (value is null)
        {
            await writer.WriteNullAsync(cancellationToken);
            return;
        }

        await writer.WriteAsync(value.Value, NpgsqlDbType.Uuid, cancellationToken);
    }
}
