using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Anvil.Persistence.Postgres;

public static class DependencyInjection
{
    public static IServiceCollection AddAnvilPostgresPersistence(this IServiceCollection services)
    {
        services.TryAddSingleton<IEfDbContextFactory, EfDbContextFactory>();
        return services;
    }
}
