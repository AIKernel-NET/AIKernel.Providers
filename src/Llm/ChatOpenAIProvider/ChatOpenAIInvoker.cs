using AIKernel.Abstractions.Capabilities;
using AIKernel.Common.Results;
using AIKernel.Dtos.Capabilities;

namespace AIKernel.Providers.ChatOpenAI;

/// <summary>
/// [EN] Capability module invoker for ChatOpenAI provider operations.
/// [JA] ChatOpenAI Provider operation 用の capability module invoker です。
/// </summary>
public sealed class ChatOpenAIInvoker : ICapabilityModuleInvoker
{
    /// <summary>
    /// [EN] Invokes a ChatOpenAI capability module operation.
    /// [JA] ChatOpenAI capability module operation を実行します。
    /// </summary>
    public ValueTask<CapabilityInvocationResult> InvokeAsync(
        CapabilityInvocationRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);

        var supported = request.Operation is "chat.completion" or "embedding" or "moderation";
        var metadata = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in request.Metadata.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            metadata[item.Key] = item.Value;
        }

        metadata["provider"] = "ChatOpenAIProvider";
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
            "OPENAI_OPERATION_NOT_SUPPORTED",
            $"Unsupported ChatOpenAI operation: {operation}.");
}
