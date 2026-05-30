using DiscWeave.Domain.SharedKernel.Ids;

namespace DiscWeave.Domain.Imports;

public sealed record DraftTrackEditableFields(
    int? Position,
    string Title,
    TimeSpan? Duration,
    IReadOnlyList<string> ArtistNames,
    IReadOnlyList<ReleaseImportArtistCredit> ArtistCredits,
    IReadOnlyList<Guid> SelectedArtistIds,
    TrackId? SelectedTrackId,
    bool IsSkipped,
    IReadOnlyList<ImportReviewIssue> Issues);
