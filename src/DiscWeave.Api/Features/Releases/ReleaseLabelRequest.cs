namespace DiscWeave.Api.Features.Releases;

public sealed record ReleaseLabelRequest
{
    public Guid? LabelId { get; init; }

    public string? Name { get; init; }

    public string? CatalogNumber { get; init; }

    public bool HasNoCatalogNumber { get; init; }
}
