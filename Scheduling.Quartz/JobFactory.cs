using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Quartz.Spi;

namespace Anvil.Scheduling.Quartz;

public sealed class JobFactory : IJobFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ConcurrentDictionary<IJob, IServiceScope> _scopes = new();

    public JobFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IJob NewJob(TriggerFiredBundle bundle, IScheduler scheduler)
    {
        var scope = _serviceProvider.CreateScope();
        try
        {
            var job = (IJob)scope.ServiceProvider.GetRequiredService(bundle.JobDetail.JobType);
            _scopes[job] = scope;
            return job;
        }
        catch
        {
            scope.Dispose();
            throw;
        }
    }

    public void ReturnJob(IJob job)
    {
        if (_scopes.TryRemove(job, out var scope))
        {
            scope.Dispose();
        }
    }
}
