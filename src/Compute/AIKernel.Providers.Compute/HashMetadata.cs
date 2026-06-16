namespace AIKernel.Providers.Compute;

/// <summary>
/// [EN] Hash carrier for descriptor-driven provider artifacts.
/// [JA] descriptor-driven Provider artifact 向けの hash carrier です。
/// </summary>
public sealed record HashMetadata
{
    /// <summary>[EN] Hash algorithm name. [JA] hash algorithm 名です。</summary>
    public string Algorithm { get; init; } = "sha256";

    /// <summary>[EN] Hash value without algorithm prefix. [JA] algorithm prefix を含まない hash 値です。</summary>
    public string? Value { get; init; }

    /// <summary>[EN] Optional full hash expression such as sha256:abc. [JA] sha256:abc などの任意の完全 hash expression です。</summary>
    public string? Expression { get; init; }

    /// <summary>[EN] Additional deterministic metadata. [JA] 追加の deterministic metadata です。</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// [EN] Creates normalized hash metadata from an optional hash expression.
    /// [JA] 任意の hash expression から正規化された hash metadata を作成します。
    /// </summary>
    /// <param name="expression">EN:  JA: expression パラメーターです。
    /// [EN] Hash expression.
    /// [JA] hash expression です。
    /// </param>
    /// <returns>EN:  JA: 結果を返します。
    /// [EN] Normalized hash metadata.
    /// [JA] 正規化された hash metadata です。
    /// </returns>
    public static HashMetadata FromExpression(string? expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return new HashMetadata();
        }

        var parts = expression.Split(':', 2, StringSplitOptions.TrimEntries);
        return parts.Length == 2
            ? new HashMetadata
            {
                Algorithm = parts[0],
                Value = parts[1],
                Expression = expression
            }
            : new HashMetadata
            {
                Value = expression,
                Expression = expression
            };
    }
}
