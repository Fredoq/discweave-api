namespace Cratebase.Api.Features.Releases;

public sealed record ReleaseRequest(
    string Title,
    string? Type,
    Guid? LabelId,
    int? Year,
    IReadOnlyList<string>? Genres,
    IReadOnlyList<string>? Tags);
