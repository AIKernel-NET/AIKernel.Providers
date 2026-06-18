using AIKernel.Abstractions.Compute;
using AIKernel.Abstractions.Models;
using AIKernel.Abstractions.Providers;
using AIKernel.Common.Results;
using AIKernel.Dtos.Capabilities;
using AIKernel.Dtos.Core;
using AIKernel.Dtos.Routing;
using AIKernel.Providers.Compute;

namespace AIKernel.Providers.CudaCompute;

/// <summary>
/// [EN] Official AIKernel external provider boundary for descriptor-driven CUDA compute modules.
/// [JA] descriptor-driven CUDA compute module 向けの AIKernel 公式外部 Provider 境界です。
/// </summary>
public sealed class CudaComputeProvider(
    CudaComputeSettings settings) : IProvider, IComputeProvider
{
    private static readonly CudaComputeProviderCapabilities Capabilities = new();
    private readonly CudaComputeSettings _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    private volatile bool _initialized;

    /// <summary>
    /// [EN] Initializes the provider with default settings for dynamic loading.
    /// [JA] dynamic loading 用の default settings で Provider を初期化します。
    /// </summary>
    public CudaComputeProvider()
        : this(new CudaComputeSettings())
    {
    }

    /// <summary>
    /// [EN] Provides the ProviderId provider contract member.
    /// [JA] ProviderId の provider contract member を提供します。
    /// </summary>
    public string ProviderId => _settings.ProviderId;

    /// <summary>
    /// [EN] Provides the Name provider contract member.
    /// [JA] Name の provider contract member を提供します。
    /// </summary>
    public string Name => _settings.Name;

    /// <summary>
    /// [EN] Provides the Version provider contract member.
    /// [JA] Version の provider contract member を提供します。
    /// </summary>
    public string Version => _settings.Version;

    /// <summary>
    /// [EN] Provides the GetCapabilities provider contract member.
    /// [JA] GetCapabilities の provider contract member を提供します。
    /// </summary>
    public IProviderCapabilities GetCapabilities() => Capabilities;

    /// <summary>
    /// [EN] Provides the IsAvailableAsync provider contract member.
    /// [JA] IsAvailableAsync の provider contract member を提供します。
    /// </summary>
    public Task<bool> IsAvailableAsync() => Task.FromResult(_initialized);

    /// <summary>
    /// [EN] Provides the InitializeAsync provider contract member.
    /// [JA] InitializeAsync の provider contract member を提供します。
    /// </summary>
    public Task InitializeAsync()
    {
        _initialized = true;
        return Task.CompletedTask;
    }

    /// <summary>
    /// [EN] Provides the ShutdownAsync provider contract member.
    /// [JA] ShutdownAsync の provider contract member を提供します。
    /// </summary>
    public Task ShutdownAsync()
    {
        _initialized = false;
        return Task.CompletedTask;
    }

    /// <summary>
    /// [EN] Provides the GetHealthAsync provider contract member.
    /// [JA] GetHealthAsync の provider contract member を提供します。
    /// </summary>
    public Task<ProviderHealthStatus> GetHealthAsync()
        => Task.FromResult(new ProviderHealthStatus(
            _initialized,
            MonadicDecision.SelectText(_initialized, "Provider is not initialized.", "Provider initialized."),
            DateTime.UtcNow,
            0));

    /// <summary>
    /// [EN] Creates a provider capability descriptor from the current settings.
    /// [JA] 現在の設定から provider capability descriptor を作成します。
    /// </summary>
    public CapabilityModuleDescriptor ToCapabilityDescriptor()
        => CudaComputeCapabilityContracts.ToContract(
            new CudaComputeCapabilityDescriptor(
                ProviderId,
                _settings.DeviceProfile,
                _settings.ToMetadata()));

    /// <summary>
    /// [EN] Creates a descriptor-only CUDA backend boundary from the current settings.
    /// [JA] 現在の設定から descriptor-only CUDA backend 境界を作成します。
    /// </summary>
    public CudaBackendDescriptor ToBackendDescriptor()
        => new()
        {
            ProviderId = ProviderId,
            BackendId = _settings.BackendId,
            BackendVersion = _settings.BackendVersion,
            PackageId = _settings.PackageId,
            DeviceProfile = _settings.DeviceProfile,
            SupportedComputeCapabilities = _settings.SupportedComputeCapabilities,
            NativeModule = new NativeModuleDescriptor
            {
                BackendId = _settings.BackendId,
                ModuleId = _settings.ModuleId,
                ModuleRef = _settings.NativeModuleRef,
                AbiVersion = _settings.AbiVersion,
                EntryPoint = _settings.EntryPoint,
                Hash = HashMetadata.FromExpression(_settings.ArtifactHash)
            },
            SupportedOps = Capabilities.SupportedOperations,
            MaxDeviceMemoryBytes = _settings.MaxDeviceMemoryBytes,
            Metadata = _settings.ToMetadata()
        };

    /// <summary>[EN] Returns whether CUDA compute is available. [JA] CUDA compute が利用可能かどうかを返します。</summary>
    public bool IsAvailable() => _initialized;

    /// <summary>[EN] Creates a compute buffer for CUDA-bound hosts. [JA] CUDA-bound host 向けの compute buffer を作成します。</summary>
    public Task<ComputeBuffer> CreateBufferAsync(int size)
        => Task.FromResult(new ComputeBuffer(size));

    /// <summary>[EN] Writes bytes into a compute buffer. [JA] compute buffer へ byte を書き込みます。</summary>
    public Task WriteBufferAsync(ComputeBuffer buffer, ReadOnlyMemory<byte> data)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        buffer.Write(data);
        return Task.CompletedTask;
    }

    /// <summary>[EN] Reads bytes from a compute buffer. [JA] compute buffer から byte を読み取ります。</summary>
    public Task ReadBufferAsync(ComputeBuffer buffer, Memory<byte> destination)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        buffer.Read(destination);
        return Task.CompletedTask;
    }

    /// <summary>[EN] Fails closed until the native CUDA backend is bound. [JA] native CUDA backend が bind されるまで fail-closed します。</summary>
    public Task ExecuteKernelAsync(ComputeKernel kernel, params ComputeBuffer[] buffers)
    {
        throw new InvalidOperationException("CUDA native kernel dispatch is not bound in this provider package. ErrorCode=CUDA_NATIVE_BACKEND_NOT_BOUND");
    }
}

