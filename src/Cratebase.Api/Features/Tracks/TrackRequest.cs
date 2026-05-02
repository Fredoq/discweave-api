namespace Cratebase.Api.Features.Tracks;

public sealed record TrackRequest(string Title, int? DurationSeconds, IReadOnlyList<string>? Genres, IReadOnlyList<string>? Tags);
