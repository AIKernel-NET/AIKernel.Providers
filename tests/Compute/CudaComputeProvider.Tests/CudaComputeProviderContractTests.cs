using AIKernel.Abstractions.Gpu;
using AIKernel.Dtos.Capabilities;
using AIKernel.Dtos.Gpu;
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
            ["version"] = "0.1.3",
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
            [
                GpuOperationNames.ComputeDispatch,
                GpuOperationNames.ComputeVectorAdd,
                "tensor.matmul",
                "tensor.softmax",
                "tensor.conv2d",
                "tensor.layernorm"
            ],
            contract.ProvidedOperations);
        Assert.Equal(
            [
                "native.load",
                GpuPermissionNames.ComputeExecute,
                GpuPermissionNames.BufferRead,
                GpuPermissionNames.BufferWrite,
                "tensor.compute"
            ],
            contract.RequiredPermissions);
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
        Assert.True(provider.GetCapabilities().SupportsOperation(GpuOperationNames.ComputeDispatch));
        Assert.True(provider.GetCapabilities().SupportsOperation("tensor.matmul"));
        Assert.True(provider.GetCapabilities().SupportsOperation("tensor.softmax"));
        Assert.Equal("providers.cuda", provider.ToCapabilityDescriptor().CapabilityId);

        await provider.ShutdownAsync();
        Assert.False(await provider.IsAvailableAsync());
    }

    [Fact]
    public async Task Provider_DiagnosticsExposeRev3ExecutionLayerMetadata()
    {
        var provider = new global::AIKernel.Providers.CudaCompute.CudaComputeProvider(new CudaComputeSettings
        {
            ProviderId = "providers.cuda"
        });
        await provider.InitializeAsync();

        var diagnosticsProvider = Assert.IsAssignableFrom<IGpuDiagnostics>(provider);
        var frame = new GpuFrameToken
        {
            FrameId = "frame-1",
            FrameIndex = 1,
            SampleTicks = 100,
            RawTarget = new GpuFrameTarget
            {
                TargetId = "raw",
                Backend = GpuBackend.Cuda,
                Kind = GpuFrameTargetKind.RawFramebuffer,
                Width = 320,
                Height = 200,
                PixelFormat = FramePixelFormat.Indexed8
            },
            HudTarget = new GpuFrameTarget
            {
                TargetId = "hud",
                Backend = GpuBackend.Cuda,
                Kind = GpuFrameTargetKind.HudCompositeOffscreen,
                Width = 320,
                Height = 200,
                PixelFormat = FramePixelFormat.Rgba32
            }
        };

        var diagnostics = await diagnosticsProvider.CaptureFrameDiagnosticsAsync(
            frame,
            TestContext.Current.CancellationToken);

        Assert.Equal("frame-1", diagnostics.SensorPath.FrameId);
        Assert.Equal(GpuBackend.Cuda.ToString(), diagnostics.SensorPath.Backend);
        Assert.False(diagnostics.SensorPath.ZeroCopy);
        Assert.Equal(GpuReadbackPolicy.RequiredFallback, diagnostics.SensorPath.Readback);
        Assert.Equal(320 * 200, diagnostics.SensorPath.MemoryEstimate);
        Assert.Equal(320 * 200 * 4, diagnostics.HudPath.MemoryEstimate);
        Assert.Equal("sensor", diagnostics.SensorPath.Metadata[GpuDiagnosticsMetadataKeys.Rev3PathRole]);
        Assert.Equal("sensor", diagnostics.SensorPath.Metadata[GpuDiagnosticsMetadataKeys.Rev3PassId]);
        Assert.Equal("native-cuda-descriptor", diagnostics.SensorPath.Metadata[GpuDiagnosticsMetadataKeys.Rev3ExecutionMode]);
        Assert.Equal("true", diagnostics.SensorPath.Metadata[GpuDiagnosticsMetadataKeys.Rev3PromotionBlocked]);
        Assert.Equal("false", diagnostics.SensorPath.Metadata[GpuDiagnosticsMetadataKeys.Rev3PromotionCandidateReady]);
        Assert.Equal("false", diagnostics.SensorPath.Metadata[GpuDiagnosticsMetadataKeys.Rev3PromotionDiagnosticStable]);
        Assert.Equal(
            GpuRev3PromotionGates.NativeBridgeRequired,
            diagnostics.SensorPath.Metadata[GpuDiagnosticsMetadataKeys.Rev3PromotionReason]);
        Assert.Equal("native-cuda-buffer-dispatch", diagnostics.SensorPath.Metadata[GpuProviderMetadataKeys.GpuBypass]);
        Assert.Equal("native-cuda-device-buffer", diagnostics.SensorPath.Metadata[GpuProviderMetadataKeys.ZeroCopyBufferHandling]);
        Assert.Contains("ReadyForBuiltIn=false", diagnostics.SensorPath.Metadata[GpuDiagnosticsMetadataKeys.Rev3PassReadiness]);
        Assert.True(GpuCanonicalValidation.EvaluateRev3PromotionReadiness(
            diagnostics.SensorPath.Metadata).IsBlocked);
        Assert.True(GpuCanonicalValidation.ValidateRev3ExecutionLayerMetadata(diagnostics.SensorPath.Metadata).IsValid);
        Assert.True(GpuCanonicalValidation.ValidateFrameDiagnostics(diagnostics).IsValid);
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
        Assert.Contains(GpuOperationNames.ComputeDispatch, descriptor.Operations);
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
            GpuOperationNames.ComputeDispatch,
            new Dictionary<string, string>(),
            null,
            "sha256:replay",
            new Dictionary<string, string>()),
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("CUDA_BACKEND_NOT_BOUND", result.ErrorCode);
        Assert.Equal("BackendNotInstalled", result.Metadata["compute.availability_reason"]);
        Assert.Equal("true", result.Metadata[GpuProviderMetadataKeys.Rev3]);
        Assert.Equal(GpuBackend.Cuda.ToString(), result.Metadata[GpuProviderMetadataKeys.GpuBackend]);
        Assert.Equal("native-cuda-buffer-dispatch", result.Metadata[GpuProviderMetadataKeys.GpuBypass]);
        Assert.Equal("native-cuda-device-buffer", result.Metadata[GpuProviderMetadataKeys.ZeroCopyBufferHandling]);
        Assert.True(GpuCanonicalValidation.ValidateRev3ExecutionLayerMetadata(result.Metadata).IsValid);
    }

    [Fact]
    public async Task Invoker_OverwritesSpoofedGpuExecutionMetadata()
    {
        var invoker = new CudaComputeInvoker();

        var result = await invoker.InvokeAsync(new CapabilityInvocationRequest(
            "invoke-spoofed-metadata",
            "cuda.compute",
            GpuOperationNames.ComputeDispatch,
            new Dictionary<string, string>(),
            null,
            "sha256:replay",
            new Dictionary<string, string>
            {
                ["caller_trace"] = "preserve-me",
                [GpuProviderMetadataKeys.Backend] = GpuBackend.WebGpu.ToString(),
                [GpuProviderMetadataKeys.GpuBackend] = GpuBackend.WebGpu.ToString(),
                [GpuProviderMetadataKeys.GpuBypass] = "raw-texture-binding",
                [GpuProviderMetadataKeys.NativeJsBridge] = "rev3-envelope-bridge",
                [GpuProviderMetadataKeys.PassBridge] = "optional-native-or-js",
                [GpuProviderMetadataKeys.ZeroCopyBufferHandling] = "raw-framebuffer-texture"
            }),
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("preserve-me", result.Metadata["caller_trace"]);
        Assert.Equal("cuda13.0", result.Metadata[GpuProviderMetadataKeys.Backend]);
        Assert.Equal(GpuBackend.Cuda.ToString(), result.Metadata[GpuProviderMetadataKeys.GpuBackend]);
        Assert.Equal("native-cuda-buffer-dispatch", result.Metadata[GpuProviderMetadataKeys.GpuBypass]);
        Assert.Equal("not-required-native-provider", result.Metadata[GpuProviderMetadataKeys.NativeJsBridge]);
        Assert.Equal("native-abi", result.Metadata[GpuProviderMetadataKeys.PassBridge]);
        Assert.Equal("native-cuda-device-buffer", result.Metadata[GpuProviderMetadataKeys.ZeroCopyBufferHandling]);
        Assert.True(GpuCanonicalValidation.ValidateRev3ExecutionLayerMetadata(result.Metadata).IsValid);
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
        Assert.Equal("true", result.Metadata[GpuProviderMetadataKeys.Rev3]);
        Assert.Equal(GpuBackend.Cuda.ToString(), result.Metadata[GpuProviderMetadataKeys.GpuBackend]);
        Assert.True(GpuCanonicalValidation.ValidateRev3ExecutionLayerMetadata(result.Metadata).IsValid);
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
        Assert.Contains("\"compute.dispatch\"", json);
        Assert.Contains("\"rev3\": \"true\"", json);
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
                "adapter_profile",
                "aot_compiler_hooks",
                "artifact_hash",
                "backend",
                "backend_id",
                "backend_version",
                "deterministic_frame_sampling",
                "device_profile",
                "entry_point",
                "fallback",
                "gpu_backend",
                "gpu_bypass",
                "gpu_capabilities",
                "loader_json",
                "module_id",
                "native_js_bridge",
                "native_module_ref",
                "package_id",
                "pass_bridge",
                "provider_family",
                "provider_role",
                "raw_capture_source",
                "rev3",
                "version",
                "zero_copy_buffer_handling"
            ],
            settings.ToMetadata().Keys.ToArray());

        Assert.True(GpuCanonicalValidation.ValidateRev3ExecutionLayerMetadata(settings.ToMetadata()).IsValid);
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
