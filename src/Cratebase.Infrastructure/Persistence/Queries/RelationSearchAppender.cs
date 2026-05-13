using Cratebase.Domain.Catalog;
using Cratebase.Domain.Relations;
using Cratebase.Domain.Settings;
using Cratebase.Domain.SharedKernel.Ids;

namespace Cratebase.Infrastructure.Persistence.Queries;

internal static class RelationSearchAppender
{
    public static void AddRelations(
        string term,
        SearchResultAccumulator accumulator,
        DictionarySearchLookup dictionaries,
        IReadOnlyList<ArtistRelation> artistRelations,
        IReadOnlyList<TrackRelation> trackRelations,
        Dictionary<ArtistId, Artist> artists,
        Dictionary<TrackId, Track> tracks)
    {
        foreach (ArtistRelation relation in artistRelations.Where(relation => dictionaries.Contains(DictionaryKind.ArtistRelationType, relation.Type, term)))
        {
            string label = dictionaries.LabelOrCode(DictionaryKind.ArtistRelationType, relation.Type);
            AddArtistRelationMatch(accumulator, artists, relation.SourceArtistId, label);
            AddArtistRelationMatch(accumulator, artists, relation.TargetArtistId, label);
        }

        foreach (TrackRelation relation in trackRelations.Where(relation => dictionaries.Contains(DictionaryKind.TrackRelationType, relation.RelationType, term)))
        {
            string label = dictionaries.LabelOrCode(DictionaryKind.TrackRelationType, relation.RelationType);
            AddTrackRelationMatch(accumulator, tracks, relation.SourceTrackId, label);
            AddTrackRelationMatch(accumulator, tracks, relation.TargetTrackId, label);
        }
    }

    private static void AddArtistRelationMatch(
        SearchResultAccumulator accumulator,
        Dictionary<ArtistId, Artist> artists,
        ArtistId artistId,
        string relationLabel)
    {
        if (artists.TryGetValue(artistId, out Artist? artist))
        {
            accumulator.Add(artist.Id.Value, "artist", artist.Name, relationLabel, "relation.type", 50);
        }
    }

    private static void AddTrackRelationMatch(
        SearchResultAccumulator accumulator,
        Dictionary<TrackId, Track> tracks,
        TrackId trackId,
        string relationLabel)
    {
        if (tracks.TryGetValue(trackId, out Track? track))
        {
            accumulator.Add(track.Id.Value, "track", track.Title, relationLabel, "relation.type", 50);
        }
    }
}
