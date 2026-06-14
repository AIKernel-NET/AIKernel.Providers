namespace AIKernel.Providers.Substrate;

/// <summary>
/// [EN] Deterministically ordered provider/backend candidate for availability probes.
/// [JA] availability probe 向けに deterministic order で並べた Provider/backend candidate です。
/// </summary>
public sealed record ProviderProbeCandidate
{
    /// <summary>[EN] Provider manifest descriptor. [JA] Provider manifest descriptor です。</summary>
    public ProviderManifestDescriptor Provider { get; init; } = new();

    /// <summary>[EN] Backend selection metadata. [JA] backend selection metadata です。</summary>
    public ProviderBackendSelection? Backend { get; init; }

    /// <summary>[EN] Stable ordinal within the probe order. [JA] probe order 内の安定した ordinal です。</summary>
    public int Ordinal { get; init; }
}
