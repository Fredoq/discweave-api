namespace DiscWeave.Api.Features.Playlists;

public sealed record PlaylistResponse
{
    public PlaylistResponse(
        Guid id,
        string name,
        string? description,
        string type,
        SmartPlaylistRulesResponse rules,
        IReadOnlyList<PlaylistItemResponse> entries,
        IReadOnlyList<PlaylistItemResponse> results)
    {
        Id = id;
        Name = name;
        Description = description;
        Type = type;
        Rules = rules;
        Entries = entries;
        Results = results;
    }

    public Guid Id { get; }

    public string Name { get; }

    public string? Description { get; }

    public string Type { get; }

    public SmartPlaylistRulesResponse Rules { get; }

    public IReadOnlyList<PlaylistItemResponse> Entries { get; }

    public IReadOnlyList<PlaylistItemResponse> Results { get; }
}
