using Cratebase.Api.Features.ArtistRelations;
using Cratebase.Api.Features.Artists;
using Cratebase.Api.Features.Credits;
using Cratebase.Api.Features.Labels;
using Cratebase.Api.Features.OwnedItems;
using Cratebase.Api.Features.Playlists;
using Cratebase.Api.Features.Ratings;
using Cratebase.Api.Features.Releases;
using Cratebase.Api.Features.Settings;
using Cratebase.Api.Features.TrackRelations;
using Cratebase.Api.Features.Tracks;

namespace Cratebase.Api.Features.Exports;

public sealed record ExportSnapshotResponse
{
    public int FormatVersion { get; init; }

    public IReadOnlyList<ArtistResponse> Artists { get; init; } = [];

    public IReadOnlyList<LabelResponse> Labels { get; init; } = [];

    public IReadOnlyList<ReleaseResponse> Releases { get; init; } = [];

    public IReadOnlyList<TrackResponse> Tracks { get; init; } = [];

    public IReadOnlyList<OwnedItemResponse> OwnedItems { get; init; } = [];

    public IReadOnlyList<PlaylistResponse> Playlists { get; init; } = [];

    public IReadOnlyList<CreditResponse> Credits { get; init; } = [];

    public IReadOnlyList<ArtistRelationResponse> ArtistRelations { get; init; } = [];

    public IReadOnlyList<TrackRelationResponse> TrackRelations { get; init; } = [];

    public IReadOnlyList<DictionaryEntryResponse> Dictionaries { get; init; } = [];

    public IReadOnlyList<ImportPatternResponse> ImportPatterns { get; init; } = [];

    public IReadOnlyList<NamingProfileResponse> NamingProfiles { get; init; } = [];

    public IReadOnlyList<TagRoleMappingResponse> TagRoleMappings { get; init; } = [];

    public IReadOnlyList<ReleaseNamingOverrideResponse> ReleaseNamingOverrides { get; init; } = [];

    public IReadOnlyList<RatingCriterionResponse> RatingCriteria { get; init; } = [];

    public IReadOnlyList<RatingValueResponse> Ratings { get; init; } = [];
}
