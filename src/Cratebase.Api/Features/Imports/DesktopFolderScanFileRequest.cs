namespace Cratebase.Api.Features.Imports;

public sealed record DesktopFolderScanFileRequest(
    string FilePath,
    string RelativePath,
    string? Format,
    long SizeBytes,
    DateTimeOffset LastModifiedAt,
    string? ContentHash,
    DesktopAudioMetadataRequest? AudioMetadata,
    DesktopCoverArtifactRequest? CoverArtifact);
