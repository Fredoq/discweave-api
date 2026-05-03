using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Cratebase.Infrastructure.Persistence;

public sealed class CratebaseDbContextDesignTimeFactory : IDesignTimeDbContextFactory<CratebaseDbContext>
{
    public CratebaseDbContext CreateDbContext(string[] args)
    {
        string? configuredConnectionString = Environment.GetEnvironmentVariable("CRATEBASE_DESIGN_TIME_CONNECTION_STRING");
        string connectionString = string.IsNullOrWhiteSpace(configuredConnectionString)
            ? "Host=localhost;Database=cratebase;Username=cratebase"
            : configuredConnectionString;

        DbContextOptions<CratebaseDbContext> options = new DbContextOptionsBuilder<CratebaseDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new CratebaseDbContext(options);
    }
}
