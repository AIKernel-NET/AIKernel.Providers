namespace AIKernel.Providers.Substrate;

/// <summary>
/// [EN] CLI hint metadata carried by provider manifests.
/// [JA] Provider manifest が保持する CLI hint metadata です。
/// </summary>
public sealed record ProviderCliHints
{
    /// <summary>[EN] Optional primary command name. [JA] 任意の primary command 名です。</summary>
    public string? Command { get; init; }

    /// <summary>[EN] Optional command aliases or command templates. [JA] 任意の command alias または command template です。</summary>
    public IReadOnlyList<string> Commands { get; init; } = [];

    /// <summary>[EN] Optional default operation name. [JA] 任意の default operation 名です。</summary>
    public string? DefaultOperation { get; init; }

    /// <summary>[EN] Configuration key names consumed by the CLI surface. [JA] CLI surface が利用する configuration key 名です。</summary>
    public IReadOnlyList<string> ConfigKeys { get; init; } = [];

    /// <summary>[EN] Required environment variable names. [JA] 必須 environment variable 名です。</summary>
    public IReadOnlyList<string> RequiredEnvironment { get; init; } = [];

    /// <summary>[EN] Unknown CLI JSON members preserved as raw JSON text. [JA] raw JSON text として保持する未知の CLI JSON member です。</summary>
    public IReadOnlyDictionary<string, string> ExtensionJson { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}
