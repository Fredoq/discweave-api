namespace DiscWeave.Infrastructure.Identity;

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

        if (id == Guid.Empty)
        {
            throw new ArgumentException("Invite ID cannot be empty.", nameof(id));
        }

        if (createdByUserId == Guid.Empty)
        {
            throw new ArgumentException("Creator user ID cannot be empty.", nameof(createdByUserId));
        }

        if (expiresAt <= now)
        {
            throw new ArgumentException("Invite expiration must be in the future.", nameof(expiresAt));
        }

        string? normalizedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

        return new Invite
        {
            Id = id,
            CodeHash = codeHash,
            CreatedAt = now,
            CreatedByUserId = createdByUserId,
            Note = normalizedNote,
            ExpiresAt = expiresAt
        };
    }

    public bool IsAvailable(DateTimeOffset now)
    {
        return RevokedAt is null && RedeemedAt is null && ExpiresAt > now;
    }

    public string Status(DateTimeOffset now)
    {
        return (RedeemedAt, RevokedAt, ExpiresAt <= now) switch
        {
            ({ }, _, _) => "redeemed",
            (_, { }, _) => "revoked",
            (_, _, true) => "expired",
            _ => "available"
        };
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
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("Redeemed user ID cannot be empty.", nameof(userId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        if (!IsAvailable(now))
        {
            throw new InvalidOperationException("Invite is not available for redemption.");
        }

        RedeemedAt = now;
        RedeemedUserId = userId;
        RedeemedEmail = email.Trim();
    }
}
