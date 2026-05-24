using Cratebase.Domain.SharedKernel.Ids;

namespace Cratebase.Seeding;

public sealed class LargeCollectionSeedResult
{
    public LargeCollectionSeedResult(
        bool wasSeeded,
        string email,
        CollectionId collectionId,
        LargeCollectionSeedData? data)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        WasSeeded = wasSeeded;
        Email = email;
        CollectionId = collectionId;
        Data = data;
    }

    public bool WasSeeded { get; }

    public string Email { get; }

    public CollectionId CollectionId { get; }

    public LargeCollectionSeedData? Data { get; }
}
