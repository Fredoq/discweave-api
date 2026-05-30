using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DiscWeave.Infrastructure.Persistence;

public sealed class DiscWeaveDbContextDesignTimeFactory : IDesignTimeDbContextFactory<DiscWeaveDbContext>
{
    public DiscWeaveDbContext CreateDbContext(string[] args)
    {
        string? configuredConnectionString = Environment.GetEnvironmentVariable("DISCWEAVE_DESIGN_TIME_CONNECTION_STRING");
        string connectionString = string.IsNullOrWhiteSpace(configuredConnectionString)
            ? "Host=localhost;Database=discweave;Username=discweave"
            : configuredConnectionString;

        DbContextOptions<DiscWeaveDbContext> options = new DbContextOptionsBuilder<DiscWeaveDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new DiscWeaveDbContext(options);
    }
}
