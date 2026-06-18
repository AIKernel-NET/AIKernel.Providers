namespace AIKernel.Providers.Substrate;

/// <summary>
/// [EN] Structured result returned by provider routing.
/// [JA] Provider routing が返す構造化 result です。
/// </summary>
public sealed record ProviderResolutionResult
{
    /// <summary>[EN] Indicates whether provider resolution succeeded. [JA] Provider resolution が成功したかどうかを示します。</summary>
    public bool Succeeded { get; init; }

    /// <summary>[EN] Selected provider manifest descriptor. [JA] 選択された Provider manifest descriptor です。</summary>
    public ProviderManifestDescriptor? Provider { get; init; }

    /// <summary>[EN] Selected backend metadata when backend descriptors participate in routing. [JA] backend descriptor が routing に参加した場合の selected backend metadata です。</summary>
    public ProviderBackendSelection? Backend { get; init; }

    /// <summary>[EN] Stable error code when resolution failed. [JA] resolution 失敗時の安定した error code です。</summary>
    public string? ErrorCode { get; init; }

    /// <summary>[EN] Human-readable error message when resolution failed. [JA] resolution 失敗時の人間可読な error message です。</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>[EN] Structured diagnostics emitted during resolution. [JA] resolution 中に出力された構造化 diagnostic です。</summary>
    public IReadOnlyList<ProviderDiagnostic> Diagnostics { get; init; } = [];

    /// <summary>[EN] Missing-provider details when no provider matched. [JA] Provider が一致しなかった場合の missing-provider details です。</summary>
    public MissingProviderResult? MissingProvider { get; init; }

    /// <summary>[EN] Duplicate-provider details when routing is ambiguous. [JA] routing が曖昧な場合の duplicate-provider details です。</summary>
    public DuplicateProviderDiagnostic? DuplicateProvider { get; init; }
}
