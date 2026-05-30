using DiscWeave.Domain.SharedKernel.Errors;
using DiscWeave.Domain.SharedKernel.Ids;
using DiscWeave.Domain.SharedKernel.Interfaces;
using DiscWeave.Domain.SharedKernel.Validation;

namespace DiscWeave.Domain.Settings;

public sealed class TagRoleMapping : IEntity<TagRoleMappingId>
{
    private TagRoleMapping()
    {
    }

    private TagRoleMapping(
        CollectionId collectionId,
        TagRoleMappingId id,
        string creditRoleCode,
        string tagField,
        int sortOrder,
        bool isActive,
        bool isBuiltin)
    {
        CollectionId = collectionId;
        Id = id;
        CreditRoleCode = Guard.RequiredText(creditRoleCode, nameof(creditRoleCode), "tag_role_mapping.role_required");
        TagField = ValidateTagField(tagField);
        SortOrder = RequiredSortOrder(sortOrder);
        IsActive = isActive;
        IsBuiltin = isBuiltin;
    }

    public TagRoleMappingId Id { get; private set; }

    public CollectionId CollectionId { get; private set; }

    public string CreditRoleCode { get; private set; } = string.Empty;

    public string TagField { get; private set; } = string.Empty;

    public int SortOrder { get; private set; }

    public bool IsActive { get; private set; }

    public bool IsBuiltin { get; private set; }

    public static TagRoleMapping Create(
        CollectionId collectionId,
        TagRoleMappingId id,
        string creditRoleCode,
        string tagField,
        int sortOrder,
        bool isActive,
        bool isBuiltin)
    {
        return new TagRoleMapping(
            collectionId,
            id,
            creditRoleCode,
            tagField,
            sortOrder,
            isActive,
            isBuiltin);
    }

    public void Update(
        string creditRoleCode,
        string tagField,
        int sortOrder,
        bool isActive)
    {
        CreditRoleCode = Guard.RequiredText(creditRoleCode, nameof(creditRoleCode), "tag_role_mapping.role_required");
        TagField = ValidateTagField(tagField);
        SortOrder = RequiredSortOrder(sortOrder);
        IsActive = isActive;
    }

    public void EnsureCanDelete()
    {
        if (IsBuiltin)
        {
            throw new DomainException("tag_role_mapping.builtin_immutable", "Built-in tag role mappings cannot be deleted");
        }
    }

    private static string ValidateTagField(string tagField)
    {
        string normalized = Guard.RequiredText(tagField, nameof(tagField), "tag_role_mapping.tag_field_required");
        if (normalized.Length > 64 || normalized.Any(char.IsControl) || normalized.Any(char.IsWhiteSpace))
        {
            throw new DomainException("tag_role_mapping.tag_field_invalid", "Tag field is invalid");
        }

        bool isValidTagField = normalized.All(static character =>
            char.IsAsciiLetterOrDigit(character) ||
            character is '_' or '-' or ':' or '.');

        return isValidTagField
            ? normalized
            : throw new DomainException("tag_role_mapping.tag_field_invalid", "Tag field is invalid");
    }

    private static int RequiredSortOrder(int sortOrder)
    {
        return sortOrder < 0
            ? throw new DomainException("tag_role_mapping.sort_order_invalid", "Tag role mapping sort order cannot be negative")
            : sortOrder;
    }
}
