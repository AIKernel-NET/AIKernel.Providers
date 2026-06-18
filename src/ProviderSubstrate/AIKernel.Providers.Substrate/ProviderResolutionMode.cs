namespace AIKernel.Providers.Substrate;

/// <summary>
/// [EN] Mode used by deterministic provider resolution requests.
/// [JA] deterministic Provider resolution request が使用する mode です。
/// </summary>
public enum ProviderResolutionMode
{
    /// <summary>[EN] Unknown mode; callers should fail closed. [JA] 不明な mode です。caller は fail closed してください。</summary>
    Unknown = 0,

    /// <summary>[EN] Strict resolution without implicit fallback. [JA] implicit fallback を使わない strict resolution です。</summary>
    Strict = 1,

    /// <summary>[EN] Resolution that permits the explicit fallback policy carried by the request. [JA] request が保持する明示的 fallback policy を許可する resolution です。</summary>
    ExplicitFallback = 2
}
