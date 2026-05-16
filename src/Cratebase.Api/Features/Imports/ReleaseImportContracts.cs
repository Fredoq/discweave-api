namespace Cratebase.Api.Features.Imports;

public sealed record DesktopFolderScanRequest(
    string SourceRoot,
    IReadOnlyList<DesktopFolderScanFileRequest>? Files,
    int IgnoredFileCount);

public sealed record DesktopFolderScanFileRequest(
    string FilePath,
    string RelativePath,
    string? Format,
    long SizeBytes,
    DateTimeOffset LastModifiedAt,
    DesktopAudioMetadataRequest? AudioMetadata,
    DesktopCoverArtifactRequest? CoverArtifact);

public sealed record DesktopAudioMetadataRequest(
    string? Title,
    IReadOnlyList<string>? Artists,
    string? AlbumTitle,
    IReadOnlyList<string>? AlbumArtists,
    string? CatalogNumber,
    string? ReleaseDate,
    int? Year,
    int? DurationSeconds,
    int? TrackNumber);

public sealed record DesktopCoverArtifactRequest(
    string FileName,
    string Extension,
    string ContentType,
    long SizeBytes,
    string ContentBase64);

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
