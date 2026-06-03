namespace DiscWeave.Api.Features.Releases;

public sealed record ReleaseArtistCreditRequest
{
    public Guid? ArtistId { get; init; }

    public string? Name { get; init; }

    public string? Role { get; init; }

    public IReadOnlyList<string>? Roles { get; init; }
}
