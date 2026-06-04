namespace DiscWeave.Api.Features.Credits;

public sealed record CreditRequest
{
    public Guid ContributorArtistId { get; init; }

    public string TargetType { get; init; } = string.Empty;

    public Guid TargetId { get; init; }

    public IReadOnlyList<string> Roles { get; init; } = [];
}
