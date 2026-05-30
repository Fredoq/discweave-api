namespace DiscWeave.Api.Features.Admin;

public sealed record CreateInviteRequest
{
    public DateTimeOffset? ExpiresAt { get; init; }

    public string? Note { get; init; }
}
