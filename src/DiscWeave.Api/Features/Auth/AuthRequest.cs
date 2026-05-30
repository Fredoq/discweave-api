namespace DiscWeave.Api.Features.Auth;

public sealed record AuthRequest
{
    public required string Email { get; init; }

    public required string Password { get; init; }

    public string? InviteCode { get; init; }
}
