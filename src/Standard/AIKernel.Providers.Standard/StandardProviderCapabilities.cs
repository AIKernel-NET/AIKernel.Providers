namespace AIKernel.Providers.Standard;

using AIKernel.Abstractions.Models;
using AIKernel.Abstractions.Providers;
using AIKernel.Dtos.Core;
using AIKernel.Dtos.Routing;

/// <summary>
/// [EN] Reusable provider capability descriptor for standard OS driver providers.
/// [JA] 標準 OS driver Provider 向けの再利用可能な provider capability descriptor です。
/// </summary>
public sealed class StandardProviderCapabilities : IProviderCapabilities
{
    private readonly IReadOnlyList<string> _operations;
    private readonly IReadOnlyList<string> _dataTypes;

    /// <summary>
    /// [EN] Initializes the capability descriptor with deterministic operation and data-type lists.
    /// [JA] 決定論的な operation / data-type list で capability descriptor を初期化します。
    /// </summary>
    public StandardProviderCapabilities(
        IEnumerable<string> operations,
        IEnumerable<string> dataTypes)
    {
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentNullException.ThrowIfNull(dataTypes);

        _operations = operations
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.Ordinal)
            .ToArray();
        _dataTypes = dataTypes
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>[EN] Supported standard driver operations. [JA] 対応する標準 driver operation です。</summary>
    public IReadOnlyList<string> SupportedOperations => _operations;

    /// <summary>[EN] Supported standard driver data types. [JA] 対応する標準 driver data type です。</summary>
    public IReadOnlyList<string> SupportedDataTypes => _dataTypes;

    /// <summary>[EN] Maximum concurrent logical connections. [JA] 最大同時 logical connection 数です。</summary>
    public int MaxConcurrentConnections => 1;

    /// <summary>[EN] Optional rate limit metadata. [JA] 任意の rate limit metadata です。</summary>
    public RateLimitInfo? RateLimit => null;

    /// <summary>[EN] Static capacity vector for standard drivers. [JA] 標準 driver の静的 capacity vector です。</summary>
    public ModelCapacityVector Vector => new();

    /// <summary>
    /// [EN] Returns no dynamic capacities because standard drivers expose deterministic local capability metadata.
    /// [JA] 標準 driver は決定論的な local capability metadata を公開するため dynamic capacity を返しません。
    /// </summary>
    public IDictionary<string, float>? GetDynamicCapacities(
        IExecutionConstraints constraints)
        => null;

    /// <summary>[EN] Returns no advanced capability profile. [JA] 高度な capability profile は返しません。</summary>
    public ICapabilityProfile? GetCapabilityProfile() => null;

    /// <summary>[EN] Checks whether an operation is supported. [JA] operation が対応しているか確認します。</summary>
    public bool SupportsOperation(string operation)
        => _operations.Contains(operation, StringComparer.OrdinalIgnoreCase);

    /// <summary>[EN] Checks whether a data type is supported. [JA] data type が対応しているか確認します。</summary>
    public bool SupportsDataType(string dataType)
        => _dataTypes.Contains(dataType, StringComparer.OrdinalIgnoreCase);

    /// <summary>[EN] Standard drivers do not advertise quantization support. [JA] 標準 driver は quantization support を宣言しません。</summary>
    public bool SupportsQuantization(string quantizationLevel) => false;

    /// <summary>[EN] Standard drivers do not augment queries. [JA] 標準 driver は query augmentation を行いません。</summary>
    public bool SupportsQueryAugmentation => false;

    /// <summary>[EN] Standard drivers do not decompose queries. [JA] 標準 driver は query decomposition を行いません。</summary>
    public bool SupportsQueryDecomposition => false;

    /// <summary>[EN] Standard drivers do not route queries. [JA] 標準 driver は query routing を行いません。</summary>
    public bool SupportsQueryRouting => false;

    /// <summary>[EN] Maximum query parts for standard drivers. [JA] 標準 driver の最大 query part 数です。</summary>
    public int MaxQueryParts => 0;

    /// <summary>[EN] Supported query-processing operations. [JA] 対応する query processing operation です。</summary>
    public IReadOnlyList<string> SupportedQueryProcessingOperations => [];

    /// <summary>[EN] Checks query-processing support. [JA] query processing 対応を確認します。</summary>
    public bool SupportsQueryProcessingOperation(string operation) => false;

    /// <summary>[EN] Standard drivers do not expose embeddings. [JA] 標準 driver は embedding を公開しません。</summary>
    public bool SupportsEmbedding => false;

    /// <summary>[EN] Embedding dimension metadata. [JA] embedding dimension metadata です。</summary>
    public int? EmbeddingDimensions => null;

    /// <summary>[EN] Supported embedding model names. [JA] 対応する embedding model 名です。</summary>
    public IReadOnlyList<string> SupportedEmbeddingModels => [];
}
