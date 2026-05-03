namespace Cratebase.Api.Features.Credits;

public sealed class CreditListRequest
{
    public Guid? ContributorArtistId { get; init; }

    public string? TargetType { get; init; }

    public Guid? TargetId { get; init; }

    public string? Role { get; init; }

    public int? Limit { get; init; }

    public int? Offset { get; init; }
}
