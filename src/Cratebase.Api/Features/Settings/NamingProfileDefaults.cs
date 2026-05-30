using Cratebase.Domain.Settings;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Api.Features.Settings;

internal static class NamingProfileDefaults
{
    private const string DefaultName = "Cratebase default";
    private const string DefaultReleaseFolderTemplate = "[{catalogNumber}, {releaseDate}] {releaseArtists} - {title}";
    private const string DefaultTrackFileTemplate = "{position2} {title}";
    private const string DefaultTrackFileWithArtistTemplate = "{position2} {trackArtists} - {title}";

    public static async Task EnsureAsync(CratebaseDbContext context, CollectionId collectionId, CancellationToken cancellationToken)
    {
        NamingProfile[] builtins = await context.NamingProfiles
            .Where(profile => profile.CollectionId == collectionId && profile.IsBuiltin)
            .ToArrayAsync(cancellationToken);

        if (builtins.Length == 0)
        {
            _ = context.NamingProfiles.Add(NamingProfile.Create(
                collectionId,
                NamingProfileId.New(),
                DefaultName,
                DefaultReleaseFolderTemplate,
                DefaultTrackFileTemplate,
                DefaultTrackFileWithArtistTemplate,
                10,
                isDefault: true,
                isBuiltin: true));
            _ = await context.SaveChangesAsync(cancellationToken);
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
        _ = await context.SaveChangesAsync(cancellationToken);
    }
}
