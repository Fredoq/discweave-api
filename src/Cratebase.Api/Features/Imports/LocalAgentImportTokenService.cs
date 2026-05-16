using Cratebase.Domain.Imports;
using Cratebase.Domain.SharedKernel.Errors;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Infrastructure.Persistence;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace Cratebase.Api.Features.Imports;

public sealed class LocalAgentImportTokenService
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(5);

    public async Task<LocalAgentImportTokenIssue> CreateAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        string token = CreateToken();
        DateTimeOffset expiresAt = now.Add(TokenLifetime);
        var entity = LocalAgentImportToken.Create(
            collectionId,
            LocalAgentImportTokenId.New(),
            Hash(token),
            expiresAt,
            now);

        _ = context.LocalAgentImportTokens.Add(entity);
        IReadOnlyList<string> releaseTemplates = await ImportPatternDefaults.ActiveTemplatesAsync(
            context,
            collectionId,
            ImportPatternKind.ReleaseFolder,
            cancellationToken);
        IReadOnlyList<string> trackTemplates = await ImportPatternDefaults.ActiveTemplatesAsync(
            context,
            collectionId,
            ImportPatternKind.TrackFile,
            cancellationToken);
        _ = await context.SaveChangesAsync(cancellationToken);

        return new LocalAgentImportTokenIssue(token, expiresAt, releaseTemplates, trackTemplates);
    }

    public async Task<LocalAgentImportToken> UseAsync(
        CratebaseDbContext context,
        string token,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new DomainException("local_agent_import_token.required", "Local agent import token is required");
        }

        string tokenHash = Hash(token);
        LocalAgentImportToken entity = await context.LocalAgentImportTokens.SingleOrDefaultAsync(
            candidate => candidate.TokenHash == tokenHash,
            cancellationToken) ?? throw new DomainException("local_agent_import_token.invalid", "Local agent import token is invalid");

        entity.Use(DateTimeOffset.UtcNow);

        return entity;
    }

    private static string CreateToken()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(32);
        return WebEncoders.Base64UrlEncode(bytes);
    }

    private static string Hash(string token)
    {
        byte[] hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token.Trim()));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

public sealed record LocalAgentImportTokenIssue(
    string Token,
    DateTimeOffset ExpiresAt,
    IReadOnlyList<string> ReleaseFolderPatterns,
    IReadOnlyList<string> TrackFilePatterns);
