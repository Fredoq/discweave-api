using DiscWeave.Domain.Settings;
using DiscWeave.Domain.SharedKernel.Ids;
using DiscWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DiscWeave.Api.Features.Settings;

internal static class TagRoleMappingDefaults
{
    private static readonly BuiltinTagRoleMapping[] Builtins =
    [
        new("remixer", "remixer", 30),
        new("producer", "producer", 40),
        new("composer", "composer", 50)
    ];

    public static async Task EnsureAsync(DiscWeaveDbContext context, CollectionId collectionId, CancellationToken cancellationToken)
    {
        TagRoleMapping[] existingMappings = await context.TagRoleMappings
            .Where(mapping => mapping.CollectionId == collectionId)
            .ToArrayAsync(cancellationToken);
        var existingBuiltinRoles = existingMappings
            .Where(mapping => mapping.IsBuiltin)
            .Select(mapping => mapping.CreditRoleCode)
            .ToHashSet(StringComparer.Ordinal);

        foreach (BuiltinTagRoleMapping builtin in Builtins)
        {
            if (existingBuiltinRoles.Contains(builtin.CreditRoleCode))
            {
                continue;
            }

            bool roleExists = await context.CollectionDictionaryEntries.AsNoTracking()
                .AnyAsync(
                    entry => entry.CollectionId == collectionId &&
                        entry.Kind == DictionaryKind.CreditRole &&
                        entry.Code == builtin.CreditRoleCode,
                    cancellationToken);
            if (!roleExists)
            {
                continue;
            }

            _ = context.TagRoleMappings.Add(TagRoleMapping.Create(
                collectionId,
                TagRoleMappingId.New(),
                builtin.CreditRoleCode,
                builtin.TagField,
                builtin.SortOrder,
                isActive: true,
                isBuiltin: true));
        }

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
            bool hasBuiltin = await context.TagRoleMappings.AsNoTracking()
                .AnyAsync(mapping => mapping.CollectionId == collectionId && mapping.IsBuiltin, cancellationToken);
            if (!hasBuiltin)
            {
                throw;
            }
        }
    }

    private sealed record BuiltinTagRoleMapping(string CreditRoleCode, string TagField, int SortOrder);
}
