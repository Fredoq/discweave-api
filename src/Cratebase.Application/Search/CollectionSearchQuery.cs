namespace Cratebase.Application.Search;

public sealed record CollectionSearchQuery(
    string Query,
    string? EntityType,
    string? Role,
    string? Media,
    string? Status,
    Guid? LabelId,
    string? Tag,
    string? SavedView,
    int Limit,
    int Offset)
{
    public bool HasQuery => !string.IsNullOrWhiteSpace(Query);

    public bool HasCriteria =>
        HasQuery ||
        !string.IsNullOrWhiteSpace(EntityType) ||
        !string.IsNullOrWhiteSpace(Role) ||
        !string.IsNullOrWhiteSpace(Media) ||
        !string.IsNullOrWhiteSpace(Status) ||
        LabelId.HasValue ||
        !string.IsNullOrWhiteSpace(Tag) ||
        !string.IsNullOrWhiteSpace(SavedView);
}
