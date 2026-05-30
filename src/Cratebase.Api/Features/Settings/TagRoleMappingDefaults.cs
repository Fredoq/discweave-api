using Cratebase.Domain.Settings;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Api.Features.Settings;

internal static class TagRoleMappingDefaults
{
    private static readonly BuiltinTagRoleMapping[] Builtins =
    [
        new("remixer", "remixer", 30),
        new("producer", "producer", 40),
        new("composer", "composer", 50)
    ];

    public static async Task EnsureAsync(CratebaseDbContext context, CollectionId collectionId, CancellationToken cancellationToken)
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

        _ = await context.SaveChangesAsync(cancellationToken);
    }

    private sealed record BuiltinTagRoleMapping(string CreditRoleCode, string TagField, int SortOrder);
}
