using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Anvil.Http.Resilience;

public static class DependencyInjection
{
    public static IServiceCollection AddAnvilHttpResilience(this IServiceCollection services)
    {
        services.TryAddSingleton<IHttpClientPoolFactory, HttpClientPoolFactory>();
        services.TryAddSingleton<RestClient>();
        return services;
    }
}
