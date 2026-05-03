namespace Cratebase.Api.Features.Admin;

public sealed record UpdateUserStatusRequest
{
    public bool IsDisabled { get; init; }
}
