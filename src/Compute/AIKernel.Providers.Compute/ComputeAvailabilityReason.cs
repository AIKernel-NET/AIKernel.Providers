namespace AIKernel.Providers.Compute;

/// <summary>
/// [EN] Structured availability reason for compute provider and backend resolution.
/// [JA] compute Provider / backend resolution 向けの構造化 availability reason です。
/// </summary>
public enum ComputeAvailabilityReason
{
    /// <summary>[EN] Unknown reason; callers should fail closed. [JA] 不明な reason です。caller は fail closed してください。</summary>
    Unknown = 0,

    /// <summary>[EN] Provider is missing. [JA] Provider が見つかりません。</summary>
    ProviderMissing = 1,

    /// <summary>[EN] Backend implementation is not installed. [JA] backend 実装が install されていません。</summary>
    BackendNotInstalled = 2,

    /// <summary>[EN] Native module reference cannot be resolved. [JA] native module reference を解決できません。</summary>
    NativeModuleMissing = 3,

    /// <summary>[EN] Device does not support the requested operation. [JA] device が要求 operation に対応していません。</summary>
    DeviceUnsupported = 4,

    /// <summary>[EN] Required driver is missing. [JA] 必要な driver が見つかりません。</summary>
    DriverMissing = 5,

    /// <summary>[EN] ABI does not match the requested boundary. [JA] ABI が要求 boundary と一致しません。</summary>
    AbiMismatch = 6,

    /// <summary>[EN] Requested operation is unsupported. [JA] 要求 operation は未対応です。</summary>
    UnsupportedOperation = 7
}
