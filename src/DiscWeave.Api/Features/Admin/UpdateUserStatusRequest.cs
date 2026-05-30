namespace DiscWeave.Api.Features.Admin;

public sealed record UpdateUserStatusRequest
{
    public required bool IsDisabled { get; init; }
}
