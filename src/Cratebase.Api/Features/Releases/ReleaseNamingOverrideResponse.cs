namespace Cratebase.Api.Features.Releases;

public sealed record ReleaseNamingOverrideResponse(
    Guid ReleaseId,
    Guid? NamingProfileId,
    string? ReleaseFolderTemplate,
    string? TrackFileTemplate,
    string? TrackFileWithArtistTemplate,
    string? Source);
