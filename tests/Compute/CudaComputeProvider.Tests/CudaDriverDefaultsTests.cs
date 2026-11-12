namespace CudaComputeProvider.Tests;

using AIKernel.Providers.CudaCompute;
using System.Text.Json;
using Xunit;

public sealed class CudaDriverDefaultsTests
{
    [Fact]
    public void DefaultsIdentifyDriverAbiWithoutClaimingBoundExecution()
    {
        var settings = new CudaComputeSettings();
        var provider = new AIKernel.Providers.CudaCompute.CudaComputeProvider(settings);
        var backend = provider.ToBackendDescriptor();
        Assert.Equal("AIKernel.Kernel", backend.PackageId);
        Assert.Equal("aikernel_cuda_driver", backend.NativeModule.ModuleId);
        Assert.Equal("ak_create", backend.NativeModule.EntryPoint);
        Assert.Equal("1", backend.NativeModule.AbiVersion);
        Assert.Equal("aikernel-cuda://cuda13.0/modules/aikernel_cuda_driver", backend.NativeModule.ModuleRef);
        Assert.Equal("ak_create", provider.ToCapabilityDescriptor().EntryPoint);
        Assert.DoesNotContain(settings.ToMetadata().Values, value => value.Contains("torch", StringComparison.OrdinalIgnoreCase));
        Assert.False(provider.IsAvailable());
    }

    [Fact]
    public void MissingEntryPointUsesDriverAbiAndExplicitLegacyEntryIsPreserved()
    {
        var metadata = new Dictionary<string, string>();
        var descriptor = new CudaComputeCapabilityDescriptor("cuda.compute", "cuda13", metadata);
        Assert.Equal("ak_create", CudaComputeCapabilityContracts.ToContract(descriptor).EntryPoint);
        metadata["entry_point"] = "libtorch_bridge";
        Assert.Equal("libtorch_bridge", CudaComputeCapabilityContracts.ToContract(descriptor).EntryPoint);
    }

    [Fact]
    public void ManagedAndPythonManifestsAgreeWithDefaultSettings()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "AIKernel.Providers.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        var settings = new CudaComputeSettings();
        foreach (var relative in new[] { "src/Compute/CudaComputeProvider/cuda.provider.json", "python/src/aikernel_providers/native/cuda.provider.json" })
        {
            using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(root.FullName, relative)));
            var metadata = json.RootElement.GetProperty("metadata");
            Assert.Equal(settings.NativeModuleRef, metadata.GetProperty("nativeModuleRef").GetString());
            Assert.Equal(settings.EntryPoint, metadata.GetProperty("entryPoint").GetString());
            Assert.Equal(settings.ModuleId, metadata.GetProperty("moduleId").GetString());
            Assert.Equal(settings.PackageId, metadata.GetProperty("packageId").GetString());
            Assert.Equal(settings.AbiVersion, metadata.GetProperty("abiVersion").GetString());
        }
    }
}
