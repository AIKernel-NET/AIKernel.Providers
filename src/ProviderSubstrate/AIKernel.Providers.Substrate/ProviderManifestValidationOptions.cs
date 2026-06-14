namespace AIKernel.Providers.Substrate;

/// <summary>
/// [EN] Options controlling strict provider manifest validation.
/// [JA] strict Provider manifest validation を制御する option です。
/// </summary>
public sealed record ProviderManifestValidationOptions
{
    /// <summary>[EN] Indicates whether capability declarations are required. [JA] capability declaration を必須にするかどうかを示します。</summary>
    public bool RequireCapabilities { get; init; }

    /// <summary>[EN] Indicates whether schema version must be present. [JA] schema version を必須にするかどうかを示します。</summary>
    public bool RequireSchemaVersion { get; init; }
}
