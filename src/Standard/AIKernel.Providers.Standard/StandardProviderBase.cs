namespace AIKernel.Providers.Standard;

using AIKernel.Abstractions.Providers;
using AIKernel.Common.Results;
using AIKernel.Dtos.Core;

/// <summary>
/// [EN] Base implementation for standard OS driver providers.
/// [JA] 標準 OS driver Provider の base implementation です。
/// </summary>
public abstract class StandardProviderBase : IProvider
{
    private readonly IProviderCapabilities _capabilities;
    private volatile bool _initialized;

    /// <summary>
    /// [EN] Initializes a standard provider with identity and capability metadata.
    /// [JA] identity と capability metadata で標準 Provider を初期化します。
    /// </summary>
    protected StandardProviderBase(
        string providerId,
        string name,
        string version,
        IEnumerable<string> operations,
        IEnumerable<string> dataTypes)
    {
        ProviderId = string.IsNullOrWhiteSpace(providerId)
            ? throw new ArgumentException("Provider id is required.", nameof(providerId))
            : providerId;
        Name = string.IsNullOrWhiteSpace(name)
            ? throw new ArgumentException("Provider name is required.", nameof(name))
            : name;
        Version = string.IsNullOrWhiteSpace(version)
            ? throw new ArgumentException("Provider version is required.", nameof(version))
            : version;
        _capabilities = new StandardProviderCapabilities(operations, dataTypes);
    }

    /// <summary>[EN] Provider identifier. [JA] Provider 識別子です。</summary>
    public string ProviderId { get; }

    /// <summary>[EN] Human-readable provider name. [JA] 人間可読な Provider 名です。</summary>
    public string Name { get; }

    /// <summary>[EN] Provider version. [JA] Provider version です。</summary>
    public string Version { get; }

    /// <summary>[EN] Returns provider capabilities. [JA] Provider capability を返します。</summary>
    public IProviderCapabilities GetCapabilities() => _capabilities;

    /// <summary>[EN] Returns whether this provider has been initialized. [JA] Provider が初期化済みかどうかを返します。</summary>
    public Task<bool> IsAvailableAsync() => Task.FromResult(_initialized);

    /// <summary>[EN] Initializes this provider. [JA] Provider を初期化します。</summary>
    public virtual Task InitializeAsync()
    {
        _initialized = true;
        return Task.CompletedTask;
    }

    /// <summary>[EN] Shuts this provider down. [JA] Provider を終了します。</summary>
    public virtual Task ShutdownAsync()
    {
        _initialized = false;
        return Task.CompletedTask;
    }

    /// <summary>[EN] Returns deterministic health information. [JA] 決定論的な health 情報を返します。</summary>
    public virtual Task<ProviderHealthStatus> GetHealthAsync()
        => Task.FromResult(new ProviderHealthStatus(
            _initialized,
            MonadicDecision.SelectText(_initialized, "Provider is not initialized.", "Provider initialized."),
            DateTime.UtcNow,
            0));
}
