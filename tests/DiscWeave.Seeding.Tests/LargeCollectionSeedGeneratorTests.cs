using DiscWeave.Domain.Credits;
using DiscWeave.Domain.SharedKernel.Ids;

namespace DiscWeave.Seeding.Tests;

public sealed class LargeCollectionSeedGeneratorTests
{
    [Fact(DisplayName = "Large collection seed generator creates connected catalog graph at the requested scale")]
    public void LargeCollectionSeedGeneratorCreatesConnectedCatalogGraphAtTheRequestedScale()
    {
        var options = new LargeCollectionSeedOptions(18, 4, 12, 5);
        var collectionId = CollectionId.New();

        LargeCollectionSeedData data = LargeCollectionSeedGenerator.Generate(collectionId, options);

        Assert.Equal(options.ArtistCount, data.Artists.Count);
        Assert.Equal(options.LabelCount, data.Labels.Count);
        Assert.Equal(options.ReleaseCount, data.Releases.Count);
        Assert.Equal(options.ReleaseCount * options.TracksPerRelease, data.Tracks.Count);
        Assert.InRange(data.OwnedItems.Count, data.Releases.Count + 1, data.Releases.Count + data.Tracks.Count);
        Assert.All(data.Releases, release => Assert.Equal(options.TracksPerRelease, release.Tracklist.Count));
        Assert.Contains(data.Credits, credit => credit.Role == Credit.ToRoleCode(CreditRole.Remixer));
        Assert.Contains(data.ArtistRelations, relation => relation.Type == "memberOf");
        Assert.Contains(data.TrackRelations, relation => relation.RelationType == "remixOf");
        Assert.Contains(data.Playlists, playlist => playlist.Type == Domain.Playlists.PlaylistType.Smart);
        Assert.All(data.Artists, artist => Assert.Equal(collectionId, artist.CollectionId));
        Assert.All(data.Labels, label => Assert.Equal(collectionId, label.CollectionId));
        Assert.All(data.Releases, release => Assert.Equal(collectionId, release.CollectionId));
        Assert.All(data.Tracks, track => Assert.Equal(collectionId, track.CollectionId));
        Assert.All(data.OwnedItems, ownedItem => Assert.Equal(collectionId, ownedItem.CollectionId));
        Assert.All(data.Credits, credit => Assert.Equal(collectionId, credit.CollectionId));
        Assert.All(data.ArtistRelations, relation => Assert.Equal(collectionId, relation.CollectionId));
        Assert.All(data.TrackRelations, relation => Assert.Equal(collectionId, relation.CollectionId));
        Assert.All(data.Playlists, playlist => Assert.Equal(collectionId, playlist.CollectionId));
    }
}
