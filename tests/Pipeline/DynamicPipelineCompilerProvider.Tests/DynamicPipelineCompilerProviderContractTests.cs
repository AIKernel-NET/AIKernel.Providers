using AIKernel.Dtos.Capabilities;
using AIKernel.Enums;
using AIKernel.Providers.DynamicPipelineCompiler;

namespace AIKernel.Providers.Tests;

public sealed class DynamicPipelineCompilerProviderContractTests
{
    [Fact]
    public void ToContract_ExposesDynamicPipelineOperations()
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["version"] = "0.1.3",
            ["dsl_schema_version"] = "0.2",
            ["gpu_execution_paths"] = "optional",
            ["parenthesized_boolean_expressions"] = "supported",
            ["pipeline_architecture"] = "aisthesis->phainesis->nous->topos->kairos->kinesis->zoe"
        };

        var contract = DynamicPipelineCompilerCapabilityContracts.ToContract(
            new DynamicPipelineCompilerCapabilityDescriptor(
                "dynamic-pipeline",
                "0.2",
                metadata));

        Assert.Equal("dynamic-pipeline", contract.CapabilityId);
        Assert.Equal("Dynamic Pipeline Compiler Provider", contract.Name);
        Assert.Equal(CapabilityModuleKind.ManagedAssembly, contract.Kind);
        Assert.Equal(CapabilityInvocationMode.AssemblyReference, contract.InvocationMode);
        Assert.Equal("AIKernel.Providers.DynamicPipelineCompiler", contract.EntryPoint);
        Assert.Equal(
            ["pipeline.compile", "pipeline.execute", "pipeline.validate"],
            contract.ProvidedOperations);
        Assert.Equal(["dsl.read", "capability.register"], contract.RequiredPermissions);
        Assert.Equal("supported", contract.Metadata["parenthesized_boolean_expressions"]);
        Assert.Equal("optional", contract.Metadata["gpu_execution_paths"]);
    }

    [Fact]
    public async Task Provider_LifecycleAndCapabilitiesRemainContractPure()
    {
        var provider = new global::AIKernel.Providers.DynamicPipelineCompiler.DynamicPipelineCompilerProvider(new DynamicPipelineCompilerSettings
        {
            ProviderId = "dynamic-pipeline",
            DslSchemaVersion = "0.2"
        });

        Assert.False(await provider.IsAvailableAsync());
        await provider.InitializeAsync();

        Assert.True(await provider.IsAvailableAsync());
        Assert.True(provider.GetCapabilities().SupportsOperation("pipeline.compile"));
        Assert.True(provider.GetCapabilities().SupportsOperation("pipeline.validate"));
        Assert.Equal("dynamic-pipeline", provider.ToCapabilityDescriptor().CapabilityId);
        Assert.Equal("0.2", provider.ToCapabilityDescriptor().Metadata["dsl_schema_version"]);
        Assert.Equal("supported", provider.ToCapabilityDescriptor().Metadata["parenthesized_boolean_expressions"]);
        Assert.Equal("optional", provider.ToCapabilityDescriptor().Metadata["gpu_execution_paths"]);
        Assert.Equal("true", provider.ToCapabilityDescriptor().Metadata["rev3"]);
        Assert.Equal(
            "aisthesis->phainesis->nous->topos->kairos->kinesis->zoe",
            provider.ToCapabilityDescriptor().Metadata["pipeline_architecture"]);

        await provider.ShutdownAsync();
        Assert.False(await provider.IsAvailableAsync());
    }

    [Fact]
    public void ProviderAssembly_DoesNotReferenceTools()
    {
        var referenced = typeof(global::AIKernel.Providers.DynamicPipelineCompiler.DynamicPipelineCompilerProvider).Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name)
            .ToArray();

        Assert.DoesNotContain("AIKernel.Tools", referenced);
        Assert.DoesNotContain(referenced, name => name is not null && name.StartsWith("AIKernel.Tools.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Invoker_RejectsUnsupportedOperationFailClosed()
    {
        var invoker = new DynamicPipelineCompilerInvoker();

        var result = await invoker.InvokeAsync(new CapabilityInvocationRequest(
            "invoke-1",
            "dynamic-pipeline",
            "unknown.operation",
            new Dictionary<string, string>(),
            null,
            "sha256:replay",
            new Dictionary<string, string>()),
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("DYNAMIC_PIPELINE_OPERATION_NOT_SUPPORTED", result.ErrorCode);
    }

    [Fact]
    public async Task Invoker_ValidatesParenthesizedBooleanOrExpression()
    {
        var invoker = new DynamicPipelineCompilerInvoker();
        const string condition = "(gapVector >= 0.30 || landmarkVector >= 0.25) && motionForwardProgress < 0.10";

        var result = await invoker.InvokeAsync(new CapabilityInvocationRequest(
            "invoke-conditions-1",
            "dynamic-pipeline",
            "pipeline.validate",
            new Dictionary<string, string>
            {
                ["condition"] = condition
            },
            null,
            "sha256:replay",
            new Dictionary<string, string>()),
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Null(result.ErrorCode);
        Assert.Equal("passed", result.Metadata["condition_validation"]);
        Assert.Equal("boolean-v1", result.Metadata["condition_expression_grammar"]);
        Assert.Equal("supported", result.Metadata["parenthesized_boolean_expressions"]);
        Assert.Equal(condition, result.Metadata["condition_expression"]);
    }

    [Fact]
    public async Task Invoker_PipelineCompileValidatesWhenExpression()
    {
        var invoker = new DynamicPipelineCompilerInvoker();

        var result = await invoker.InvokeAsync(new CapabilityInvocationRequest(
            "invoke-conditions-2",
            "dynamic-pipeline",
            "pipeline.compile",
            new Dictionary<string, string>
            {
                ["when"] = "NOT (routeDeadEndRisk == true) OR useProbeConfidence >= 0.70"
            },
            null,
            "sha256:replay",
            new Dictionary<string, string>()),
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal("passed", result.Metadata["condition_validation"]);
        Assert.Equal("boolean-v1", result.Metadata["condition_expression_grammar"]);
    }

    [Fact]
    public async Task Invoker_RejectsMalformedParenthesizedBooleanExpression()
    {
        var invoker = new DynamicPipelineCompilerInvoker();

        var result = await invoker.InvokeAsync(new CapabilityInvocationRequest(
            "invoke-conditions-3",
            "dynamic-pipeline",
            "pipeline.validate",
            new Dictionary<string, string>
            {
                ["condition"] = "(gapVector >= 0.30 || ) && motionForwardProgress < 0.10"
            },
            null,
            "sha256:replay",
            new Dictionary<string, string>()),
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("DYNAMIC_PIPELINE_CONDITION_INVALID", result.ErrorCode);
        Assert.Equal("failed", result.Metadata["condition_validation"]);
        Assert.Equal("boolean-v1", result.Metadata["condition_expression_grammar"]);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    [Fact]
    public void ProviderManifest_IncludesCliInstallSettings()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "dynamic-pipeline.provider.json");

        Assert.True(File.Exists(path));

        var json = File.ReadAllText(path);
        Assert.Contains("\"cli\"", json);
        Assert.Contains("\"command\": \"dynamic-pipeline\"", json);
        Assert.Contains("\"defaultOperation\": \"pipeline.compile\"", json);
        Assert.Contains("\"parenthesized_boolean_expressions\": \"supported\"", json);
        Assert.Contains("\"gpu_execution_paths\": \"optional\"", json);
    }
}
