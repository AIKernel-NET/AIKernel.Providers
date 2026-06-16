namespace AIKernel.Providers.Compute;

/// <summary>
/// [EN] Validates backend-neutral compute buffer references.
/// [JA] backend-neutral compute buffer reference を検証します。
/// </summary>
public sealed class ComputeBufferRefValidator
{
    private static readonly HashSet<string> DTypes = new(StringComparer.Ordinal)
    {
        ComputeBufferDTypes.F32,
        ComputeBufferDTypes.F16,
        ComputeBufferDTypes.F64,
        ComputeBufferDTypes.I32,
        ComputeBufferDTypes.I64,
        ComputeBufferDTypes.U8,
        ComputeBufferDTypes.U32
    };

    /// <summary>
    /// [EN] Validates a compute buffer reference.
    /// [JA] compute buffer reference を検証します。
    /// </summary>
    /// <param name="buffer">
    /// [EN] Compute buffer reference.
    /// [JA] compute buffer reference です。
    /// </param>
    /// <returns>
    /// [EN] Structured validation result.
    /// [JA] 構造化された validation result です。
    /// </returns>
    public ComputeBufferRefValidationResult Validate(ComputeBufferRef? buffer)
    {
        if (buffer is null)
        {
            return Failure("COMPUTE_BUFFER_REF_MISSING", "Compute buffer reference is required.");
        }

        if (string.IsNullOrWhiteSpace(buffer.BufferId))
        {
            return Failure("COMPUTE_BUFFER_ID_MISSING", "Compute buffer id is required.");
        }

        if (!DTypes.Contains(buffer.DType))
        {
            return Failure("COMPUTE_BUFFER_DTYPE_UNSUPPORTED", "Compute buffer dtype is not supported.");
        }

        if (!IsShape(buffer.Shape))
        {
            return Failure("COMPUTE_BUFFER_SHAPE_INVALID", "Compute buffer shape must be a comma-separated positive integer list.");
        }

        if (!string.IsNullOrWhiteSpace(buffer.Stride) && !IsShape(buffer.Stride))
        {
            return Failure("COMPUTE_BUFFER_STRIDE_INVALID", "Compute buffer stride must be a comma-separated positive integer list.");
        }

        return new ComputeBufferRefValidationResult { Succeeded = true };
    }

    private static bool IsShape(string value)
        => !string.IsNullOrWhiteSpace(value) &&
           value
               .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
               .All(part => long.TryParse(part, out var number) && number > 0);

    private static ComputeBufferRefValidationResult Failure(
        string code,
        string message)
        => new()
        {
            Succeeded = false,
            ErrorCode = code,
            ErrorMessage = message
        };
}
