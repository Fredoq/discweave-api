using DiscWeave.Domain.Settings;
using DiscWeave.Domain.SharedKernel.Ids;
using DiscWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DiscWeave.Api.Features.Settings;

internal static class NamingProfileDefaults
{
    private const string DefaultName = "DiscWeave default";
    private const string DefaultReleaseFolderTemplate = "[{catalogNumber}, {releaseDate}] {releaseArtists} - {title}";
    private const string DefaultTrackFileTemplate = "{position2} {title}";
    private const string DefaultTrackFileWithArtistTemplate = "{position2} {trackArtists} - {title}";

    public static async Task EnsureAsync(DiscWeaveDbContext context, CollectionId collectionId, CancellationToken cancellationToken)
    {
        NamingProfile[] builtins = await context.NamingProfiles
            .Where(profile => profile.CollectionId == collectionId && profile.IsBuiltin)
            .ToArrayAsync(cancellationToken);

        if (builtins.Length == 0)
        {
            _ = context.NamingProfiles.Add(NamingProfile.Create(
                collectionId,
                NamingProfileId.New(),
                (
                    DefaultName,
                    DefaultReleaseFolderTemplate,
                    DefaultTrackFileTemplate,
                    DefaultTrackFileWithArtistTemplate,
                    10,
                    IsDefault: true,
                    IsBuiltin: true)));
            await SaveSeedChangesAsync(context, collectionId, cancellationToken);
            return;
        }

        foreach (NamingProfile builtin in builtins)
        {
            builtin.SyncBuiltinDefaults(
                DefaultName,
                DefaultReleaseFolderTemplate,
                DefaultTrackFileTemplate,
                DefaultTrackFileWithArtistTemplate,
                10);
        }

        bool hasDefault = await context.NamingProfiles
            .AnyAsync(profile => profile.CollectionId == collectionId && profile.IsDefault, cancellationToken);
        if (hasDefault)
        {
            _ = await context.SaveChangesAsync(cancellationToken);
            return;
        }

        NamingProfile? firstActive = await context.NamingProfiles
            .Where(profile => profile.CollectionId == collectionId && profile.IsActive)
            .OrderBy(profile => profile.SortOrder)
            .FirstOrDefaultAsync(cancellationToken);
        firstActive?.SetDefault(true);
        await SaveSeedChangesAsync(context, collectionId, cancellationToken);
    }

    private static async Task SaveSeedChangesAsync(
        DiscWeaveDbContext context,
        CollectionId collectionId,
        CancellationToken cancellationToken)
    {
        try
        {
            _ = await context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception)
        {
            context.ChangeTracker.Clear();
            bool hasBuiltin = await context.NamingProfiles.AsNoTracking()
                .AnyAsync(profile => profile.CollectionId == collectionId && profile.IsBuiltin, cancellationToken);
            if (!hasBuiltin)
            {
                throw;
            }
        }
    }
}
