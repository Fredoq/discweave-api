namespace DiscWeave.Api.Features.Artists;

public sealed record ArtistListResponse(IReadOnlyList<ArtistResponse> Items, int Limit, int Offset, int Total);
