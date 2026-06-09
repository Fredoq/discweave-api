namespace DiscWeave.Api.Features.Tracks;

public sealed record TrackCreditRequest
{
    public Guid? ArtistId { get; init; }

    public string? Name { get; init; }

    public string? Role { get; init; }

    public IReadOnlyList<string>? Roles { get; init; }
}
