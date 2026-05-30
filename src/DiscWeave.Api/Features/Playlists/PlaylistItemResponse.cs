namespace DiscWeave.Api.Features.Playlists;

public sealed record PlaylistItemResponse
{
    public PlaylistItemResponse(string kind, Guid id, string title, string? subtitle)
    {
        Kind = kind;
        Id = id;
        Title = title;
        Subtitle = subtitle;
    }

    public string Kind { get; }

    public Guid Id { get; }

    public string Title { get; }

    public string? Subtitle { get; }
}
