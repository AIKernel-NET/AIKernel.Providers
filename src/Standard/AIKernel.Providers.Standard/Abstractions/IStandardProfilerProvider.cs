using AIKernel.Common.Results;
using AIKernel.Providers.Standard.Profiler;

namespace AIKernel.Providers.Standard.Abstractions;

/// <summary>
/// [EN] Stable profiler contract for standard AIKernel resource inspection.
/// [JA] 標準 AIKernel resource inspection 向けの安定した profiler contract です。
/// </summary>
public interface IStandardProfilerProvider
{
    /// <summary>
    /// [EN] Safely captures a resource snapshot.
    /// [JA] resource snapshot を安全に取得します。
    /// </summary>
    /// <returns>EN:  JA: 結果を返します。
    /// [EN] Resource snapshot result.
    /// [JA] resource snapshot result です。
    /// </returns>
    Result<ResourceSnapshot> TryCapture();
}
