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
        if (!supported)
        {
            return ValueTask.FromResult(new CapabilityInvocationResult(
                request.InvocationId,
                request.CapabilityId,
                Succeeded: false,
                OutputHash: null,
                ErrorCode: unsupported.Match<string?>(() => null, error => error.Code),
                ErrorMessage: unsupported.Match<string?>(() => null, error => error.Message),
                ReplayLogHash: request.ReplayLogHash,
                Metadata: metadata));
        }

        metadata["parenthesized_boolean_expressions"] = "supported";
        var conditionExpression = ResolveConditionExpression(request);
        var validation = ValidateConditionExpression(request.Operation, conditionExpression);
        metadata["condition_expression_grammar"] = validation.Grammar;
        metadata["condition_validation"] = ResolveConditionValidationLabel(validation, conditionExpression);
        if (!string.IsNullOrWhiteSpace(conditionExpression))
        {
            metadata["condition_expression"] = conditionExpression!;
        }

        if (!validation.IsValid)
        {
            return ValueTask.FromResult(new CapabilityInvocationResult(
                request.InvocationId,
                request.CapabilityId,
                Succeeded: false,
                OutputHash: null,
                ErrorCode: "DYNAMIC_PIPELINE_CONDITION_INVALID",
                ErrorMessage: validation.Error,
                ReplayLogHash: request.ReplayLogHash,
                Metadata: metadata));
        }

        return ValueTask.FromResult(new CapabilityInvocationResult(
            request.InvocationId,
            request.CapabilityId,
            Succeeded: true,
            OutputHash: null,
            ErrorCode: null,
            ErrorMessage: null,
            ReplayLogHash: request.ReplayLogHash,
            Metadata: metadata));
    }

    private static Option<MonadicError> UnsupportedOperation(bool supported, string operation)
        => MonadicDecision.ErrorUnless(
            supported,
            "DYNAMIC_PIPELINE_OPERATION_NOT_SUPPORTED",
            $"Unsupported dynamic pipeline operation: {operation}.");

    private static string? ResolveConditionExpression(CapabilityInvocationRequest request)
    {
        var value = FindConditionExpression(request.Arguments);
        return value ?? FindConditionExpression(request.Metadata);
    }

    private static string? FindConditionExpression(IReadOnlyDictionary<string, string> values)
    {
        foreach (var key in new[] { "condition", "expression", "when" })
        {
            if (values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static DynamicPipelineConditionValidationResult ValidateConditionExpression(
        string operation,
        string? conditionExpression)
        => operation is "pipeline.compile" or "pipeline.validate"
            ? DynamicPipelineConditionExpressionValidator.Validate(conditionExpression)
            : DynamicPipelineConditionValidationResult.Valid("not-applicable");

    private static string ResolveConditionValidationLabel(
        DynamicPipelineConditionValidationResult validation,
        string? conditionExpression)
    {
        if (!validation.IsValid)
        {
            return "failed";
        }

        return validation.Grammar == "not-applicable" || string.IsNullOrWhiteSpace(conditionExpression)
            ? "skipped"
            : "passed";
    }
}
