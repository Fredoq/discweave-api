namespace DiscWeave.Api.Features.Playlists;

public sealed record PlaylistEntryRequest
{
    public string Kind { get; init; } = string.Empty;

    public Guid Id { get; init; }
}
