namespace Cratebase.Application.Imports;

public sealed record AudioMetadata(
    string? Title,
    IReadOnlyList<string> Artists,
    string? AlbumTitle,
    IReadOnlyList<string> AlbumArtists,
    string? CatalogNumber,
    DateOnly? ReleaseDate,
    int? Year,
    TimeSpan? Duration,
    int? TrackNumber);
