using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Anvil.Scheduling.Quartz;

public static class DependencyInjection
{
    public static IServiceCollection AddAnvilQuartzScheduling(this IServiceCollection services)
    {
        services.TryAddSingleton<QuartzSchedulerManager>();
        services.TryAddTransient(typeof(RetryableJob<>));
        return services;
    }
}
