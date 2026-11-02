using System.Collections.Specialized;
using Anvil.Primitives;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using Quartz;
using Quartz.Impl;

namespace Anvil.Scheduling.Quartz;

public sealed class QuartzSchedulerManager
{
    private readonly IConfiguration _config;
    private readonly ILogger<QuartzSchedulerManager> _logger;

    public QuartzSchedulerManager(IConfiguration config, ILogger<QuartzSchedulerManager> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task<IScheduler> StartAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        var instanceName = _config["Quartz:InstanceName"] ?? throw new ArgumentNullException(nameof(_config), "Quartz:InstanceName configuration is required.");
        var connectionString = _config["ConnectionStrings:QuartzDbContext"] ?? throw new ArgumentNullException(nameof(_config), "ConnectionStrings:QuartzDbContext configuration is required.");

        await using (var conn = new NpgsqlConnection(connectionString))
        {
            await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        var props = new NameValueCollection
        {
            { "quartz.jobStore.type", "Quartz.Impl.AdoJobStore.JobStoreTX, Quartz" },
            { "quartz.serializer.type", "json" },
            { "quartz.jobStore.dataSource", "default" },
            { "quartz.jobStore.tablePrefix", string.Empty },
            { "quartz.threadPool.threadCount", _config["Quartz:ThreadCount"] ?? "20" },
            { "quartz.dataSource.default.provider", "Npgsql" },
            { "quartz.jobStore.driverDelegateType", "Quartz.Impl.AdoJobStore.PostgreSQLDelegate, Quartz" },
            { "quartz.scheduler.instanceName", instanceName },
            { "quartz.dataSource.default.connectionString", connectionString }
        };

        var factory = new StdSchedulerFactory(props);
        var scheduler = await factory.GetScheduler(cancellationToken).ConfigureAwait(false);
        scheduler.JobFactory = new JobFactory(serviceProvider);
        await scheduler.Start(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Quartz scheduler '{InstanceName}' started successfully", instanceName);
        return scheduler;
    }

    public async Task<bool> CheckJobExistsAsync(IScheduler scheduler, string jobName, string jobGroup = "default")
    {
        var jobKey = new JobKey(jobName, jobGroup);
        return await scheduler.CheckExists(jobKey).ConfigureAwait(false);
    }

    public async Task<Result<JobKey>> ScheduleJobAsync<TJob>(IScheduler scheduler, string jobGroup = "default", JobDataMap? jobDataMap = null, int intervalSeconds = 2) where TJob : IJob
    {
        var jobName = typeof(TJob).Name;
        var jobKey = new JobKey(jobName, jobGroup);
        if (!await scheduler.CheckExists(jobKey).ConfigureAwait(false))
        {
            try
            {
                var job = JobBuilder.Create<TJob>()
                    .DisallowConcurrentExecution()
                    .WithIdentity(jobKey)
                    .Build();

                var trigger = TriggerBuilder.Create()
                    .WithIdentity(jobName, jobGroup)
                    .UsingJobData(jobDataMap ?? new JobDataMap())
                    .StartNow()
                    .WithSimpleSchedule(x => x
                        .WithIntervalInSeconds(intervalSeconds)
                        .RepeatForever())
                    .Build();

                await scheduler.ScheduleJob(job, trigger).ConfigureAwait(false);
                return Result.Success(jobKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scheduling job '{JobName}'", jobName);
                return Result.Failure<JobKey>(Error.Failure(ErrorCode.SchedulerError, ex.Message));
            }
        }
        else
        {
            var triggerError = await IsJobTriggerInErrorAsync(scheduler, jobName, jobGroup).ConfigureAwait(false);
            if (triggerError.IsSuccess && triggerError.Data)
            {
                await ReactivateJobTriggersAsync(scheduler, jobName, jobGroup).ConfigureAwait(false);
                return Result.Success(jobKey);
            }
            else
            {
                return Result.Failure<JobKey>(Error.Conflict($"Job '{jobName}' already exists.", ErrorCode.AlreadyExists));
            }
        }
    }

    public async Task<Result<JobKey>> ScheduleJobWithCronAsync<TJob>(IScheduler scheduler, string cronExpression, string jobGroup = "default", JobDataMap? jobDataMap = null) where TJob : IJob
    {
        var jobName = typeof(TJob).Name;
        var jobKey = new JobKey(jobName, jobGroup);
        if (!await scheduler.CheckExists(jobKey).ConfigureAwait(false))
        {
            try
            {
                var job = JobBuilder.Create<TJob>()
                    .DisallowConcurrentExecution()
                    .WithIdentity(jobKey)
                    .Build();

                var trigger = TriggerBuilder.Create()
                    .WithIdentity(jobName, jobGroup)
                    .UsingJobData(jobDataMap ?? new JobDataMap())
                    .WithCronSchedule(cronExpression)
                    .Build();

                await scheduler.ScheduleJob(job, trigger).ConfigureAwait(false);
                return Result.Success(jobKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scheduling CRON job '{JobName}'", jobName);
                return Result.Failure<JobKey>(Error.Failure(ErrorCode.SchedulerError, ex.Message));
            }
        }
        else
        {
            return Result.Failure<JobKey>(Error.Conflict($"Job '{jobName}' already exists.", ErrorCode.AlreadyExists));
        }
    }

    public async Task<Result<JobKey>> ScheduleRetryableJobAsync<TRetryableJob>(
        IScheduler scheduler,
        string jobName,
        string jobGroup = "default",
        int retryIntervalSeconds = 2,
        int maxExecutionWindowSeconds = 300,
        JobDataMap? jobDataMap = null)
        where TRetryableJob : class, IRetryableJob
    {
        var jobKey = new JobKey(jobName, jobGroup);
        if (await scheduler.CheckExists(jobKey).ConfigureAwait(false))
        {
            return Result.Failure<JobKey>(Error.Conflict($"Job '{jobName}' already exists.", ErrorCode.AlreadyExists));
        }

        try
        {
            var dataMap = jobDataMap is not null ? new JobDataMap((IDictionary<string, object>)jobDataMap) : new JobDataMap();
            dataMap[RetryableJobDefaults.MaxWindowSecondsKey] = maxExecutionWindowSeconds;

            var job = JobBuilder.Create<RetryableJob<TRetryableJob>>()
                .WithIdentity(jobKey)
                .UsingJobData(dataMap)
                .Build();

            var trigger = TriggerBuilder.Create()
                .WithIdentity(jobName, jobGroup)
                .UsingJobData(dataMap)
                .StartNow()
                .WithSimpleSchedule(x => x
                    .WithIntervalInSeconds(retryIntervalSeconds)
                    .RepeatForever())
                .Build();

            await scheduler.ScheduleJob(job, trigger).ConfigureAwait(false);
            return Result.Success(jobKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scheduling retryable job '{JobName}'", jobName);
            return Result.Failure<JobKey>(Error.Failure(ErrorCode.SchedulerError, ex.Message));
        }
    }

    public async Task<Result<bool>> IsJobTriggerInErrorAsync(IScheduler scheduler, string jobName, string jobGroup)
    {
        try
        {
            var jobKey = new JobKey(jobName, jobGroup);
            if (!await scheduler.CheckExists(jobKey).ConfigureAwait(false))
            {
                return Result.Failure<bool>(Error.NotFound($"Job '{jobName}'/'{jobGroup}' not found."));
            }

            var triggers = await scheduler.GetTriggersOfJob(jobKey).ConfigureAwait(false);
            foreach (var trigger in triggers)
            {
                var state = await scheduler.GetTriggerState(trigger.Key).ConfigureAwait(false);
                if (state == TriggerState.Error)
                {
                    return Result.Success(true);
                }
            }

            return Result.Success(false);
        }
        catch (Exception ex)
        {
            return Result.Failure<bool>(Error.Failure(ErrorCode.SchedulerError, ex.Message));
        }
    }

    public async Task<Result<ResultNone>> ReactivateJobTriggersAsync(IScheduler scheduler, string jobName, string jobGroup)
    {
        try
        {
            var jobKey = new JobKey(jobName, jobGroup);
            if (!await scheduler.CheckExists(jobKey).ConfigureAwait(false))
            {
                return Result.Failure<ResultNone>(Error.NotFound($"Job '{jobName}'/'{jobGroup}' not found."));
            }

            var triggers = await scheduler.GetTriggersOfJob(jobKey).ConfigureAwait(false);
            foreach (var trigger in triggers)
            {
                var state = await scheduler.GetTriggerState(trigger.Key).ConfigureAwait(false);
                if (state == TriggerState.Error)
                {
                    await scheduler.ResetTriggerFromErrorState(trigger.Key).ConfigureAwait(false);
                }
            }

            return Result.Success(new ResultNone());
        }
        catch (Exception ex)
        {
            return Result.Failure<ResultNone>(Error.Failure(ErrorCode.SchedulerError, ex.Message));
        }
    }

    public async Task<Result<bool>> DeleteJobAsync(IScheduler scheduler, string jobName, string jobGroup = "default")
    {
        try
        {
            var jobKey = new JobKey(jobName, jobGroup);
            var deleted = await scheduler.DeleteJob(jobKey).ConfigureAwait(false);
            if (deleted)
            {
                return Result.Success(true);
            }
            else
            {
                return Result.Failure<bool>(Error.NotFound($"Job '{jobName}' does not exist."));
            }
        }
        catch (Exception ex)
        {
            return Result.Failure<bool>(Error.Failure(ErrorCode.SchedulerError, ex.Message));
        }
    }
}
