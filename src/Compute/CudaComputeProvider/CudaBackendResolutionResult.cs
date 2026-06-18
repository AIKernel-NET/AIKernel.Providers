namespace AIKernel.Providers.CudaCompute;

using AIKernel.Providers.Compute;
using AIKernel.Providers.Substrate;

/// <summary>
/// [EN] Structured result returned by CUDA backend descriptor resolution.
/// [JA] CUDA backend descriptor resolution が返す構造化 result です。
/// </summary>
public sealed record CudaBackendResolutionResult
{
    /// <summary>[EN] Indicates whether backend descriptor resolution succeeded. [JA] backend descriptor resolution が成功したかどうかを示します。</summary>
    public bool Succeeded { get; init; }

    /// <summary>[EN] Resolved backend descriptor. [JA] 解決された backend descriptor です。</summary>
    public CudaBackendDescriptor? Backend { get; init; }

    /// <summary>[EN] Structured compute availability reason. [JA] 構造化された compute availability reason です。</summary>
    public ComputeAvailabilityReason AvailabilityReason { get; init; } = ComputeAvailabilityReason.Unknown;

    /// <summary>[EN] Stable error code when resolution failed. [JA] resolution 失敗時の安定した error code です。</summary>
    public string? ErrorCode { get; init; }

    /// <summary>[EN] Human-readable error message when resolution failed. [JA] resolution 失敗時の人間可読な error message です。</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>[EN] Structured diagnostics emitted during resolution. [JA] resolution 中に出力された構造化 diagnostic です。</summary>
    public IReadOnlyList<ProviderDiagnostic> Diagnostics { get; init; } = [];
}
