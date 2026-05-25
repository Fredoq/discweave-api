using Cratebase.Application.Errors;
using Cratebase.Infrastructure.Identity;
using Cratebase.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Infrastructure.Tests;

public sealed class InviteSchemaTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _postgres;

    public InviteSchemaTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact(DisplayName = "Invite schema stores audit metadata without plaintext codes")]
    public async Task Invite_schema_stores_audit_metadata_without_plaintext_codes()
    {
        await using CratebaseDbContext context = await CreateMigratedContextAsync();

        string[] columns = [.. await ReadColumnNamesAsync(context, "invites")];
        string[] indexes = [.. await ReadIndexNamesAsync(context, "invites")];

        Assert.Contains("invite_id", columns);
        Assert.Contains("code_hash", columns);
        Assert.Contains("created_at", columns);
        Assert.Contains("created_by_user_id", columns);
        Assert.Contains("note", columns);
        Assert.Contains("expires_at", columns);
        Assert.Contains("revoked_at", columns);
        Assert.Contains("revoked_by_user_id", columns);
        Assert.Contains("redeemed_at", columns);
        Assert.Contains("redeemed_user_id", columns);
        Assert.Contains("redeemed_email", columns);
        Assert.DoesNotContain("code", columns);
        Assert.DoesNotContain("plain_code", columns);
        Assert.Contains("ix_invites_code_hash", indexes);
    }

    [Fact(DisplayName = "Invite code hashes are unique")]
    public async Task Invite_code_hashes_are_unique()
    {
        await using CratebaseDbContext context = await CreateMigratedContextAsync();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        const string codeHash = "duplicate-hash";

        _ = context.Invites.Add(Invite.Create(Guid.CreateVersion7(), codeHash, Guid.CreateVersion7(), null, now.AddDays(30), now));
        _ = context.Invites.Add(Invite.Create(Guid.CreateVersion7(), codeHash, Guid.CreateVersion7(), null, now.AddDays(30), now));

        ResourceConflictException exception = await Assert.ThrowsAsync<ResourceConflictException>(() => context.SaveChangesAsync());
        Assert.Equal(ResourceConflictException.IntegrityConstraint, exception.Conflict);
    }

    [Fact(DisplayName = "Invite validates creation and redemption inputs")]
    public void Invite_validates_creation_and_redemption_inputs()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DateTimeOffset expiresAt = now.AddDays(1);

        _ = Assert.Throws<ArgumentException>(() => Invite.Create(Guid.Empty, "hash", Guid.CreateVersion7(), null, expiresAt, now));
        _ = Assert.Throws<ArgumentException>(() => Invite.Create(Guid.CreateVersion7(), "hash", Guid.Empty, null, expiresAt, now));
        _ = Assert.Throws<ArgumentException>(() => Invite.Create(Guid.CreateVersion7(), "hash", Guid.CreateVersion7(), null, now, now));

        var invite = Invite.Create(Guid.CreateVersion7(), "hash", Guid.CreateVersion7(), null, expiresAt, now);
        _ = Assert.Throws<ArgumentException>(() => invite.Redeem(Guid.CreateVersion7(), string.Empty, now));
    }

    private static async Task<IReadOnlyList<string>> ReadColumnNamesAsync(CratebaseDbContext context, string tableName)
    {
        FormattableString sql = $"""
            SELECT column_name
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name = {tableName}
            ORDER BY ordinal_position
            """;

        return await context.Database.SqlQuery<string>(sql).ToArrayAsync();
    }

    private static async Task<IReadOnlyList<string>> ReadIndexNamesAsync(CratebaseDbContext context, string tableName)
    {
        FormattableString sql = $"""
            SELECT indexname
            FROM pg_indexes
            WHERE schemaname = 'public'
              AND tablename = {tableName}
            ORDER BY indexname
            """;

        return await context.Database.SqlQuery<string>(sql).ToArrayAsync();
    }

    private async Task<CratebaseDbContext> CreateMigratedContextAsync()
    {
        string connectionString = await _postgres.CreateDatabaseAsync();
        CratebaseDbContext context = new(CreateOptions(connectionString));
        await context.Database.MigrateAsync();

        return context;
    }

    private static DbContextOptions<CratebaseDbContext> CreateOptions(string connectionString)
    {
        return new DbContextOptionsBuilder<CratebaseDbContext>()
            .UseNpgsql(connectionString)
            .Options;
    }
}
