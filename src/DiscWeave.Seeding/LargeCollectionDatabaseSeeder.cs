using DiscWeave.Domain.Collection;
using DiscWeave.Domain.Ratings;
using DiscWeave.Domain.Settings;
using DiscWeave.Domain.SharedKernel.Ids;
using DiscWeave.Infrastructure.Identity;
using DiscWeave.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace DiscWeave.Seeding;

public static class LargeCollectionDatabaseSeeder
{
    public static async Task<LargeCollectionSeedResult> SeedAsync(
        DiscWeaveDbContext context,
        UserManager<DiscWeaveUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        SeedCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(userManager);
        ArgumentNullException.ThrowIfNull(roleManager);
        ArgumentNullException.ThrowIfNull(command);

        DiscWeaveUser user = await EnsureSeedUserAsync(
            context,
            userManager,
            roleManager,
            command,
            cancellationToken);
        CollectionId collectionId = user.DefaultCollectionId ?? throw new InvalidOperationException("Seed user default collection is missing");

        if (await HasCatalogDataAsync(context, collectionId, cancellationToken))
        {
            return new LargeCollectionSeedResult(false, user.Email ?? command.Email, collectionId, null);
        }

        LargeCollectionSeedData data = LargeCollectionSeedGenerator.Generate(collectionId, command.Options);
        context.Artists.AddRange(data.Artists);
        context.Labels.AddRange(data.Labels);
        context.Tracks.AddRange(data.Tracks);
        context.Releases.AddRange(data.Releases);
        context.OwnedItems.AddRange(data.OwnedItems);
        context.Credits.AddRange(data.Credits);
        context.ArtistRelations.AddRange(data.ArtistRelations);
        context.TrackRelations.AddRange(data.TrackRelations);
        context.Playlists.AddRange(data.Playlists);

        _ = await context.SaveChangesAsync(cancellationToken);

        return new LargeCollectionSeedResult(true, user.Email ?? command.Email, collectionId, data);
    }

    private static async Task<DiscWeaveUser> EnsureSeedUserAsync(
        DiscWeaveDbContext context,
        UserManager<DiscWeaveUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        SeedCommand command,
        CancellationToken cancellationToken)
    {
        string trimmedEmail = command.Email.Trim();
        DiscWeaveUser? existingUser = await userManager.FindByEmailAsync(trimmedEmail);
        if (existingUser is not null)
        {
            if (existingUser.DefaultCollectionId is null)
            {
                await using IDbContextTransaction transaction =
                    await context.Database.BeginTransactionAsync(cancellationToken);
                await CreateDefaultCollectionAsync(context, existingUser, userManager, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }

            return existingUser;
        }

        await using IDbContextTransaction createUserTransaction =
            await context.Database.BeginTransactionAsync(cancellationToken);
        await EnsureRolesAsync(roleManager);

        var user = new DiscWeaveUser
        {
            Id = Guid.CreateVersion7(),
            Email = trimmedEmail,
            UserName = trimmedEmail
        };
        IdentityResult createResult = await userManager.CreateAsync(user, command.Password);
        EnsureIdentitySucceeded(createResult, "Seed user could not be created");
        IdentityResult roleResult = await userManager.AddToRolesAsync(user, [DiscWeaveRoles.Admin, DiscWeaveRoles.User]);
        EnsureIdentitySucceeded(roleResult, "Seed user roles could not be assigned");

        await CreateDefaultCollectionAsync(context, user, userManager, cancellationToken);
        await createUserTransaction.CommitAsync(cancellationToken);

        return user;
    }

    private static async Task CreateDefaultCollectionAsync(
        DiscWeaveDbContext context,
        DiscWeaveUser user,
        UserManager<DiscWeaveUser> userManager,
        CancellationToken cancellationToken)
    {
        var collectionId = CollectionId.New();
        _ = context.MusicCollections.Add(MusicCollection.Create(collectionId, new UserId(user.Id), "Seed collection"));
        context.CollectionDictionaryEntries.AddRange(CollectionDictionaryDefaults.CreateEntries(collectionId));
        context.RatingCriteria.AddRange(RatingCriterionDefaults.CreateCriteria(collectionId));
        _ = await context.SaveChangesAsync(cancellationToken);

        user.DefaultCollectionId = collectionId;
        IdentityResult updateResult = await userManager.UpdateAsync(user);
        EnsureIdentitySucceeded(updateResult, "Seed user default collection could not be assigned");
    }

    private static async Task EnsureRolesAsync(RoleManager<IdentityRole<Guid>> roleManager)
    {
        foreach (string roleName in new[] { DiscWeaveRoles.Admin, DiscWeaveRoles.User })
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                IdentityResult result = await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
                EnsureIdentitySucceeded(result, "Seed roles could not be created");
            }
        }
    }

    private static async Task<bool> HasCatalogDataAsync(
        DiscWeaveDbContext context,
        CollectionId collectionId,
        CancellationToken cancellationToken)
    {
        return await context.Artists.AnyAsync(entity => entity.CollectionId == collectionId, cancellationToken) ||
            await context.Labels.AnyAsync(entity => entity.CollectionId == collectionId, cancellationToken) ||
            await context.Releases.AnyAsync(entity => entity.CollectionId == collectionId, cancellationToken) ||
            await context.Tracks.AnyAsync(entity => entity.CollectionId == collectionId, cancellationToken) ||
            await context.OwnedItems.AnyAsync(entity => entity.CollectionId == collectionId, cancellationToken) ||
            await context.Playlists.AnyAsync(entity => entity.CollectionId == collectionId, cancellationToken) ||
            await context.Credits.AnyAsync(entity => entity.CollectionId == collectionId, cancellationToken) ||
            await context.ArtistRelations.AnyAsync(entity => entity.CollectionId == collectionId, cancellationToken) ||
            await context.TrackRelations.AnyAsync(entity => entity.CollectionId == collectionId, cancellationToken);
    }

    private static void EnsureIdentitySucceeded(IdentityResult result, string message)
    {
        if (!result.Succeeded)
        {
            string code = result.Errors.FirstOrDefault()?.Code ?? "identity_error";
            throw new InvalidOperationException($"{message}: {code}");
        }
    }
}
