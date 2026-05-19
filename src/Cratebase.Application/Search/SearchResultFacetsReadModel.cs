namespace Cratebase.Application.Search;

public sealed record SearchResultFacetsReadModel(
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Media,
    IReadOnlyList<string> Statuses,
    IReadOnlyList<string> Tags,
    Guid? LabelId,
    IReadOnlyList<string> CollectorSignals);
