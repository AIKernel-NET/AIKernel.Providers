namespace AIKernel.Providers.Cosmos;

// Exact, ordinal allowlists. Never infer a public code from a prefix, exception
// message, type name, backend response, or a caller-configurable dictionary.
internal static class SceneIntakeErrors
{
    internal static string Code(string? code) => code switch
    {
        "capability_unsupported" or "operation_unsupported" or "unknown_argument"
        or "unauthenticated" or "permission_denied" or "authorization_unavailable"
        or "action_not_authorized" or "target_not_authorized"
        or "invalid_request" or "invalid_json" or "duplicate_field" or "invalid_identifier"
        or "duplicate_identifier" or "schema_unsupported" or "mode_unsupported"
        or "hash_mismatch" or "inconsistent_snapshot" or "actor_revision_mismatch" or "catalog_mismatch"
        or "invalid_deadline" or "future_capture" or "observation_expired" or "stale_context"
        or "request_id_conflict" or "request_not_found"
        or "media_unsupported" or "media_unavailable" or "media_expired" or "invalid_media"
        or "payload_too_large" or "input_limit" or "media_too_large" or "media_capacity" or "media_busy"
        or "job_capacity" or "caller_capacity" or "session_capacity"
        or "backend_work_unconfirmed" or "disabled" or "journal_unavailable" => code,
        _ => "invalid_request"
    };

    internal static Exception Authorization(Exception error)
    {
        if (error is OperationCanceledException canceled) return Cancellation(canceled);
        var code = error is SceneException known ? known.Code : null;
        return new SceneException(code is "unauthenticated" or "permission_denied" or "actor_revision_mismatch"
            or "action_not_authorized" or "target_not_authorized" or "authorization_unavailable"
            ? code : "authorization_unavailable");
    }

    internal static Exception Sanitize(Exception error, CancellationToken token = default)
        => error is OperationCanceledException canceled ? Cancellation(canceled, token)
            : new SceneException(Code(error is SceneException known ? known.Code : null));

    // Keep cancellation semantics/type, but never retain the host's task,
    // message, inner exception, Data, or internal cancellation token.
    internal static OperationCanceledException Cancellation(OperationCanceledException error, CancellationToken token = default)
        => error is TaskCanceledException ? new TaskCanceledException("Scene request canceled.", null, token)
            : new OperationCanceledException("Scene request canceled.", null, token);
}
