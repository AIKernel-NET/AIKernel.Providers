namespace AIKernel.Providers.Standard.Scheduler;

using System.Collections.Concurrent;
using AIKernel.Common.Results;
using AIKernel.Providers.Standard;
using AIKernel.Providers.Standard.Abstractions;

/// <summary>
/// [EN] Scheduled logical job metadata.
/// [JA] schedule された logical job metadata です。
/// </summary>
public sealed record ScheduledJob(
    string JobId,
    string Name,
    TimeSpan Interval,
    DateTimeOffset CreatedAtUtc);

/// <summary>
/// [EN] Standard scheduler provider for AIKernel cron-style jobs.
/// [JA] AIKernel cron-style job 向けの標準 scheduler Provider です。
/// </summary>
public sealed class SchedulerProvider : StandardProviderBase, IStandardSchedulerProvider
{
    private readonly ConcurrentDictionary<string, ScheduledJob> _jobs = new(StringComparer.Ordinal);

    /// <summary>[EN] Initializes the scheduler provider. [JA] scheduler Provider を初期化します。</summary>
    public SchedulerProvider()
        : base("providers.scheduler.standard", "Scheduler Provider", "0.1.1", ["schedule.add", "schedule.list", "schedule.remove"], ["job"])
    {
    }

    /// <summary>[EN] Adds a scheduled logical job. [JA] schedule された logical job を追加します。</summary>
    public ScheduledJob Add(string name, TimeSpan interval)
        => RequireSuccess(TryAdd(name, interval));

    /// <inheritdoc />
    public Result<ScheduledJob> TryAdd(string name, TimeSpan interval)
    {
        return
            from validName in ValidateName(name)
            from validInterval in ValidateInterval(interval)
            from job in Try.Run(() =>
            {
                var created = new ScheduledJob(
                    $"job-{Guid.NewGuid():N}",
                    validName,
                    validInterval,
                    DateTimeOffset.UtcNow);
                _jobs[created.JobId] = created;
                return created;
            })
            select job;
    }

    /// <summary>[EN] Lists scheduled logical jobs. [JA] schedule された logical job を列挙します。</summary>
    public IReadOnlyList<ScheduledJob> List()
        => RequireSuccess(TryList());

    /// <inheritdoc />
    public Result<IReadOnlyList<ScheduledJob>> TryList()
        => Try.Run<IReadOnlyList<ScheduledJob>>(() => _jobs.Values.OrderBy(job => job.JobId, StringComparer.Ordinal).ToArray());

    /// <summary>[EN] Removes a scheduled logical job. [JA] schedule された logical job を削除します。</summary>
    public bool Remove(string jobId)
        => RequireSuccess(TryRemove(jobId));

    /// <inheritdoc />
    public Result<bool> TryRemove(string jobId)
        =>
            from validJobId in ValidateName(jobId)
            select _jobs.TryRemove(validJobId, out _);

    private static Result<string> ValidateName(string value)
        => string.IsNullOrWhiteSpace(value)
            ? Result<string>.Fail("Scheduler value is required. ErrorCode=STANDARD_SCHEDULER_VALUE_REQUIRED")
            : Result<string>.Success(value);

    private static Result<TimeSpan> ValidateInterval(TimeSpan interval)
        => interval <= TimeSpan.Zero
            ? Result<TimeSpan>.Fail("Interval must be positive. ErrorCode=STANDARD_SCHEDULER_INTERVAL_INVALID")
            : Result<TimeSpan>.Success(interval);

    private static T RequireSuccess<T>(Result<T> result)
        => result.Match(
            error => throw new InvalidOperationException(error.Message),
            value => value);
}
