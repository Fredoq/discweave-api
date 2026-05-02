namespace Cratebase.Api.Features.Tracks;

public sealed record TrackResponse(Guid Id, string Title, int? DurationSeconds, IReadOnlyList<string> Genres, IReadOnlyList<string> Tags);
