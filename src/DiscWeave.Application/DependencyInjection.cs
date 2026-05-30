using Microsoft.Extensions.DependencyInjection;

namespace DiscWeave.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddDiscWeaveApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services;
    }
}
