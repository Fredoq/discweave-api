namespace Cratebase.Api.Features.Releases;

public sealed record ReleaseResponse(
    Guid Id,
    string Title,
    string Type,
    Guid? LabelId,
    int? Year,
    IReadOnlyList<string> Genres,
    IReadOnlyList<string> Tags);
