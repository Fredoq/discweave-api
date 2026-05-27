using Cratebase.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Infrastructure.Tests;

public sealed class SearchSchemaTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _postgres;

    public SearchSchemaTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact(DisplayName = "Search schema has trigram indexes for filter facets")]
    public async Task Search_schema_has_trigram_indexes_for_filter_facets()
    {
        await using CratebaseDbContext context = await CreateMigratedContextAsync();

        string[] indexes = [.. await ReadIndexNamesAsync(context, "search_documents")];

        Assert.Contains("ix_search_documents_role_facet_trgm", indexes);
        Assert.Contains("ix_search_documents_media_facet_trgm", indexes);
        Assert.Contains("ix_search_documents_status_facet_trgm", indexes);
        Assert.Contains("ix_search_documents_tag_facet_trgm", indexes);
        Assert.Contains("ix_search_documents_label_id_facet_trgm", indexes);
        Assert.Contains("ix_search_documents_collector_signal_facet_trgm", indexes);
    }

    private static async Task<IReadOnlyList<string>> ReadIndexNamesAsync(CratebaseDbContext context, string tableName)
    {
        FormattableString sql = $"""
            SELECT indexname AS "Value"
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
