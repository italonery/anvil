using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Anvil.Messaging.Centrifugo;

public static class DependencyInjection
{
    public static IServiceCollection AddAnvilCentrifugo(this IServiceCollection services, Action<CentrifugoOptions> configureOptions)
    {
        services.Configure(configureOptions);
        services.TryAddSingleton<IRealtimePublisher, CentrifugoPublisher>();
        return services;
    }
}
