using DiscWeave.Domain.Settings;
using DiscWeave.Domain.SharedKernel.Errors;
using DiscWeave.Domain.SharedKernel.Ids;
using DiscWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DiscWeave.Api.Features.Releases;

public static partial class ReleasesEndpointRouteBuilderExtensions
{
    private static async Task<string> ResolveReleaseTypeCodeAsync(
        DiscWeaveDbContext context,
        CollectionId collectionId,
        string? type,
        CancellationToken cancellationToken)
    {
        string code = string.IsNullOrWhiteSpace(type) ? "unknown" : type.Trim();
        CollectionDictionaryEntry? existing = await context.CollectionDictionaryEntries.SingleOrDefaultAsync(
            entry => entry.CollectionId == collectionId &&
                entry.Kind == DictionaryKind.ReleaseType &&
                entry.Code == code,
            cancellationToken);
        if (existing is not null)
        {
            return existing.IsActive
                ? existing.Code
                : throw new DomainException("release.type_invalid", "Release type is invalid");
        }

        int nextSortOrder = await context.CollectionDictionaryEntries
            .Where(entry => entry.CollectionId == collectionId && entry.Kind == DictionaryKind.ReleaseType)
            .Select(entry => (int?)entry.SortOrder)
            .MaxAsync(cancellationToken) ?? 0;
        var entry = CollectionDictionaryEntry.Create(
            CollectionDictionaryEntryId.New(),
            collectionId,
            DictionaryKind.ReleaseType,
            code,
            code,
            nextSortOrder + 10,
            isBuiltin: false);
        _ = context.CollectionDictionaryEntries.Add(entry);

        return entry.Code;
    }

    private static async Task<IReadOnlyList<string>> ResolveGenreCodesAsync(
        DiscWeaveDbContext context,
        CollectionId collectionId,
        IReadOnlyList<string>? genres,
        CancellationToken cancellationToken)
    {
        if (genres is null || genres.Count == 0)
        {
            return [];
        }

        string[] requestedCodes =
        [
            .. genres
                .Select(genre => string.IsNullOrWhiteSpace(genre)
                    ? throw new DomainException("release.genre_invalid", "Release genre is invalid")
                    : genre.Trim())
                .Distinct(StringComparer.Ordinal)
        ];

        int nextSortOrder = await context.CollectionDictionaryEntries
            .Where(entry => entry.CollectionId == collectionId && entry.Kind == DictionaryKind.Genre)
            .Select(entry => (int?)entry.SortOrder)
            .MaxAsync(cancellationToken) ?? 0;
        Dictionary<string, CollectionDictionaryEntry> existingByCode = await context.CollectionDictionaryEntries
            .Where(entry => entry.CollectionId == collectionId && entry.Kind == DictionaryKind.Genre)
            .ToDictionaryAsync(entry => entry.Code, StringComparer.Ordinal, cancellationToken);
        var resolved = new List<string>(requestedCodes.Length);

        foreach (string code in requestedCodes)
        {
            if (existingByCode.TryGetValue(code, out CollectionDictionaryEntry? existing))
            {
                if (!existing.IsActive)
                {
                    throw new DomainException("release.genre_invalid", "Release genre is invalid");
                }

                resolved.Add(existing.Code);
                continue;
            }

            nextSortOrder += 10;
            var entry = CollectionDictionaryEntry.Create(
                CollectionDictionaryEntryId.New(),
                collectionId,
                DictionaryKind.Genre,
                code,
                code,
                nextSortOrder,
                isBuiltin: false);
            _ = context.CollectionDictionaryEntries.Add(entry);
            existingByCode.Add(entry.Code, entry);
            resolved.Add(entry.Code);
        }

        return resolved;
    }
}
