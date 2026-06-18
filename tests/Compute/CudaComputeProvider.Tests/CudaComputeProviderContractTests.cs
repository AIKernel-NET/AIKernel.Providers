using AIKernel.Dtos.Capabilities;
using AIKernel.Enums;
using AIKernel.Providers.Compute;
using AIKernel.Providers.CudaCompute;
using AIKernel.Providers.CudaCompute.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

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
        Assert.DoesNotContain("AIKernel.Cuda13.0", referenced);
        Assert.DoesNotContain(referenced, name => name is not null && name.Contains("Cuda13", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ToBackendDescriptor_CarriesNativeBoundaryByDescriptorOnly()
    {
        var provider = new global::AIKernel.Providers.CudaCompute.CudaComputeProvider(new CudaComputeSettings
        {
            ProviderId = "providers.cuda",
            BackendName = "cuda13.0",
            NativeModuleRef = "aikernel-cuda://cuda13.0/modules/libtorch_bridge",
            ArtifactHash = "sha256:cuda",
            MaxDeviceMemoryBytes = 1024
        });

        var descriptor = provider.ToBackendDescriptor();

        Assert.Equal("cuda13.0", descriptor.BackendName);
        Assert.Equal("cuda13.0", descriptor.BackendId);
        Assert.Equal("13.0", descriptor.BackendVersion);
        Assert.Equal("aikernel-cuda13", descriptor.PackageId);
        Assert.Equal("aikernel-cuda://cuda13.0/modules/libtorch_bridge", descriptor.NativeModule.ModuleRef);
        Assert.Equal("cuda13.0", descriptor.NativeModule.BackendId);
        Assert.Equal("libtorch_bridge", descriptor.NativeModule.ModuleId);
        Assert.Equal("1.0", descriptor.NativeModule.AbiVersion);
        Assert.Equal("sha256:cuda", descriptor.NativeModule.Hash.Expression);
        Assert.Equal("sha256:cuda", descriptor.NativeModule.ArtifactHash);
        Assert.Contains("tensor.matmul", descriptor.Operations);
        Assert.Contains("tensor.matmul", descriptor.SupportedOps);
        Assert.Equal(1024, descriptor.MaxDeviceMemoryBytes);
    }

    [Fact]
    public void CudaBackendResolver_MissingOperation_ReturnsStructuredUnavailable()
    {
        var provider = new global::AIKernel.Providers.CudaCompute.CudaComputeProvider();
        var result = new CudaBackendResolver().Resolve(
            provider.ToBackendDescriptor(),
            new CudaBackendResolutionPolicy
            {
                RequiredBackend = "cuda13.0",
                RequiredDeviceProfile = "cuda13",
                RequiredOperations = ["tensor.unknown"]
            });

        Assert.False(result.Succeeded);
        Assert.Equal("CUDA_OPERATION_NOT_ADVERTISED", result.ErrorCode);
        Assert.Equal(ComputeAvailabilityReason.UnsupportedOperation, result.AvailabilityReason);
        Assert.NotEmpty(result.Diagnostics);
    }

    [Fact]
    public void CudaBackendResolver_MissingNativeModule_ReturnsNativeModuleMissing()
    {
        var result = new CudaBackendResolver().Resolve(
            new CudaBackendDescriptor
            {
                BackendName = "cuda13.0",
                DeviceProfile = "cuda13",
                Operations = ["tensor.matmul"]
            },
            new CudaBackendResolutionPolicy
            {
                RequiredBackend = "cuda13.0",
                RequiredDeviceProfile = "cuda13",
                RequiredOperations = ["tensor.matmul"]
            });

        Assert.False(result.Succeeded);
        Assert.Equal("CUDA_NATIVE_MODULE_MISSING", result.ErrorCode);
        Assert.Equal(ComputeAvailabilityReason.NativeModuleMissing, result.AvailabilityReason);
    }

    [Fact]
    public void CudaBackendResolver_InvalidNativeModuleScheme_FailsClosed()
    {
        var result = new CudaBackendResolver().Resolve(
            new CudaBackendDescriptor
            {
                BackendName = "cuda13.0",
                DeviceProfile = "cuda13",
                NativeModule = new NativeModuleDescriptor
                {
                    ModuleRef = "aikernel-cuda13://modules/libtorch_bridge"
                },
                Operations = ["tensor.matmul"]
            },
            new CudaBackendResolutionPolicy
            {
                RequiredBackend = "cuda13.0",
                RequiredDeviceProfile = "cuda13",
                RequiredOperations = ["tensor.matmul"]
            });

        Assert.False(result.Succeeded);
        Assert.Equal("CUDA_NATIVE_MODULE_REF_INVALID", result.ErrorCode);
        Assert.Equal(ComputeAvailabilityReason.NativeModuleMissing, result.AvailabilityReason);
    }

    [Fact]
    public async Task Invoker_RecognizedOperationReturnsBackendNotBoundFailure()
    {
        var invoker = new CudaComputeInvoker();

        var result = await invoker.InvokeAsync(new CapabilityInvocationRequest(
            "invoke-1",
            "cuda.compute",
            "tensor.matmul",
            new Dictionary<string, string>(),
            null,
            "sha256:replay",
            new Dictionary<string, string>()),
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("CUDA_BACKEND_NOT_BOUND", result.ErrorCode);
        Assert.Equal("BackendNotInstalled", result.Metadata["compute.availability_reason"]);
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
        Assert.Equal("UnsupportedOperation", result.Metadata["compute.availability_reason"]);
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
            [
                "abi_version",
                "artifact_hash",
                "backend",
                "backend_id",
                "backend_version",
                "device_profile",
                "entry_point",
                "loader_json",
                "module_id",
                "native_module_ref",
                "package_id",
                "version"
            ],
            settings.ToMetadata().Keys.ToArray());
    }

    [Fact]
    public void AddCudaComputeProvider_RegistersDescriptorBoundaryServices()
    {
        var services = new ServiceCollection();
        services.AddCudaComputeProvider(new CudaComputeSettings
        {
            ProviderId = "providers.cuda.test",
            BackendId = "cuda-test"
        });

        using var provider = services.BuildServiceProvider();

        Assert.Equal("providers.cuda.test", provider.GetRequiredService<CudaComputeSettings>().ProviderId);
        Assert.Equal("providers.cuda.test", provider.GetRequiredService<global::AIKernel.Providers.CudaCompute.CudaComputeProvider>().ProviderId);
        Assert.NotNull(provider.GetRequiredService<CudaComputeInvoker>());
        Assert.NotNull(provider.GetRequiredService<CudaBackendResolver>());
        Assert.NotNull(provider.GetRequiredService<CudaBackendResolutionPolicy>());
    }
}
