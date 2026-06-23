using AIKernel.Dtos.Gpu;

namespace AIKernel.Providers.DynamicPipelineCompiler;

/// <summary>
/// [EN] Settings for the dynamic pipeline compiler provider boundary.
/// [JA] Dynamic Pipeline Compiler Provider 境界の設定です。
/// </summary>
public sealed record DynamicPipelineCompilerSettings
{
    /// <summary>[EN] Provider id. [JA] Provider id です。</summary>
    public string ProviderId { get; init; } = "providers.dynamic-pipeline";

    /// <summary>[EN] Human-readable provider name. [JA] 人間可読な Provider 名です。</summary>
    public string Name { get; init; } = "Dynamic Pipeline Compiler Provider";

    /// <summary>[EN] Provider contract version. [JA] Provider 契約 version です。</summary>
    public string Version { get; init; } = "0.1.3";

    /// <summary>[EN] DSL schema version. [JA] DSL schema version です。</summary>
    public string DslSchemaVersion { get; init; } = "0.2";

    /// <summary>[EN] Optional DSL schema URI. [JA] 任意の DSL schema URI です。</summary>
    public string? DslSchemaUri { get; init; } = "rom://providers/dynamic-pipeline/schema.json";

    /// <summary>[EN] Returns deterministic metadata for capability export. [JA] capability export 用の決定論的 metadata を返します。</summary>
    public IReadOnlyDictionary<string, string> ToMetadata()
    {
        var metadata = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["dsl_schema_version"] = DslSchemaVersion,
            ["gpu_execution_paths"] = "optional",
            ["parenthesized_boolean_expressions"] = "supported",
            ["pipeline_architecture"] = "aisthesis->phainesis->nous->topos->kairos->kinesis->zoe",
            [GpuProviderMetadataKeys.Rev3] = "true",
            [GpuProviderMetadataKeys.Version] = Version
        };

        if (!string.IsNullOrWhiteSpace(DslSchemaUri))
        {
            metadata["dsl_schema_uri"] = DslSchemaUri;
        }

        return metadata;
    }
}
