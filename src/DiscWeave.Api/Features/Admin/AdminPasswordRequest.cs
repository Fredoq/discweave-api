namespace DiscWeave.Api.Features.Admin;

public sealed record AdminPasswordRequest
{
    public required string TemporaryPassword { get; init; }
}
