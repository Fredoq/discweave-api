namespace DiscWeave.Api.Features.Settings;

public sealed record NamingProfileRequest(
    string Name,
    string ReleaseFolderTemplate,
    string TrackFileTemplate,
    string TrackFileWithArtistTemplate,
    int? SortOrder,
    bool? IsDefault,
    bool? IsActive);
