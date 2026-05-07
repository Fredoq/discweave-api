using Cratebase.Domain.SharedKernel.Errors;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Domain.SharedKernel.Optional;

namespace Cratebase.Domain.Catalog;

public sealed class ReleaseLabel
{
    private ReleaseLabel()
    {
        CatalogNumber = Optional.Missing<string>();
    }

    private ReleaseLabel(LabelId labelId, IOptionalValue<string> catalogNumber, bool hasNoCatalogNumber)
    {
        if (catalogNumber.HasValue && hasNoCatalogNumber)
        {
            throw new DomainException("release_label.catalog_number_conflict", "Release label cannot have a catalog number and be marked as no catalog number");
        }

        LabelId = labelId;
        CatalogNumber = catalogNumber;
        HasNoCatalogNumber = hasNoCatalogNumber;
    }

    public LabelId LabelId { get; private set; }

    public IOptionalValue<string> CatalogNumber { get; private set; }

    public bool HasNoCatalogNumber { get; private set; }

    public static ReleaseLabel Create(LabelId labelId, IOptionalValue<string> catalogNumber, bool hasNoCatalogNumber)
    {
        ArgumentNullException.ThrowIfNull(catalogNumber);

        return new ReleaseLabel(labelId, catalogNumber, hasNoCatalogNumber);
    }
}
