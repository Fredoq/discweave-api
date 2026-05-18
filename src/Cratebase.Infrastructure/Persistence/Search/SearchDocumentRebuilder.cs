using Cratebase.Domain.SharedKernel.Ids;
using Microsoft.EntityFrameworkCore;

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
        IReadOnlyList<SearchDocument> documents = await SearchDocumentBuilder.BuildAsync(context, collectionId, cancellationToken);

        _ = await context.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM search_documents WHERE collection_id = {collectionId.Value}",
            cancellationToken);

        foreach (SearchDocument document in documents)
        {
            _ = await context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO search_documents (
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
                    collector_signal_facet)
                VALUES (
                    {document.CollectionId.Value},
                    {document.EntityType},
                    {document.EntityId},
                    {document.Title},
                    {document.Subtitle},
                    {document.Summary},
                    {document.SearchText},
                    {document.MatchedFields},
                    {document.Snippets},
                    {document.RoleFacet},
                    {document.MediaFacet},
                    {document.StatusFacet},
                    {document.TagFacet},
                    {document.LabelId},
                    {document.CollectorSignalFacet})
                """,
                cancellationToken);
        }
    }
}
