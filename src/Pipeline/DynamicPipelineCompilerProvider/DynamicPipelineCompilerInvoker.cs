using AIKernel.Abstractions.Capabilities;
using AIKernel.Common.Results;
using AIKernel.Dtos.Capabilities;

namespace AIKernel.Providers.DynamicPipelineCompiler;

/// <summary>
/// [EN] Capability module invoker for dynamic pipeline compiler provider operations.
/// [JA] Dynamic Pipeline Compiler Provider operation 用の capability module invoker です。
/// </summary>
public sealed class DynamicPipelineCompilerInvoker : ICapabilityModuleInvoker
{
    /// <summary>
    /// [EN] Invokes a dynamic pipeline compiler capability module operation.
    /// [JA] Dynamic Pipeline Compiler capability module operation を実行します。
    /// </summary>
    public ValueTask<CapabilityInvocationResult> InvokeAsync(
        CapabilityInvocationRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);

        var supported = request.Operation is
            "pipeline.compile" or
            "pipeline.execute" or
            "pipeline.validate";
        var metadata = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in request.Metadata.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            metadata[item.Key] = item.Value;
        }

        metadata["provider"] = "DynamicPipelineCompilerProvider";
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
            "DYNAMIC_PIPELINE_OPERATION_NOT_SUPPORTED",
            $"Unsupported dynamic pipeline operation: {operation}.");
}
