using AIKernel.Abstractions.Capabilities;
using AIKernel.Common.Results;
using AIKernel.Dtos.Capabilities;

namespace AIKernel.Providers.CudaCompute;

/// <summary>
/// [EN] Capability module invoker for CUDA compute provider operations.
/// [JA] CUDA compute Provider operation 用の capability module invoker です。
/// </summary>
public sealed class CudaComputeInvoker : ICapabilityModuleInvoker
{
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

        var supported = request.Operation is
            "tensor.matmul" or
            "tensor.softmax" or
            "tensor.conv2d" or
            "tensor.layernorm";
        var metadata = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in request.Metadata.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            metadata[item.Key] = item.Value;
        }

        metadata["provider"] = "CudaComputeProvider";
        metadata["operation"] = request.Operation;
        var unsupported = UnsupportedOperation(supported, request.Operation);

        return ValueTask.FromResult(new CapabilityInvocationResult(
            request.InvocationId,
            request.CapabilityId,
            Succeeded: supported,
            OutputHash: null,
            ErrorCode: unsupported.Match<string?>(() => null, error => error.Code),
            ErrorMessage: unsupported.Match<string?>(() => null, error => error.Message),
            ReplayLogHash: request.ReplayLogHash,
            Metadata: metadata));
    }

    private static Option<MonadicError> UnsupportedOperation(bool supported, string operation)
        => MonadicDecision.ErrorUnless(
            supported,
            "CUDA_OPERATION_NOT_SUPPORTED",
            $"Unsupported CUDA compute operation: {operation}.");
}
