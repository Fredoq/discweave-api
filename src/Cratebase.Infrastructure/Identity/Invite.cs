namespace Cratebase.Infrastructure.Identity;

public sealed class Invite
{
    public Guid Id { get; private set; }

    public string CodeHash { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public string? Note { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public Guid? RevokedByUserId { get; private set; }

    public DateTimeOffset? RedeemedAt { get; private set; }

    public Guid? RedeemedUserId { get; private set; }

    public string? RedeemedEmail { get; private set; }

    public static Invite Create(Guid id, string codeHash, Guid createdByUserId, string? note, DateTimeOffset expiresAt, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codeHash);

        return id == Guid.Empty
            ? throw new ArgumentException("Invite ID cannot be empty.", nameof(id))
            : createdByUserId == Guid.Empty
            ? throw new ArgumentException("Creator user ID cannot be empty.", nameof(createdByUserId))
            : expiresAt <= now
            ? throw new ArgumentException("Invite expiration must be in the future.", nameof(expiresAt))
            : new Invite
            {
                Id = id,
                CodeHash = codeHash,
                CreatedAt = now,
                CreatedByUserId = createdByUserId,
                Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
                ExpiresAt = expiresAt
            };
    }

    public bool IsAvailable(DateTimeOffset now)
    {
        return RevokedAt is null && RedeemedAt is null && ExpiresAt > now;
    }

    public string Status(DateTimeOffset now)
    {
        return RedeemedAt is not null
            ? "redeemed"
            : RevokedAt is not null
                ? "revoked"
                : ExpiresAt <= now ? "expired" : "available";
    }

    public bool TryRevoke(Guid revokedByUserId, DateTimeOffset now)
    {
        if (RedeemedAt is not null)
        {
            return false;
        }

        RevokedAt ??= now;
        RevokedByUserId ??= revokedByUserId;

        return true;
    }

    public void Redeem(Guid userId, string email, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        RedeemedAt = now;
        RedeemedUserId = userId;
        RedeemedEmail = email.Trim();
    }
}
