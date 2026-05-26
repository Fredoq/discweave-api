namespace Cratebase.Api.Features.Admin;

public sealed record InviteResponse(
    Guid Id,
    string Status,
    DateTimeOffset CreatedAt,
    Guid CreatedByUserId,
    string? Note,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? RevokedAt,
    Guid? RevokedByUserId,
    DateTimeOffset? RedeemedAt,
    Guid? RedeemedUserId,
    string? RedeemedEmail);
