namespace DiscWeave.Api.Features.Search;

public sealed record SearchResultFacetsResponse(
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Media,
    IReadOnlyList<string> Statuses,
    IReadOnlyList<string> Tags,
    Guid? LabelId,
    IReadOnlyList<string> CollectorSignals);
