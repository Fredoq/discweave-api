namespace Cratebase.Api.Features.Settings;

public sealed record ImportPatternRequest(
    string Kind,
    string Template,
    int? SortOrder,
    bool? IsActive);
