using DiscWeave.Domain.SharedKernel.Errors;
using DiscWeave.Domain.SharedKernel.Ids;
using DiscWeave.Domain.SharedKernel.Interfaces;
using DiscWeave.Domain.SharedKernel.Validation;

namespace DiscWeave.Domain.Imports;

public sealed class ImportPattern : IEntity<ImportPatternId>
{
    private ImportPattern()
    {
        Template = string.Empty;
    }

    private ImportPattern(
        CollectionId collectionId,
        ImportPatternId id,
        ImportPatternKind kind,
        string template,
        int sortOrder,
        bool isBuiltin)
    {
        CollectionId = collectionId;
        Id = id;
        Kind = kind;
        Template = Guard.RequiredText(template, nameof(template), "import_pattern.template_required");
        SortOrder = RequiredSortOrder(sortOrder);
        IsActive = true;
        IsBuiltin = isBuiltin;
    }

    public CollectionId CollectionId { get; private set; }

    public ImportPatternId Id { get; private set; }

    public ImportPatternKind Kind { get; private set; }

    public string Template { get; private set; }

    public int SortOrder { get; private set; }

    public bool IsActive { get; private set; }

    public bool IsBuiltin { get; private set; }

    public static ImportPattern Create(CollectionId collectionId, ImportPatternId id, ImportPatternKind kind, string template, int sortOrder, bool isBuiltin)
    {
        return new ImportPattern(collectionId, id, kind, template, sortOrder, isBuiltin);
    }

    public void Update(string template, int sortOrder, bool isActive)
    {
        if (IsBuiltin)
        {
            throw new DomainException("import_pattern.builtin_immutable", "Built-in import patterns cannot be edited");
        }

        Template = Guard.RequiredText(template, nameof(template), "import_pattern.template_required");
        SortOrder = RequiredSortOrder(sortOrder);
        IsActive = isActive;
    }

    private static int RequiredSortOrder(int sortOrder)
    {
        return sortOrder < 0
            ? throw new DomainException("import_pattern.sort_order_invalid", "Import pattern sort order cannot be negative")
            : sortOrder;
    }
}
