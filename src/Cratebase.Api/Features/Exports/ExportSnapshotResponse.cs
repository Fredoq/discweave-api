using Cratebase.Api.Features.ArtistRelations;
using Cratebase.Api.Features.Artists;
using Cratebase.Api.Features.Credits;
using Cratebase.Api.Features.Labels;
using Cratebase.Api.Features.OwnedItems;
using Cratebase.Api.Features.Ratings;
using Cratebase.Api.Features.Releases;
using Cratebase.Api.Features.Settings;
using Cratebase.Api.Features.TrackRelations;
using Cratebase.Api.Features.Tracks;

namespace Cratebase.Api.Features.Exports;

public sealed record ExportSnapshotResponse(
    int FormatVersion,
    IReadOnlyList<ArtistResponse> Artists,
    IReadOnlyList<LabelResponse> Labels,
    IReadOnlyList<ReleaseResponse> Releases,
    IReadOnlyList<TrackResponse> Tracks,
    IReadOnlyList<OwnedItemResponse> OwnedItems,
    IReadOnlyList<CreditResponse> Credits,
    IReadOnlyList<ArtistRelationResponse> ArtistRelations,
    IReadOnlyList<TrackRelationResponse> TrackRelations,
    IReadOnlyList<DictionaryEntryResponse> Dictionaries,
    IReadOnlyList<ImportPatternResponse> ImportPatterns,
    IReadOnlyList<RatingCriterionResponse> RatingCriteria,
    IReadOnlyList<RatingValueResponse> Ratings);
