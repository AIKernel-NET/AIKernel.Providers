namespace AIKernel.Providers.Cosmos;

// Host operations have a separate, exact allowlist from caller admission and jobs.
internal static class SceneHostErrors
{
    internal static Exception Sanitize(Exception error, string fallback = "host_operation_failed", CancellationToken token = default)
    {
        if (error is OperationCanceledException canceled) return SceneIntakeErrors.Cancellation(canceled, token);
        var scene = error as SceneException;
        var code = scene?.Code is "invalid_identifier" or "request_not_found" or "disabled" or "backend_still_active"
            or "journal_invalid" or "journal_capacity" or "journal_unavailable" or "deployment_evidence_invalid"
            or "host_operation_failed" ? scene.Code : fallback;
        // This public flag is safety evidence, unlike arbitrary exception Data.
        return new SceneException(code, scene?.WorkMayStillBeRunning == true);
    }

    internal static Exception Configuration(Exception error, string parameter)
        => error is OperationCanceledException canceled ? SceneIntakeErrors.Cancellation(canceled)
            : new ArgumentException("Invalid scene host configuration.", parameter);
}
