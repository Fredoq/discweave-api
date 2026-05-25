namespace Cratebase.Api.Features.Admin;

public sealed record CreateInviteResponse(
    Guid Id,
    string Code,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    string? Note);
