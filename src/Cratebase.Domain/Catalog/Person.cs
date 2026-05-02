using Cratebase.Domain.SharedKernel.Ids;

namespace Cratebase.Domain.Catalog;

public sealed class Person : Artist
{
    private Person(CollectionId collectionId, ArtistId id, string name)
        : base(collectionId, id, name)
    {
    }

    public static Person Create(CollectionId collectionId, ArtistId id, string name)
    {
        return new Person(collectionId, id, name);
    }
}
