namespace Cratebase.Api.Features.Imports;

public sealed record LocalAgentImportTokenResponse(
    string Token,
    DateTimeOffset ExpiresAt,
    string AgentBaseUrl,
    int ProtocolVersion,
    string MacOsDownloadUrl,
    IReadOnlyList<string> ReleaseFolderPatterns,
    IReadOnlyList<string> TrackFilePatterns);

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

public sealed record ReleaseImportDraftResponse(
    Guid Id,
    string SourcePath,
    string RelativePath,
    string Status,
    string Title,
    string Type,
    string? CatalogNumber,
    string? LabelName,
    string? ReleaseDate,
    int? Year,
    bool IsVariousArtists,
    bool NotOnLabel,
    IReadOnlyList<string> ArtistNames,
    IReadOnlyList<ReleaseImportArtistCreditResponse> ArtistCredits,
    IReadOnlyList<Guid> SelectedArtistIds,
    IReadOnlyList<EntitySuggestionResponse> ArtistSuggestions,
    IReadOnlyList<ReleaseImportLabelResponse> Labels,
    IReadOnlyList<string> Genres,
    IReadOnlyList<string> Tags,
    string? CoverPath,
    IReadOnlyList<ImportIssueResponse> Issues,
    IReadOnlyList<ReleaseImportDraftTrackResponse> Tracks);

public sealed record ReleaseImportDraftTrackResponse(
    Guid Id,
    string FilePath,
    string RelativePath,
    string Format,
    long SizeBytes,
    DateTimeOffset LastModifiedAt,
    int? DurationSeconds,
    int? Position,
    string Title,
    IReadOnlyList<string> ArtistNames,
    IReadOnlyList<ReleaseImportArtistCreditResponse> ArtistCredits,
    IReadOnlyList<EntitySuggestionResponse> ArtistSuggestions,
    IReadOnlyList<EntitySuggestionResponse> TrackSuggestions,
    bool IsSkipped,
    Guid? SelectedTrackId,
    IReadOnlyList<Guid> SelectedArtistIds,
    IReadOnlyList<ImportIssueResponse> Issues);

public sealed record EntitySuggestionResponse(Guid Id, string Name, string Match);

public sealed record ReleaseImportArtistCreditResponse(Guid? ArtistId, string Name, string Role);

public sealed record ReleaseImportLabelResponse(Guid? LabelId, string Name, string? CatalogNumber, bool HasNoCatalogNumber);

public sealed record ImportIssueResponse(string Code, string Message, string Severity);
