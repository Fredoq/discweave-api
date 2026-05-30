using DiscWeave.Domain.Imports;
using DiscWeave.Domain.SharedKernel.Ids;
using DiscWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DiscWeave.Api.Features.Imports;

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

    public static async Task EnsureAsync(DiscWeaveDbContext context, CollectionId collectionId, CancellationToken cancellationToken)
    {
        foreach ((ImportPatternKind kind, string template, int sortOrder) in Defaults)
        {
            _ = await context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO import_patterns (import_pattern_id, collection_id, kind, template, sort_order, is_active, is_builtin)
                VALUES ({ImportPatternId.New().Value}, {collectionId.Value}, {kind.ToString()}, {template}, {sortOrder}, {true}, {true})
                ON CONFLICT (collection_id, kind, template, is_builtin) DO NOTHING
                """, cancellationToken);
        }
    }

    public static async Task<IReadOnlyList<string>> ActiveTemplatesAsync(
        DiscWeaveDbContext context,
        CollectionId collectionId,
        ImportPatternKind kind,
        CancellationToken cancellationToken)
    {
        await EnsureAsync(context, collectionId, cancellationToken);

        return await context.ImportPatterns.AsNoTracking()
            .Where(pattern => pattern.CollectionId == collectionId && pattern.Kind == kind && pattern.IsActive)
            .OrderBy(pattern => pattern.SortOrder)
            .Select(pattern => pattern.Template)
            .ToArrayAsync(cancellationToken);
    }
}
