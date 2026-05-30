namespace DiscWeave.Api.Features.Settings;

public sealed record ImportPatternTestResponse(
    bool Matched,
    IReadOnlyDictionary<string, string?> Fields,
    IReadOnlyList<string> Issues);
