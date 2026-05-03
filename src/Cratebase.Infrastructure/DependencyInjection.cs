using Cratebase.Application.Catalog.Artists;
using Cratebase.Application.Persistence;
using Cratebase.Application.Search;
using Cratebase.Infrastructure.Identity;
using Cratebase.Infrastructure.Persistence;
using Cratebase.Infrastructure.Persistence.Queries;
using Microsoft.AspNetCore.Identity;
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

        string? configuredConnectionString = configuration.GetConnectionString("Cratebase");
        if (string.IsNullOrWhiteSpace(configuredConnectionString))
        {
            throw new InvalidOperationException("Connection string 'Cratebase' is not configured");
        }

        _ = services.AddDbContext<CratebaseDbContext>(options =>
        {
            _ = options.UseNpgsql(configuredConnectionString);
        });
        _ = services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<CratebaseDbContext>());
        _ = services.AddScoped<IArtistQueries, ArtistQueries>();
        _ = services.AddScoped<ICollectionSearchQueries, CollectionSearchQueries>();
        _ = services.AddIdentityCore<CratebaseUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<CratebaseDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();
        _ = services.AddScoped<IUserClaimsPrincipalFactory<CratebaseUser>, CratebaseUserClaimsPrincipalFactory>();

        return services;
    }
}
