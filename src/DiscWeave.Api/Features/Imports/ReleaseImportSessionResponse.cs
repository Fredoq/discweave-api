namespace DiscWeave.Api.Features.Imports;

public sealed record ReleaseImportSessionResponse(
    Guid Id,
    string SourceRoot,
    string Status,
    int DraftCount,
    int TrackCount,
    int IgnoredFileCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<ReleaseImportDraftResponse>? Drafts);
