namespace Cratebase.Api.Features.Imports;

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
