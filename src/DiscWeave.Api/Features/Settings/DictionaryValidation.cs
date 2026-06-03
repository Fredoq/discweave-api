using DiscWeave.Domain.Settings;
using DiscWeave.Domain.SharedKernel.Errors;
using DiscWeave.Domain.SharedKernel.Ids;
using DiscWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DiscWeave.Api.Features.Settings;

internal static class DictionaryValidation
{
    public static async Task<string> RequireActiveCodeAsync(
        DiscWeaveDbContext context,
        CollectionId collectionId,
        DictionaryKind kind,
        string code,
        string errorCode,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        CollectionDictionaryEntry entry = await RequireActiveEntryAsync(
            context,
            collectionId,
            kind,
            code,
            errorCode,
            errorMessage,
            cancellationToken);

        return entry.Code;
    }

    public static async Task<string> ResolveOrCreateActiveCodeAsync(
        DiscWeaveDbContext context,
        CollectionId collectionId,
        DictionaryKind kind,
        string code,
        string errorCode,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new DomainException(errorCode, errorMessage);
        }

        string normalizedCode = code.Trim();
        CollectionDictionaryEntry? trackedEntry = context.CollectionDictionaryEntries.Local
            .FirstOrDefault(
                item => item.CollectionId == collectionId &&
                    item.Kind == kind &&
                    item.Code == normalizedCode);
        if (trackedEntry is not null)
        {
            return trackedEntry.IsActive
                ? trackedEntry.Code
                : throw new DomainException(errorCode, errorMessage);
        }

        CollectionDictionaryEntry? existing = await context.CollectionDictionaryEntries
            .SingleOrDefaultAsync(
                item => item.CollectionId == collectionId &&
                    item.Kind == kind &&
                    item.Code == normalizedCode,
                cancellationToken);
        if (existing is not null)
        {
            return existing.IsActive
                ? existing.Code
                : throw new DomainException(errorCode, errorMessage);
        }

        int persistedSortOrder = await context.CollectionDictionaryEntries
            .Where(entry => entry.CollectionId == collectionId && entry.Kind == kind)
            .Select(entry => (int?)entry.SortOrder)
            .MaxAsync(cancellationToken) ?? 0;
        int trackedSortOrder = context.CollectionDictionaryEntries.Local
            .Where(entry => entry.CollectionId == collectionId && entry.Kind == kind)
            .Max(entry => (int?)entry.SortOrder) ?? 0;
        int nextSortOrder = Math.Max(persistedSortOrder, trackedSortOrder);
        var entry = CollectionDictionaryEntry.Create(
            CollectionDictionaryEntryId.New(),
            collectionId,
            kind,
            normalizedCode,
            normalizedCode,
            nextSortOrder + 10,
            isBuiltin: false);
        _ = context.CollectionDictionaryEntries.Add(entry);

        return entry.Code;
    }

    public static async Task<string> RequireCodeAsync(
        DiscWeaveDbContext context,
        CollectionId collectionId,
        DictionaryKind kind,
        string code,
        string errorCode,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new DomainException(errorCode, errorMessage);
        }

        string normalizedCode = code.Trim();
        CollectionDictionaryEntry? entry = await context.CollectionDictionaryEntries.AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.CollectionId == collectionId &&
                    item.Kind == kind &&
                    item.Code == normalizedCode,
                cancellationToken);

        return entry?.Code ?? throw new DomainException(errorCode, errorMessage);
    }

    public static async Task<CollectionDictionaryEntry> RequireActiveEntryAsync(
        DiscWeaveDbContext context,
        CollectionId collectionId,
        DictionaryKind kind,
        string code,
        string errorCode,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new DomainException(errorCode, errorMessage);
        }

        string normalizedCode = code.Trim();
        CollectionDictionaryEntry? entry = await context.CollectionDictionaryEntries.AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.CollectionId == collectionId &&
                    item.Kind == kind &&
                    item.Code == normalizedCode &&
                    item.IsActive,
                cancellationToken);

        return entry ?? throw new DomainException(errorCode, errorMessage);
    }

    public static async Task<IReadOnlyList<string>> RequireActiveCodesAsync(
        DiscWeaveDbContext context,
        CollectionId collectionId,
        DictionaryKind kind,
        IReadOnlyList<string>? codes,
        string errorCode,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        if (codes is null || codes.Count == 0)
        {
            return [];
        }

        List<string> normalizedCodes = [];
        foreach (string code in codes)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new DomainException(errorCode, errorMessage);
            }

            normalizedCodes.Add(code.Trim());
        }

        string[] distinctCodes = [.. normalizedCodes.Distinct(StringComparer.Ordinal)];
        string[] activeCodes = await context.CollectionDictionaryEntries.AsNoTracking()
            .Where(item => item.CollectionId == collectionId &&
                item.Kind == kind &&
                item.IsActive &&
                distinctCodes.Contains(item.Code))
            .Select(item => item.Code)
            .ToArrayAsync(cancellationToken);
        HashSet<string> activeCodeSet = activeCodes.ToHashSet(StringComparer.Ordinal);

        return normalizedCodes.Any(code => !activeCodeSet.Contains(code))
            ? throw new DomainException(errorCode, errorMessage)
            : normalizedCodes;
    }
}
