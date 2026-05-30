using System.Diagnostics.CodeAnalysis;
using DiscWeave.Domain.SharedKernel.Ids;
using DiscWeave.Domain.SharedKernel.Interfaces;
using DiscWeave.Domain.SharedKernel.Validation;

namespace DiscWeave.Domain.Collection;

[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Music Collection is the domain term for a user's archive boundary.")]
public sealed class MusicCollection : IEntity<CollectionId>, INamedEntity
{
    private MusicCollection(CollectionId id, UserId ownerUserId, string name, DateTimeOffset createdAt)
    {
        Id = id;
        OwnerUserId = ownerUserId;
        Name = Guard.RequiredText(name, nameof(name), "collection.name_required");
        CreatedAt = createdAt;
    }

    public CollectionId Id { get; }

    public UserId OwnerUserId { get; }

    public string Name { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public static MusicCollection Create(CollectionId id, UserId ownerUserId, string name)
    {
        return new MusicCollection(id, ownerUserId, name, DateTimeOffset.UtcNow);
    }
}
