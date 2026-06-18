namespace AIKernel.Providers.Standard.Profiler;

using AIKernel.Common.Results;
using AIKernel.Providers.Standard;
using AIKernel.Providers.Standard.Abstractions;

/// <summary>
/// [EN] Public resource snapshot returned by the standard profiler provider.
/// [JA] 標準 profiler Provider が返す公開 resource snapshot です。
/// </summary>
public sealed record ResourceSnapshot(
    double CpuPercent,
    long ManagedMemoryBytes,
    long WorkingSetBytes,
    DateTimeOffset CapturedAtUtc);

/// <summary>
/// [EN] Standard profiler provider for AIKernel top-style resource inspection.
/// [JA] AIKernel top-style resource inspection 向けの標準 profiler Provider です。
/// </summary>
public sealed class ProfilerProvider : StandardProviderBase, IStandardProfilerProvider
{
    /// <summary>[EN] Initializes the profiler provider. [JA] profiler Provider を初期化します。</summary>
    public ProfilerProvider()
        : base("providers.profiler.standard", "Profiler Provider", "0.1.1", ["profiler.snapshot", "profiler.top"], ["resource.snapshot"])
    {
    }

    /// <summary>[EN] Captures a local resource snapshot. [JA] local resource snapshot を取得します。</summary>
    public ResourceSnapshot Capture()
        => RequireSuccess(TryCapture());

    /// <summary>[EN] Documents this public package API member. [JA] TryCapture を実行します。</summary>
    /// <inheritdoc />
    public Result<ResourceSnapshot> TryCapture()
        => Try.Run(() => new ResourceSnapshot(
            0,
            GC.GetTotalMemory(forceFullCollection: false),
            Environment.WorkingSet,
            DateTimeOffset.UtcNow));

    private static T RequireSuccess<T>(Result<T> result)
        => result.Match(
            error => throw new InvalidOperationException(error.Message),
            value => value);
}
