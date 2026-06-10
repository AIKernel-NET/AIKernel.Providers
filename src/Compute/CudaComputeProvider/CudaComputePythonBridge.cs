namespace AIKernel.Providers.CudaCompute;

/// <summary>
/// [EN] Python bridge that exposes the public CUDA compute provider contract surface.
/// [JA] 公開 CUDA compute Provider 契約 surface を公開する Python bridge です。
/// </summary>
public static class CudaComputePythonBridge
{
    /// <summary>
    /// [EN] Creates a deterministic capability descriptor for Python wrappers.
    /// [JA] Python wrapper 用の決定論的 capability descriptor を作成します。
    /// </summary>
    public static object ToContract(
        string providerId,
        string deviceProfile)
    {
        var settings = new CudaComputeSettings
        {
            ProviderId = providerId,
            DeviceProfile = deviceProfile
        };

        return CudaComputeCapabilityContracts.ToContract(
            new CudaComputeCapabilityDescriptor(
                settings.ProviderId,
                settings.DeviceProfile,
                settings.ToMetadata()));
    }

    /// <summary>
    /// [EN] Creates a provider with default settings.
    /// [JA] default settings の Provider を作成します。
    /// </summary>
    public static object CreateProvider()
        => new CudaComputeProvider();

    /// <summary>
    /// [EN] Creates a capability module invoker.
    /// [JA] capability module invoker を作成します。
    /// </summary>
    public static object CreateInvoker()
        => new CudaComputeInvoker();
}
