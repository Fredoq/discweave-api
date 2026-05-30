namespace DiscWeave.Api.Features.Admin;

public sealed record CreateUserRequest
{
    public required string Email { get; init; }

    public required string Password { get; init; }
}
