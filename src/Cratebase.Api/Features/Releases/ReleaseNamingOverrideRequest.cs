namespace Cratebase.Api.Features.Releases;

public sealed record ReleaseNamingOverrideRequest(
    Guid? NamingProfileId,
    string? ReleaseFolderTemplate,
    string? TrackFileTemplate,
    string? TrackFileWithArtistTemplate,
    string? Source);
