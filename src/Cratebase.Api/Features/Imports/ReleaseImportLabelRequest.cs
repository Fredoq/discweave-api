namespace Cratebase.Api.Features.Imports;

public sealed record ReleaseImportLabelRequest(Guid? LabelId, string? Name, string? CatalogNumber, bool HasNoCatalogNumber);
