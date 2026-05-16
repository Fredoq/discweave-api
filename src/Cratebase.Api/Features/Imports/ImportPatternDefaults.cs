using Cratebase.Domain.Imports;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Api.Features.Imports;

internal static class ImportPatternDefaults
{
    private static readonly (ImportPatternKind Kind, string Template, int SortOrder)[] Defaults =
    [
        (ImportPatternKind.ReleaseFolder, ReleaseFolderNameParser.DefaultTemplate, 10),
        (ImportPatternKind.TrackFile, "{position} {title}", 10),
        (ImportPatternKind.TrackFile, "{position} - {title}", 20),
        (ImportPatternKind.TrackFile, "{position} {artist} - {title}", 30),
        (ImportPatternKind.TrackFile, "{position} - {artist} - {title}", 40)
    ];

    public static async Task EnsureAsync(CratebaseDbContext context, CollectionId collectionId, CancellationToken cancellationToken)
    {
        bool hasPatterns = await context.ImportPatterns.AnyAsync(pattern => pattern.CollectionId == collectionId, cancellationToken);
        if (hasPatterns)
        {
            return;
        }

        foreach ((ImportPatternKind kind, string template, int sortOrder) in Defaults)
        {
            _ = context.ImportPatterns.Add(ImportPattern.Create(collectionId, ImportPatternId.New(), kind, template, sortOrder, isBuiltin: true));
        }
    }

    public static async Task<IReadOnlyList<string>> ActiveTemplatesAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        ImportPatternKind kind,
        CancellationToken cancellationToken)
    {
        await EnsureAsync(context, collectionId, cancellationToken);
        _ = await context.SaveChangesAsync(cancellationToken);

        return await context.ImportPatterns.AsNoTracking()
            .Where(pattern => pattern.CollectionId == collectionId && pattern.Kind == kind && pattern.IsActive)
            .OrderBy(pattern => pattern.SortOrder)
            .Select(pattern => pattern.Template)
            .ToArrayAsync(cancellationToken);
    }
}
