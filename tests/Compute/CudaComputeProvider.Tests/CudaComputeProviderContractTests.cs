using AIKernel.Dtos.Capabilities;
using AIKernel.Enums;
using AIKernel.Providers.CudaCompute;

namespace AIKernel.Providers.Tests;

public sealed class CudaComputeProviderContractTests
{
    [Fact]
    public void ToContract_ExposesCudaProviderOperations()
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["version"] = "0.1.1",
            ["device_profile"] = "cuda13",
            ["entry_point"] = "libtorch_bridge",
            ["loader_json"] = "rom://providers/cuda/loader.json"
        };

        var contract = CudaComputeCapabilityContracts.ToContract(
            new CudaComputeCapabilityDescriptor(
                "providers.cuda",
                "cuda13",
                metadata));

        Assert.Equal("providers.cuda", contract.CapabilityId);
        Assert.Equal("CUDA Compute Provider", contract.Name);
        Assert.Equal(CapabilityModuleKind.NativeLibrary, contract.Kind);
        Assert.Equal(CapabilityInvocationMode.NativeAbi, contract.InvocationMode);
        Assert.Equal("libtorch_bridge", contract.EntryPoint);
        Assert.Equal("rom://providers/cuda/loader.json", contract.ArtifactUri);
        Assert.Equal(
            ["tensor.matmul", "tensor.softmax", "tensor.conv2d", "tensor.layernorm"],
            contract.ProvidedOperations);
        Assert.Equal(["native.load", "tensor.compute"], contract.RequiredPermissions);
    }

    [Fact]
    public async Task Provider_LifecycleAndCapabilitiesRemainContractPure()
    {
        var provider = new global::AIKernel.Providers.CudaCompute.CudaComputeProvider(new CudaComputeSettings
        {
            ProviderId = "providers.cuda",
            DeviceProfile = "cuda13"
        });

        Assert.False(await provider.IsAvailableAsync());
        await provider.InitializeAsync();

        Assert.True(await provider.IsAvailableAsync());
        Assert.True(provider.GetCapabilities().SupportsOperation("tensor.matmul"));
        Assert.True(provider.GetCapabilities().SupportsOperation("tensor.softmax"));
        Assert.Equal("providers.cuda", provider.ToCapabilityDescriptor().CapabilityId);

        await provider.ShutdownAsync();
        Assert.False(await provider.IsAvailableAsync());
    }

    [Fact]
    public void ProviderAssembly_DoesNotReferenceTools()
    {
        var referenced = typeof(global::AIKernel.Providers.CudaCompute.CudaComputeProvider).Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name)
            .ToArray();

        Assert.DoesNotContain("AIKernel.Tools", referenced);
        Assert.DoesNotContain(referenced, name => name is not null && name.StartsWith("AIKernel.Tools.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Invoker_RejectsUnsupportedOperationFailClosed()
    {
        var invoker = new CudaComputeInvoker();

        var result = await invoker.InvokeAsync(new CapabilityInvocationRequest(
            "invoke-1",
            "cuda.compute",
            "unknown.operation",
            new Dictionary<string, string>(),
            null,
            "sha256:replay",
            new Dictionary<string, string>()),
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("CUDA_OPERATION_NOT_SUPPORTED", result.ErrorCode);
    }

    [Fact]
    public void ProviderManifest_IncludesCliSettings()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "cuda.provider.json");

        Assert.True(File.Exists(path));

        var json = File.ReadAllText(path);
        Assert.Contains("\"cli\"", json);
        Assert.Contains("\"defaultOperation\": \"tensor.matmul\"", json);
        Assert.Contains("\"command\": \"cuda\"", json);
    }

    [Fact]
    public void Settings_MetadataIsDeterministicallyOrdered()
    {
        var settings = new CudaComputeSettings
        {
            ArtifactHash = "sha256:cuda"
        };

        Assert.Equal(
            ["artifact_hash", "device_profile", "entry_point", "loader_json", "version"],
            settings.ToMetadata().Keys.ToArray());
    }
}
