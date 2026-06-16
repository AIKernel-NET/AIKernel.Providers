using AIKernel.Common.Results;
using AIKernel.Providers.Standard.Scheduler;

namespace AIKernel.Providers.Standard.Abstractions;

/// <summary>
/// [EN] Stable scheduler contract for standard AIKernel provider jobs.
/// [JA] 標準 AIKernel Provider job 向けの安定した scheduler contract です。
/// </summary>
public interface IStandardSchedulerProvider
{
    /// <summary>
    /// [EN] Safely adds a scheduled job.
    /// [JA] scheduled job を安全に追加します。
    /// </summary>
    /// <param name="name">EN:  JA: name パラメーターです。
    /// [EN] Job name.
    /// [JA] job name です。
    /// </param>
    /// <param name="interval">EN:  JA: interval パラメーターです。
    /// [EN] Positive execution interval.
    /// [JA] 正の execution interval です。
    /// </param>
    /// <returns>EN:  JA: 結果を返します。
    /// [EN] Scheduled job result.
    /// [JA] scheduled job result です。
    /// </returns>
    Result<ScheduledJob> TryAdd(string name, TimeSpan interval);

    /// <summary>
    /// [EN] Safely lists scheduled jobs.
    /// [JA] scheduled job を安全に列挙します。
    /// </summary>
    /// <returns>EN:  JA: 結果を返します。
    /// [EN] Scheduled job list result.
    /// [JA] scheduled job list result です。
    /// </returns>
    Result<IReadOnlyList<ScheduledJob>> TryList();

    /// <summary>
    /// [EN] Safely removes a scheduled job.
    /// [JA] scheduled job を安全に削除します。
    /// </summary>
    /// <param name="jobId">EN:  JA: jobId パラメーターです。
    /// [EN] Job id.
    /// [JA] job id です。
    /// </param>
    /// <returns>EN:  JA: 結果を返します。
    /// [EN] Removal result.
    /// [JA] removal result です。
    /// </returns>
    Result<bool> TryRemove(string jobId);
}
