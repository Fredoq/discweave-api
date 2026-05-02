using Cratebase.Domain.Collection;
using Cratebase.Domain.SharedKernel.Errors;
using Cratebase.Domain.SharedKernel.Ids;

namespace Cratebase.Domain.Tests.Collection;

public sealed class MusicCollectionTests
{
    [Fact(DisplayName = "Music collections belong to one user and require a name")]
    public void Music_collections_belong_to_one_user_and_require_a_name()
    {
        var userId = UserId.New();
        var collectionId = CollectionId.New();

        var collection = MusicCollection.Create(collectionId, userId, "  Main collection  ");

        Assert.Equal(collectionId, collection.Id);
        Assert.Equal(userId, collection.OwnerUserId);
        Assert.Equal("Main collection", collection.Name);
        Assert.Equal("collection.name_required", Assert.Throws<DomainException>(() => MusicCollection.Create(CollectionId.New(), userId, " ")).Code);
    }
}
