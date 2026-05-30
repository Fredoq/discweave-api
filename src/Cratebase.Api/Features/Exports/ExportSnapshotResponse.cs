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
using System.Text.Json.Serialization;

namespace Cratebase.Api.Features.Exports;

public sealed record ExportSnapshotResponse
{
    [JsonConstructor]
    public ExportSnapshotResponse(
        int formatVersion,
        IReadOnlyList<ArtistResponse>? artists = null,
        IReadOnlyList<LabelResponse>? labels = null,
        IReadOnlyList<ReleaseResponse>? releases = null,
        IReadOnlyList<TrackResponse>? tracks = null,
        IReadOnlyList<OwnedItemResponse>? ownedItems = null,
        IReadOnlyList<PlaylistResponse>? playlists = null,
        IReadOnlyList<CreditResponse>? credits = null,
        IReadOnlyList<ArtistRelationResponse>? artistRelations = null,
        IReadOnlyList<TrackRelationResponse>? trackRelations = null,
        IReadOnlyList<DictionaryEntryResponse>? dictionaries = null,
        IReadOnlyList<ImportPatternResponse>? importPatterns = null,
        IReadOnlyList<NamingProfileResponse>? namingProfiles = null,
        IReadOnlyList<TagRoleMappingResponse>? tagRoleMappings = null,
        IReadOnlyList<ReleaseNamingOverrideResponse>? releaseNamingOverrides = null,
        IReadOnlyList<RatingCriterionResponse>? ratingCriteria = null,
        IReadOnlyList<RatingValueResponse>? ratings = null)
    {
        FormatVersion = formatVersion;
        Artists = artists ?? [];
        Labels = labels ?? [];
        Releases = releases ?? [];
        Tracks = tracks ?? [];
        OwnedItems = ownedItems ?? [];
        Playlists = playlists ?? [];
        Credits = credits ?? [];
        ArtistRelations = artistRelations ?? [];
        TrackRelations = trackRelations ?? [];
        Dictionaries = dictionaries ?? [];
        ImportPatterns = importPatterns ?? [];
        NamingProfiles = namingProfiles ?? [];
        TagRoleMappings = tagRoleMappings ?? [];
        ReleaseNamingOverrides = releaseNamingOverrides ?? [];
        RatingCriteria = ratingCriteria ?? [];
        Ratings = ratings ?? [];
    }

    public int FormatVersion { get; }

    public IReadOnlyList<ArtistResponse> Artists { get; }

    public IReadOnlyList<LabelResponse> Labels { get; }

    public IReadOnlyList<ReleaseResponse> Releases { get; }

    public IReadOnlyList<TrackResponse> Tracks { get; }

    public IReadOnlyList<OwnedItemResponse> OwnedItems { get; }

    public IReadOnlyList<PlaylistResponse> Playlists { get; }

    public IReadOnlyList<CreditResponse> Credits { get; }

    public IReadOnlyList<ArtistRelationResponse> ArtistRelations { get; }

    public IReadOnlyList<TrackRelationResponse> TrackRelations { get; }

    public IReadOnlyList<DictionaryEntryResponse> Dictionaries { get; }

    public IReadOnlyList<ImportPatternResponse> ImportPatterns { get; }

    public IReadOnlyList<NamingProfileResponse> NamingProfiles { get; }

    public IReadOnlyList<TagRoleMappingResponse> TagRoleMappings { get; }

    public IReadOnlyList<ReleaseNamingOverrideResponse> ReleaseNamingOverrides { get; }

    public IReadOnlyList<RatingCriterionResponse> RatingCriteria { get; }

    public IReadOnlyList<RatingValueResponse> Ratings { get; }
}