internal sealed class CudaComputeProviderCapabilities : IProviderCapabilities
{
    private static readonly string[] Operations =
    [
        "tensor.matmul",
        "tensor.softmax",
        "tensor.conv2d",
        "tensor.layernorm"
    ];

    private static readonly string[] DataTypes =
    [
        "tensor",
        "float16",
        "float32"
    ];

    /// <summary>
    /// [EN] Provides the SupportedOperations public provider contract surface.
    /// [JA] SupportedOperations の public provider contract surface を提供します。
    /// </summary>
    public IReadOnlyList<string> SupportedOperations => Operations;

    /// <summary>
    /// [EN] Provides the SupportedDataTypes public provider contract surface.
    /// [JA] SupportedDataTypes の public provider contract surface を提供します。
    /// </summary>
    public IReadOnlyList<string> SupportedDataTypes => DataTypes;

    /// <summary>
    /// [EN] Provides the MaxConcurrentConnections public provider contract surface.
    /// [JA] MaxConcurrentConnections の public provider contract surface を提供します。
    /// </summary>
    public int MaxConcurrentConnections => 1;

    /// <summary>
    /// [EN] Provides the RateLimit public provider contract surface.
    /// [JA] RateLimit の public provider contract surface を提供します。
    /// </summary>
    public RateLimitInfo? RateLimit => null;

    /// <summary>
    /// [EN] Provides the Vector public provider contract surface.
    /// [JA] Vector の public provider contract surface を提供します。
    /// </summary>
    public ModelCapacityVector Vector => new();

    /// <summary>
    /// [EN] Provides the GetDynamicCapacities public provider contract surface.
    /// [JA] GetDynamicCapacities の public provider contract surface を提供します。
    /// </summary>
    public IDictionary<string, float>? GetDynamicCapacities(
        IExecutionConstraints constraints)
        => null;

    /// <summary>
    /// [EN] Provides the GetCapabilityProfile public provider contract surface.
    /// [JA] GetCapabilityProfile の public provider contract surface を提供します。
    /// </summary>
    public ICapabilityProfile? GetCapabilityProfile() => null;

    /// <summary>
    /// [EN] Provides the SupportsOperation public provider contract surface.
    /// [JA] SupportsOperation の public provider contract surface を提供します。
    /// </summary>
    public bool SupportsOperation(string operation)
        => Operations.Contains(operation, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// [EN] Provides the SupportsDataType public provider contract surface.
    /// [JA] SupportsDataType の public provider contract surface を提供します。
    /// </summary>
    public bool SupportsDataType(string dataType)
        => DataTypes.Contains(dataType, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// [EN] Provides the SupportsQuantization public provider contract surface.
    /// [JA] SupportsQuantization の public provider contract surface を提供します。
    /// </summary>
    public bool SupportsQuantization(string quantizationLevel) => false;

    /// <summary>
    /// [EN] Provides the SupportsQueryAugmentation public provider contract surface.
    /// [JA] SupportsQueryAugmentation の public provider contract surface を提供します。
    /// </summary>
    public bool SupportsQueryAugmentation => false;

    /// <summary>
    /// [EN] Provides the SupportsQueryDecomposition public provider contract surface.
    /// [JA] SupportsQueryDecomposition の public provider contract surface を提供します。
    /// </summary>
    public bool SupportsQueryDecomposition => false;

    /// <summary>
    /// [EN] Provides the SupportsQueryRouting public provider contract surface.
    /// [JA] SupportsQueryRouting の public provider contract surface を提供します。
    /// </summary>
    public bool SupportsQueryRouting => false;

    /// <summary>
    /// [EN] Provides the MaxQueryParts public provider contract surface.
    /// [JA] MaxQueryParts の public provider contract surface を提供します。
    /// </summary>
    public int MaxQueryParts => 0;

    /// <summary>
    /// [EN] Provides the SupportedQueryProcessingOperations public provider contract surface.
    /// [JA] SupportedQueryProcessingOperations の public provider contract surface を提供します。
    /// </summary>
    public IReadOnlyList<string> SupportedQueryProcessingOperations => [];

    /// <summary>
    /// [EN] Provides the SupportsQueryProcessingOperation public provider contract surface.
    /// [JA] SupportsQueryProcessingOperation の public provider contract surface を提供します。
    /// </summary>
    public bool SupportsQueryProcessingOperation(string operation) => false;

    /// <summary>
    /// [EN] Provides the SupportsEmbedding public provider contract surface.
    /// [JA] SupportsEmbedding の public provider contract surface を提供します。
    /// </summary>
    public bool SupportsEmbedding => false;

    /// <summary>
    /// [EN] Provides the EmbeddingDimensions public provider contract surface.
    /// [JA] EmbeddingDimensions の public provider contract surface を提供します。
    /// </summary>
    public int? EmbeddingDimensions => null;

    /// <summary>
    /// [EN] Provides the SupportedEmbeddingModels public provider contract surface.
    /// [JA] SupportedEmbeddingModels の public provider contract surface を提供します。
    /// </summary>
    public IReadOnlyList<string> SupportedEmbeddingModels => [];
}
