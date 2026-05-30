namespace DiscWeave.Api.Features.Settings;

public sealed record NamingProfileResponse(
    Guid Id,
    string Name,
    string ReleaseFolderTemplate,
    string TrackFileTemplate,
    string TrackFileWithArtistTemplate,
    int SortOrder,
    bool IsDefault,
    bool IsActive,
    bool IsBuiltin);
