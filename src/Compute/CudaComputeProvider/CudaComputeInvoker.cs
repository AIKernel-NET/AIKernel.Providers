using AIKernel.Abstractions.Capabilities;
using AIKernel.Dtos.Capabilities;
using AIKernel.Dtos.Gpu;
using AIKernel.Providers.Compute;

namespace AIKernel.Providers.CudaCompute;

/// <summary>
/// [EN] Capability module invoker for CUDA compute provider operations.
/// [JA] CUDA compute Provider operation 用の capability module invoker です。
/// </summary>
public sealed class CudaComputeInvoker : ICapabilityModuleInvoker
{
    private static readonly CudaComputeSettings DefaultSettings = new();

    /// <summary>
    /// [EN] Invokes a CUDA compute capability module operation.
    /// [JA] CUDA compute capability module operation を実行します。
    /// </summary>
    public ValueTask<CapabilityInvocationResult> InvokeAsync(
        CapabilityInvocationRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);

        var recognized = request.Operation is
            GpuOperationNames.ComputeDispatch or
            GpuOperationNames.ComputeVectorAdd or
            "tensor.matmul" or
            "tensor.softmax" or
            "tensor.conv2d" or
            "tensor.layernorm";
        var metadata = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in request.Metadata.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            metadata[item.Key] = item.Value;
        }

        AddCanonicalExecutionLayerMetadata(metadata);
        metadata["provider"] = "CudaComputeProvider";
        metadata["operation"] = request.Operation;
        var failure = ResolveFailure(recognized, request.Operation);
        metadata["compute.availability_reason"] = failure.AvailabilityReason.ToString();

        return ValueTask.FromResult(new CapabilityInvocationResult(
            request.InvocationId,
            request.CapabilityId,
            Succeeded: false,
            OutputHash: null,
            ErrorCode: failure.Code,
            ErrorMessage: failure.Message,
            ReplayLogHash: request.ReplayLogHash,
            Metadata: metadata));
    }

    private static CudaInvocationFailure ResolveFailure(bool recognized, string operation)
        => recognized
            ? new CudaInvocationFailure(
                "CUDA_BACKEND_NOT_BOUND",
                "CUDA compute operation is recognized, but no descriptor-resolved backend is bound in this provider package.",
                ComputeAvailabilityReason.BackendNotInstalled)
            : new CudaInvocationFailure(
                "CUDA_OPERATION_NOT_SUPPORTED",
                $"Unsupported CUDA compute operation: {operation}.",
                ComputeAvailabilityReason.UnsupportedOperation);

    private static void AddCanonicalExecutionLayerMetadata(IDictionary<string, string> metadata)
    {
        foreach (var item in DefaultSettings.ToMetadata())
        {
            metadata[item.Key] = item.Value;
        }
    }

    private sealed record CudaInvocationFailure(
        string Code,
        string Message,
        ComputeAvailabilityReason AvailabilityReason);
}
