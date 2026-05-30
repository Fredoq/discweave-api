namespace DiscWeave.Api.Features.Releases;

public sealed record ReleaseTrackRequest
{
    public Guid? TrackId { get; init; }

    public string? Title { get; init; }

    public int Position { get; init; }

    public int? DurationSeconds { get; init; }

    public IReadOnlyList<ReleaseArtistCreditRequest>? ArtistCredits { get; init; }

    public string? VersionNote { get; init; }
}
