namespace AIKernel.Providers.Substrate;

/// <summary>
/// [EN] Structured result returned by provider manifest catalog loading.
/// [JA] Provider manifest catalog loading が返す構造化 result です。
/// </summary>
public sealed record ProviderManifestCatalogResult
{
    /// <summary>[EN] Indicates whether catalog loading succeeded. [JA] catalog loading が成功したかどうかを示します。</summary>
    public bool Succeeded { get; init; }

    /// <summary>[EN] Registered provider manifest descriptors. [JA] 登録済み Provider manifest descriptor です。</summary>
    public IReadOnlyList<ProviderManifestDescriptor> Providers { get; init; } = [];

    /// <summary>[EN] Stable error code when loading failed. [JA] loading 失敗時の安定した error code です。</summary>
    public string? ErrorCode { get; init; }

    /// <summary>[EN] Human-readable error message when loading failed. [JA] loading 失敗時の人間可読な error message です。</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>[EN] Structured diagnostics emitted during catalog loading. [JA] catalog loading 中に出力された構造化 diagnostic です。</summary>
    public IReadOnlyList<ProviderDiagnostic> Diagnostics { get; init; } = [];
}
