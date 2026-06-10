using AIKernel.Abstractions.Capabilities;
using AIKernel.Common.Results;
using AIKernel.Dtos.Capabilities;

namespace AIKernel.Providers.ChatHistory;

/// <summary>
/// [EN] Capability module invoker for chat history provider operations.
/// [JA] ChatHistory Provider operation 用の capability module invoker です。
/// </summary>
public sealed class ChatHistoryInvoker : ICapabilityModuleInvoker
{
    /// <summary>
    /// [EN] Invokes a chat history capability module operation.
    /// [JA] ChatHistory capability module operation を実行します。
    /// </summary>
    public ValueTask<CapabilityInvocationResult> InvokeAsync(
        CapabilityInvocationRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);

        var supported = request.Operation is
            "chat.history.read" or
            "chat.history.filter" or
            "chat.history.latest";
        var metadata = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in request.Metadata.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            metadata[item.Key] = item.Value;
        }

        metadata["provider"] = "ChatHistoryProvider";
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
            "CHAT_HISTORY_OPERATION_NOT_SUPPORTED",
            $"Unsupported chat history operation: {operation}.");
}
