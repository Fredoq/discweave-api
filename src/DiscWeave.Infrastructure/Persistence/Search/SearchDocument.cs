using DiscWeave.Domain.SharedKernel.Ids;

namespace DiscWeave.Infrastructure.Persistence.Search;

internal sealed class SearchDocument
{
    public long Id { get; set; }

    public CollectionId CollectionId { get; set; }

    public string EntityType { get; set; } = string.Empty;

    public Guid EntityId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Subtitle { get; set; }

    public string? Summary { get; set; }

    public string SearchText { get; set; } = string.Empty;

    public string MatchedFields { get; set; } = string.Empty;

    public string Snippets { get; set; } = string.Empty;

    public string RoleFacet { get; set; } = string.Empty;

    public string MediaFacet { get; set; } = string.Empty;

    public string StatusFacet { get; set; } = string.Empty;

    public string TagFacet { get; set; } = string.Empty;

    public Guid? LabelId { get; set; }

    public string LabelIdFacet { get; set; } = string.Empty;

    public string CollectorSignalFacet { get; set; } = string.Empty;
}
