namespace AIKernel.Providers.Substrate;

/// <summary>
/// [EN] Result returned by provider manifest loading.
/// [JA] Provider manifest loading が返す result です。
/// </summary>
public sealed record ProviderManifestLoadResult
{
    /// <summary>[EN] Indicates whether manifest loading succeeded. [JA] manifest loading が成功したかどうかを示します。</summary>
    public bool Succeeded { get; init; }

    /// <summary>[EN] Loaded provider manifest descriptor. [JA] load された Provider manifest descriptor です。</summary>
    public ProviderManifestDescriptor? Descriptor { get; init; }

    /// <summary>[EN] Stable error code when loading failed. [JA] loading 失敗時の安定した error code です。</summary>
    public string? ErrorCode { get; init; }

    /// <summary>[EN] Human-readable error message when loading failed. [JA] loading 失敗時の人間可読な error message です。</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>[EN] Structured diagnostics emitted during loading. [JA] loading 中に出力された構造化 diagnostic です。</summary>
    public IReadOnlyList<ProviderDiagnostic> Diagnostics { get; init; } = [];
}
