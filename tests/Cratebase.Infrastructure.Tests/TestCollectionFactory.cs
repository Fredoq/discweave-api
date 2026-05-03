using Cratebase.Domain.Collection;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Infrastructure.Identity;
using Cratebase.Infrastructure.Persistence;

namespace Cratebase.Infrastructure.Tests;

internal static class TestCollectionFactory
{
    public static async Task AddCollectionAsync(CratebaseDbContext context, CollectionId collectionId)
    {
        var ownerUserId = UserId.New();
        string email = $"{ownerUserId.Value:N}@example.com";
        var user = new CratebaseUser
        {
            Id = ownerUserId.Value,
            Email = email,
            UserName = email
        };

        _ = context.Users.Add(user);
        _ = await context.SaveChangesAsync();

        _ = context.MusicCollections.Add(MusicCollection.Create(collectionId, ownerUserId, "Main collection"));
        _ = await context.SaveChangesAsync();

        user.DefaultCollectionId = collectionId;
        _ = await context.SaveChangesAsync();
    }
}
