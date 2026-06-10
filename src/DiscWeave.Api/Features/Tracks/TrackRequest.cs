using DiscWeave.Api.Features.ExternalSources;

namespace DiscWeave.Api.Features.Tracks;

public sealed record TrackRequest
{
    public string Title { get; init; } = string.Empty;

    public int? DurationSeconds { get; init; }

    public IReadOnlyList<string> Genres { get; init; } = [];

    public IReadOnlyList<string> Tags { get; init; } = [];

    public IReadOnlyList<ExternalSourceReferenceRequest>? ExternalSources { get; init; }

    public IReadOnlyList<TrackCreditRequest> Credits { get; init; } = [];

    public IReadOnlyList<TrackReleaseAppearanceRequest> ReleaseAppearances { get; init; } = [];
}

public sealed record TrackReleaseAppearanceRequest(Guid ReleaseId, int Position, string? Disc, string? Side, string? VersionNote);
