namespace Cratebase.Api.Features.CatalogGraph;

public sealed record CatalogGraphContextResponse(
    CatalogGraphContextResponse.EntityResponse Entity,
    CatalogGraphContextResponse.SectionsResponse Sections,
    IReadOnlyList<string> CollectorSignals)
{
    public sealed record EntityResponse(Guid Id, string Type, string Title, string? Subtitle, string? Summary);

    public sealed record LinkResponse(Guid Id, string Type, string Title, string? Subtitle, string Relation);

    public sealed record SectionsResponse(
        IReadOnlyList<LinkResponse> Artists,
        IReadOnlyList<LinkResponse> Releases,
        IReadOnlyList<LinkResponse> Tracks,
        IReadOnlyList<LinkResponse> OwnedCopies,
        IReadOnlyList<LinkResponse> Labels,
        IReadOnlyList<LinkResponse> Playlists,
        IReadOnlyList<LinkResponse> Credits,
        IReadOnlyList<LinkResponse> Relations,
        IReadOnlyList<LinkResponse> Media);
}
