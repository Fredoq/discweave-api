namespace Cratebase.Api.Features.Releases;

public sealed record ReleaseTrackRequest
{
    public string Title { get; init; } = string.Empty;

    public int Position { get; init; }

    public int? DurationSeconds { get; init; }

    public IReadOnlyList<ReleaseArtistCreditRequest>? ArtistCredits { get; init; }

    public string? VersionNote { get; init; }
}
