namespace Cratebase.Api.Features.Settings;

public sealed record ImportPatternRequest(
    string Kind,
    string Template,
    int? SortOrder,
    bool? IsActive);

public sealed record ImportPatternTestRequest(string Kind, string Template, string Input);

public sealed record ImportPatternResponse(
    Guid Id,
    string Kind,
    string Template,
    int SortOrder,
    bool IsActive,
    bool IsBuiltin);

public sealed record ImportPatternTestResponse(
    bool Matched,
    IReadOnlyDictionary<string, string?> Fields,
    IReadOnlyList<string> Issues);
