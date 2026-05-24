namespace Cratebase.Api.Features.Exports;

public sealed record ExportRestoreResponse(
    bool Restored,
    int FormatVersion,
    int Artists,
    int Labels,
    int Releases,
    int Tracks,
    int OwnedItems,
    int Playlists,
    int Credits,
    int ArtistRelations,
    int TrackRelations,
    int Dictionaries,
    int ImportPatterns,
    int RatingCriteria,
    int Ratings);
