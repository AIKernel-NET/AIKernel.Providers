namespace AIKernel.Providers.DynamicPipelineCompiler;

/// <summary>
/// [EN] Python bridge that exposes the public dynamic pipeline compiler provider contract surface.
/// [JA] 公開 Dynamic Pipeline Compiler Provider 契約 surface を公開する Python bridge です。
/// </summary>
public static class DynamicPipelineCompilerPythonBridge
{
    /// <summary>
    /// [EN] Creates a deterministic capability descriptor for Python wrappers.
    /// [JA] Python wrapper 用の決定論的 capability descriptor を作成します。
    /// </summary>
    public static object ToContract(
        string providerId,
        string dslSchemaVersion)
    {
        var settings = new DynamicPipelineCompilerSettings
        {
            ProviderId = providerId,
            DslSchemaVersion = dslSchemaVersion
        };

        return DynamicPipelineCompilerCapabilityContracts.ToContract(
            new DynamicPipelineCompilerCapabilityDescriptor(
                settings.ProviderId,
                settings.DslSchemaVersion,
                settings.ToMetadata()));
    }

    /// <summary>
    /// [EN] Creates a provider with default settings.
    /// [JA] default settings の Provider を作成します。
    /// </summary>
    public static object CreateProvider()
        => new DynamicPipelineCompilerProvider();

    /// <summary>
    /// [EN] Creates a capability module invoker.
    /// [JA] capability module invoker を作成します。
    /// </summary>
    public static object CreateInvoker()
        => new DynamicPipelineCompilerInvoker();
}
