using System.Globalization;
using Anvil.Primitives;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Anvil.Scheduling.Quartz;

public interface IRetryableJob
{
    Task<Result<ResultNone>> ExecuteAsync(IJobExecutionContext context, CancellationToken cancellationToken);
    Task<Result<ResultNone>> AbortByTimeoutAsync(IJobExecutionContext context, CancellationToken cancellationToken);
}

internal static class RetryableJobDefaults
{
    public const string StartedAtUtcKey = "startedAtUtc";
    public const string AttemptsKey = "attempts";
    public const string MaxWindowSecondsKey = "maxWindowSeconds";
    public const string LastErrorKey = "lastError";
    public const int DefaultMaxWindowSeconds = 300;
}

[DisallowConcurrentExecution]
[PersistJobDataAfterExecution]
public sealed class RetryableJob<TJob> : IJob
    where TJob : class, IRetryableJob
{
    private readonly TJob _innerJob;
    private readonly ILogger<RetryableJob<TJob>> _logger;

    public RetryableJob(TJob innerJob, ILogger<RetryableJob<TJob>> logger)
    {
        _innerJob = innerJob;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var jobData = context.JobDetail.JobDataMap;
        var now = DateTime.UtcNow;

        if (!jobData.ContainsKey(RetryableJobDefaults.StartedAtUtcKey))
        {
            jobData[RetryableJobDefaults.StartedAtUtcKey] = now.ToString("o", CultureInfo.InvariantCulture);
            jobData[RetryableJobDefaults.AttemptsKey] = 0;
        }

        var startedAt = DateTime.Parse(
            jobData[RetryableJobDefaults.StartedAtUtcKey].ToString()!,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

        var attempts = Convert.ToInt32(jobData[RetryableJobDefaults.AttemptsKey], CultureInfo.InvariantCulture);
        var elapsed = DateTime.UtcNow - startedAt;
        var maxWindowSeconds = jobData.ContainsKey(RetryableJobDefaults.MaxWindowSecondsKey)
            ? Convert.ToInt32(jobData[RetryableJobDefaults.MaxWindowSecondsKey], CultureInfo.InvariantCulture)
            : RetryableJobDefaults.DefaultMaxWindowSeconds;

        if (elapsed > TimeSpan.FromSeconds(maxWindowSeconds))
        {
            if (await TryAbortAsync(context).ConfigureAwait(false))
            {
                return;
            }
        }

        var currentAttempt = attempts + 1;
        jobData[RetryableJobDefaults.AttemptsKey] = currentAttempt;

        Result<ResultNone> result;
        try
        {
            result = await _innerJob.ExecuteAsync(context, context.CancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unhandled exception executing retryable job {JobKey}", context.JobDetail.Key);
            result = Result.Failure<ResultNone>(Error.Failure(ErrorCode.SchedulerError, ex.Message));
        }

        if (result.IsSuccess)
        {
            _logger.LogInformation("Retryable job {JobKey} completed after {Attempts} attempts", context.JobDetail.Key, currentAttempt);
            await context.Scheduler.DeleteJob(context.JobDetail.Key, context.CancellationToken).ConfigureAwait(false);
        }
        else
        {
            jobData[RetryableJobDefaults.LastErrorKey] = result.Error.Description;
            if (elapsed > TimeSpan.FromSeconds(maxWindowSeconds))
            {
                await TryAbortAsync(context).ConfigureAwait(false);
            }
            else
            {
                _logger.LogInformation(
                    "Retryable job {JobKey} failed attempt {Attempt}. Reason: {Reason}",
                    context.JobDetail.Key,
                    currentAttempt,
                    result.Error.Description);
            }
        }
    }

    private async Task<bool> TryAbortAsync(IJobExecutionContext context)
    {
        var abortResult = await _innerJob.AbortByTimeoutAsync(context, context.CancellationToken).ConfigureAwait(false);
        if (abortResult.IsFailure)
        {
            return false;
        }

        _logger.LogWarning("Retryable job {JobKey} aborted due to timeout window", context.JobDetail.Key);
        return await context.Scheduler.DeleteJob(context.JobDetail.Key, context.CancellationToken).ConfigureAwait(false);
    }
}
