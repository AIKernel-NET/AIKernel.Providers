namespace AIKernel.Providers.CudaCompute;

/// <summary>
/// [EN] Public capability descriptor for the CUDA compute external provider.
/// [JA] CUDA compute 外部 Provider の公開 capability descriptor です。
/// </summary>
public sealed record CudaComputeCapabilityDescriptor(
    string CapabilityId,
    string DeviceProfile,
    IReadOnlyDictionary<string, string> Metadata);
