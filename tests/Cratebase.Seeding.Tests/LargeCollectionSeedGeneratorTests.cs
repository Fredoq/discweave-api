using Cratebase.Domain.Credits;
using Cratebase.Domain.SharedKernel.Ids;

namespace Cratebase.Seeding.Tests;

public sealed class LargeCollectionSeedGeneratorTests
{
    [Fact(DisplayName = "Large collection seed generator creates connected catalog graph at the requested scale")]
    public void LargeCollectionSeedGeneratorCreatesConnectedCatalogGraphAtTheRequestedScale()
    {
        var options = new LargeCollectionSeedOptions(18, 4, 12, 5);

        LargeCollectionSeedData data = LargeCollectionSeedGenerator.Generate(CollectionId.New(), options);

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
    }
}
