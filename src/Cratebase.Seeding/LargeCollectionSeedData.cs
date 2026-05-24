using Cratebase.Domain.Catalog;
using Cratebase.Domain.Collection;
using Cratebase.Domain.Credits;
using Cratebase.Domain.Playlists;
using Cratebase.Domain.Relations;

namespace Cratebase.Seeding;

public sealed class LargeCollectionSeedData
{
    public required IReadOnlyList<Artist> Artists
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    } = [];

    public required IReadOnlyList<Label> Labels
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    } = [];

    public required IReadOnlyList<Release> Releases
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    } = [];

    public required IReadOnlyList<Track> Tracks
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    } = [];

    public required IReadOnlyList<OwnedItem> OwnedItems
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    } = [];

    public required IReadOnlyList<Credit> Credits
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    } = [];

    public required IReadOnlyList<ArtistRelation> ArtistRelations
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    } = [];

    public required IReadOnlyList<TrackRelation> TrackRelations
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    } = [];

    public required IReadOnlyList<Playlist> Playlists
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    } = [];
}
