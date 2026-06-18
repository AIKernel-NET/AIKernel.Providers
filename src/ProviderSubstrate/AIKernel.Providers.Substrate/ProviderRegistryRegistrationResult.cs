namespace AIKernel.Providers.Substrate;

/// <summary>
/// [EN] Structured result returned when registering a provider manifest.
/// [JA] Provider manifest registration が返す構造化 result です。
/// </summary>
public sealed record ProviderRegistryRegistrationResult
{
    /// <summary>[EN] Indicates whether registration succeeded. [JA] registration が成功したかどうかを示します。</summary>
    public bool Succeeded { get; init; }

    /// <summary>[EN] Registered provider manifest descriptor. [JA] 登録された Provider manifest descriptor です。</summary>
    public ProviderManifestDescriptor? Provider { get; init; }

    /// <summary>[EN] Stable error code when registration failed. [JA] registration 失敗時の安定した error code です。</summary>
    public string? ErrorCode { get; init; }

    /// <summary>[EN] Human-readable error message when registration failed. [JA] registration 失敗時の人間可読な error message です。</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>[EN] Structured diagnostics emitted during registration. [JA] registration 中に出力された構造化 diagnostic です。</summary>
    public IReadOnlyList<ProviderDiagnostic> Diagnostics { get; init; } = [];
}
