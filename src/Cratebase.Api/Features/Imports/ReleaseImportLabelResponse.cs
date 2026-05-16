namespace Cratebase.Api.Features.Imports;

public sealed record ReleaseImportLabelResponse(Guid? LabelId, string Name, string? CatalogNumber, bool HasNoCatalogNumber);
