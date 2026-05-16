namespace Cratebase.Domain.Imports;

public sealed record ReleaseImportLabel(Guid? LabelId, string Name, string? CatalogNumber, bool HasNoCatalogNumber);
