namespace AIKernel.Providers.DynamicPipelineCompiler;

/// <summary>
/// [EN] Public capability descriptor for the dynamic pipeline compiler provider.
/// [JA] Dynamic Pipeline Compiler Provider の公開 capability descriptor です。
/// </summary>
public sealed record DynamicPipelineCompilerCapabilityDescriptor(
    string CapabilityId,
    string DslSchemaVersion,
    IReadOnlyDictionary<string, string> Metadata);
