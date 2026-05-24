using Cratebase.Domain.Catalog;
using Cratebase.Domain.Collection;
using Cratebase.Domain.Credits;
using Cratebase.Domain.Playlists;
using Cratebase.Domain.Relations;

namespace Cratebase.Seeding;

public sealed class LargeCollectionSeedData
{
    public LargeCollectionSeedData(
        IReadOnlyList<Artist> artists,
        IReadOnlyList<Label> labels,
        IReadOnlyList<Release> releases,
        IReadOnlyList<Track> tracks,
        IReadOnlyList<OwnedItem> ownedItems,
        IReadOnlyList<Credit> credits,
        IReadOnlyList<ArtistRelation> artistRelations,
        IReadOnlyList<TrackRelation> trackRelations,
        IReadOnlyList<Playlist> playlists)
    {
        Artists = artists;
        Labels = labels;
        Releases = releases;
        Tracks = tracks;
        OwnedItems = ownedItems;
        Credits = credits;
        ArtistRelations = artistRelations;
        TrackRelations = trackRelations;
        Playlists = playlists;
    }

    public IReadOnlyList<Artist> Artists { get; }

    public IReadOnlyList<Label> Labels { get; }

    public IReadOnlyList<Release> Releases { get; }

    public IReadOnlyList<Track> Tracks { get; }

    public IReadOnlyList<OwnedItem> OwnedItems { get; }

    public IReadOnlyList<Credit> Credits { get; }

    public IReadOnlyList<ArtistRelation> ArtistRelations { get; }

    public IReadOnlyList<TrackRelation> TrackRelations { get; }

    public IReadOnlyList<Playlist> Playlists { get; }
}
