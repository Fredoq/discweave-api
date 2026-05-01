using Cratebase.Application.Persistence;
using Cratebase.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cratebase.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCratebaseInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        string connectionString = configuration.GetConnectionString("Cratebase")
            ?? throw new InvalidOperationException("Connection string 'Cratebase' is not configured");

        _ = services.AddDbContext<CratebaseDbContext>(options =>
        {
            _ = options.UseNpgsql(connectionString);
        });
        _ = services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<CratebaseDbContext>());

        return services;
    }
}
