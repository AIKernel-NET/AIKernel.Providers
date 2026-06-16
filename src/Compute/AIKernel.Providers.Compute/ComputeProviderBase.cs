namespace AIKernel.Providers.Compute;

/// <summary>
/// [EN] Backend-neutral base class for compute provider adapters.
/// [JA] compute Provider adapter 向けの backend-neutral な base class です。
/// </summary>
public abstract class ComputeProviderBase
{
    /// <summary>
    /// [EN] Initializes the compute provider base with a capability descriptor.
    /// [JA] capability descriptor を指定して compute Provider base を初期化します。
    /// </summary>
    /// <param name="capability">
    /// [EN] Compute capability descriptor.
    /// [JA] compute capability descriptor です。
    /// </param>
    protected ComputeProviderBase(ComputeCapabilityDescriptor capability)
    {
        Capability = capability ?? throw new ArgumentNullException(nameof(capability));
    }

    /// <summary>[EN] Gets the compute capability descriptor. [JA] compute capability descriptor を取得します。</summary>
    public ComputeCapabilityDescriptor Capability { get; }
}
