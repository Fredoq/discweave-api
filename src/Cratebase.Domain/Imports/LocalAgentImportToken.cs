using Cratebase.Domain.SharedKernel.Errors;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Domain.SharedKernel.Interfaces;
using Cratebase.Domain.SharedKernel.Validation;

namespace Cratebase.Domain.Imports;

public sealed class LocalAgentImportToken : IEntity<LocalAgentImportTokenId>
{
    private LocalAgentImportToken()
    {
        TokenHash = string.Empty;
    }

    private LocalAgentImportToken(
        CollectionId collectionId,
        LocalAgentImportTokenId id,
        string tokenHash,
        DateTimeOffset expiresAt,
        DateTimeOffset createdAt)
    {
        CollectionId = collectionId;
        Id = id;
        TokenHash = Guard.RequiredText(tokenHash, nameof(tokenHash), "local_agent_import_token.hash_required");
        ExpiresAt = expiresAt;
        CreatedAt = createdAt;
    }

    public CollectionId CollectionId { get; private set; }

    public LocalAgentImportTokenId Id { get; private set; }

    public string TokenHash { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? UsedAt { get; private set; }

    public static LocalAgentImportToken Create(CollectionId collectionId, LocalAgentImportTokenId id, string tokenHash, DateTimeOffset expiresAt, DateTimeOffset createdAt)
    {
        return expiresAt <= createdAt
            ? throw new DomainException("local_agent_import_token.expiry_invalid", "Local agent import token expiry must be in the future")
            : new LocalAgentImportToken(collectionId, id, tokenHash, expiresAt, createdAt);
    }

    public void Use(DateTimeOffset usedAt)
    {
        if (UsedAt is not null)
        {
            throw new DomainException("local_agent_import_token.used", "Local agent import token was already used");
        }

        if (usedAt > ExpiresAt)
        {
            throw new DomainException("local_agent_import_token.expired", "Local agent import token has expired");
        }

        UsedAt = usedAt;
    }
}
