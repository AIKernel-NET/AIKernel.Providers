namespace AIKernel.Providers.Compute;

/// <summary>
/// [EN] Standard dtype names for backend-neutral compute buffer metadata.
/// [JA] backend-neutral compute buffer metadata 向けの標準 dtype 名です。
/// </summary>
public static class ComputeBufferDTypes
{
    /// <summary>[EN] Unknown dtype sentinel. [JA] unknown dtype sentinel です。</summary>
    public const string Unknown = "unknown";

    /// <summary>[EN] 32-bit floating point dtype. [JA] 32-bit floating point dtype です。</summary>
    public const string F32 = "f32";

    /// <summary>[EN] 16-bit floating point dtype. [JA] 16-bit floating point dtype です。</summary>
    public const string F16 = "f16";

    /// <summary>[EN] 64-bit floating point dtype. [JA] 64-bit floating point dtype です。</summary>
    public const string F64 = "f64";

    /// <summary>[EN] 32-bit signed integer dtype. [JA] 32-bit signed integer dtype です。</summary>
    public const string I32 = "i32";

    /// <summary>[EN] 64-bit signed integer dtype. [JA] 64-bit signed integer dtype です。</summary>
    public const string I64 = "i64";

    /// <summary>[EN] 8-bit unsigned integer dtype. [JA] 8-bit unsigned integer dtype です。</summary>
    public const string U8 = "u8";

    /// <summary>[EN] 32-bit unsigned integer dtype. [JA] 32-bit unsigned integer dtype です。</summary>
    public const string U32 = "u32";
}
