using Cratebase.Domain.SharedKernel.Ids;

namespace Cratebase.Domain.Catalog;

public sealed class Group : Artist
{
    private Group(CollectionId collectionId, ArtistId id, string name)
        : base(collectionId, id, name)
    {
    }

    public static Group Create(CollectionId collectionId, ArtistId id, string name)
    {
        return new Group(collectionId, id, name);
    }
}
