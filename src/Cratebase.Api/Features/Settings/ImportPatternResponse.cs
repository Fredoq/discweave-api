namespace Cratebase.Api.Features.Settings;

public sealed record ImportPatternResponse(
    Guid Id,
    string Kind,
    string Template,
    int SortOrder,
    bool IsActive,
    bool IsBuiltin);
