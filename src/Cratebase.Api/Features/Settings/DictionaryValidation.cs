using Cratebase.Domain.Settings;
using Cratebase.Domain.SharedKernel.Errors;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Api.Features.Settings;

internal static class DictionaryValidation
{
    public static async Task<string> RequireActiveCodeAsync(
        CratebaseDbContext context,
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

    public static async Task<string> RequireCodeAsync(
        CratebaseDbContext context,
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
        CratebaseDbContext context,
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
        CratebaseDbContext context,
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
