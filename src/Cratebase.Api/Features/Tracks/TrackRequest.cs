namespace Cratebase.Api.Features.Tracks;

public sealed record TrackRequest
{
    public string Title { get; init; } = string.Empty;

    public int? DurationSeconds { get; init; }

    public IReadOnlyList<string> Genres { get; init; } = [];

    public IReadOnlyList<string> Tags { get; init; } = [];
}
