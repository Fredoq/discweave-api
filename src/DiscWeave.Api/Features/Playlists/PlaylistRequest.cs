namespace DiscWeave.Api.Features.Playlists;

public sealed record PlaylistRequest
{
    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string Type { get; init; } = "manual";

    public IReadOnlyList<PlaylistEntryRequest> Entries { get; init; } = [];

    public SmartPlaylistRulesRequest? Rules { get; init; }
}
