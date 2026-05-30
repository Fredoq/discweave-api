using DiscWeave.Domain.Collection;
using DiscWeave.Domain.Ratings;
using DiscWeave.Domain.Settings;
using DiscWeave.Domain.SharedKernel.Ids;
using DiscWeave.Infrastructure.Identity;
using DiscWeave.Infrastructure.Persistence;

namespace DiscWeave.Infrastructure.Tests;

internal static class TestCollectionFactory
{
    public static async Task AddCollectionAsync(DiscWeaveDbContext context, CollectionId collectionId)
    {
        var ownerUserId = UserId.New();
        string email = $"{ownerUserId.Value:N}@example.com";
        var user = new DiscWeaveUser
        {
            Id = ownerUserId.Value,
            Email = email,
            UserName = email
        };

        _ = context.Users.Add(user);
        _ = await context.SaveChangesAsync();

        _ = context.MusicCollections.Add(MusicCollection.Create(collectionId, ownerUserId, "Main collection"));
        context.CollectionDictionaryEntries.AddRange(CollectionDictionaryDefaults.CreateEntries(collectionId));
        context.RatingCriteria.AddRange(RatingCriterionDefaults.CreateCriteria(collectionId));
        _ = await context.SaveChangesAsync();

        user.DefaultCollectionId = collectionId;
        _ = await context.SaveChangesAsync();
    }
}
