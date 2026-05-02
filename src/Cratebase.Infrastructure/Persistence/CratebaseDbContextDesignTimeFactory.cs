using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Cratebase.Infrastructure.Persistence;

public sealed class CratebaseDbContextDesignTimeFactory : IDesignTimeDbContextFactory<CratebaseDbContext>
{
    public CratebaseDbContext CreateDbContext(string[] args)
    {
        DbContextOptions<CratebaseDbContext> options = new DbContextOptionsBuilder<CratebaseDbContext>()
            .UseNpgsql("Host=localhost;Database=cratebase;Username=cratebase;Password=cratebase")
            .Options;

        return new CratebaseDbContext(options);
    }
}
