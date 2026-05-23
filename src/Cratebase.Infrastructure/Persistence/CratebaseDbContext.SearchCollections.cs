using Cratebase.Domain.Catalog;
using Cratebase.Domain.Collection;
using Cratebase.Domain.Credits;
using Cratebase.Domain.Playlists;
using Cratebase.Domain.Relations;
using Cratebase.Domain.Settings;
using Cratebase.Domain.SharedKernel.Ids;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Cratebase.Infrastructure.Persistence;

public partial class CratebaseDbContext
{
    private HashSet<CollectionId> CollectSearchDocumentCollections()
    {
        HashSet<CollectionId> collectionIds = [];

        foreach (EntityEntry entry in ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            if (TryGetSearchCollectionId(entry, out CollectionId collectionId))
            {
                _ = collectionIds.Add(collectionId);
            }
        }

        return collectionIds;
    }

    private bool TryGetSearchCollectionId(EntityEntry entry, out CollectionId collectionId)
    {
        switch (entry.Entity)
        {
            case Artist artist:
                collectionId = artist.CollectionId;
                return true;
            case Label label:
                collectionId = label.CollectionId;
                return true;
            case Release release:
                collectionId = release.CollectionId;
                return true;
            case Track track:
                collectionId = track.CollectionId;
                return true;
            case OwnedItem ownedItem:
                collectionId = ownedItem.CollectionId;
                return true;
            case Playlist playlist:
                collectionId = playlist.CollectionId;
                return true;
            case Credit credit:
                collectionId = credit.CollectionId;
                return true;
            case ArtistRelation artistRelation:
                collectionId = artistRelation.CollectionId;
                return true;
            case TrackRelation trackRelation:
                collectionId = trackRelation.CollectionId;
                return true;
            case CollectionDictionaryEntry dictionaryEntry:
                collectionId = dictionaryEntry.CollectionId;
                return true;
            default:
                return TryGetOwnedSearchCollectionId(entry, out collectionId);
        }
    }

    private bool TryGetOwnedSearchCollectionId(EntityEntry entry, out CollectionId collectionId)
    {
        collectionId = default;

        if (!entry.Metadata.IsOwned())
        {
            return false;
        }

        if (TryReadCollectionIdProperty(entry, out collectionId))
        {
            return true;
        }

        if (HasCurrentCollection)
        {
            collectionId = CurrentCollectionId;
            return true;
        }

        return false;
    }

    private static bool TryReadCollectionIdProperty(EntityEntry entry, out CollectionId collectionId)
    {
        collectionId = default;
        if (entry.Metadata.FindProperty(CollectionIdProperty) is null)
        {
            return false;
        }

        object? value = entry.State == EntityState.Deleted
            ? entry.Property(CollectionIdProperty).OriginalValue
            : entry.Property(CollectionIdProperty).CurrentValue;
        if (value is not CollectionId id)
        {
            return false;
        }

        collectionId = id;
        return true;
    }
}
