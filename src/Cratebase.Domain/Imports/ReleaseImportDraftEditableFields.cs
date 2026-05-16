using Cratebase.Domain.SharedKernel.Optional;

namespace Cratebase.Domain.Imports;

public sealed record ReleaseImportDraftEditableFields(
    string Title,
    string Type,
    IOptionalValue<string> CatalogNumber,
    IOptionalValue<string> LabelName,
    IOptionalValue<DateOnly> ReleaseDate,
    IOptionalValue<int> Year,
    bool IsVariousArtists,
    bool NotOnLabel,
    IOptionalValue<string> CoverPath,
    IReadOnlyList<string> ArtistNames,
    IReadOnlyList<ReleaseImportArtistCredit> ArtistCredits,
    IReadOnlyList<ReleaseImportLabel> Labels,
    IReadOnlyList<Guid> SelectedArtistIds,
    IReadOnlyList<string> Genres,
    IReadOnlyList<string> Tags,
    IReadOnlyList<ImportReviewIssue> Issues);
