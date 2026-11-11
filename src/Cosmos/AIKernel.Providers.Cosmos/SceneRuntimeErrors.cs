namespace AIKernel.Providers.Cosmos;

internal static class SceneRuntimeErrors
{
    // Reasons that can prevent a readonly probe from certifying readiness.
    private static bool ReadinessFailure(string? code) => code is
        "disabled" or "not_probed" or "probe_required" or "probe_timed_out" or "probe_failed"
        or "backend_not_ready" or "inference_capacity" or "backend_busy" or "backend_unavailable" or "backend_oom"
        or "backend_work_unconfirmed" or "model_revision_mismatch" or "endpoint_not_allowed"
        or "deployment_evidence_invalid" or "deployment_mismatch" or "deployment_state_unknown"
        or "runtime_mismatch" or "model_mismatch"
        or "deployment_disabled" or "deployment_loading" or "deployment_warming_up"
        or "deployment_busy" or "deployment_degraded" or "deployment_unavailable";

    internal static string JobCode(string? code) => ReadinessFailure(code) ? code! : code switch
    {
        "unauthenticated" or "permission_denied" or "actor_revision_mismatch"
        or "action_not_authorized" or "target_not_authorized" or "authorization_unavailable"
        or "invalid_request" or "invalid_json" or "duplicate_field" or "invalid_identifier" or "duplicate_identifier"
        or "schema_unsupported" or "mode_unsupported" or "payload_too_large" or "input_limit" or "output_limit"
        or "inconsistent_snapshot" or "catalog_mismatch" or "hash_mismatch" or "invalid_deadline"
        or "media_unsupported" or "media_unavailable" or "media_expired" or "invalid_media" or "media_too_large"
        or "invalid_evidence" or "unknown_actor" or "unknown_target" or "invalid_unknown" or "invalid_region"
        or "duplicate_proposal" or "temporal_evidence_required"
        or "invalid_backend_response" or "response_truncated" or "content_filtered" or "completion_incomplete"
        or "unexpected_tool_call" or "backend_response_too_large" or "backend_request_rejected"
        or "backend_transport_unconfirmed" or "backend_failed_unconfirmed" or "backend_canceled"
        or "observation_deadline" or "clock_unverified" or "freshness_unverified_or_expired" or "stale_context"
        or "context_changed" or "context_invalidated" or "newer_observation" or "caller_canceled"
        or "service_stopping" or "runtime_restarted" or "journal_unavailable" => code,
        _ => "invalid_backend_response"
    };

    internal static SceneAnalysisResult Result(SceneAnalysisResult result)
    {
        // Successful/nonterminal records may legitimately have no reason.
        if (result.ErrorCode is null && result.Status is "accepted" or "queued" or "running" or "completed") return result;
        var safeCode = JobCode(result.ErrorCode);
        return result.ErrorCode == safeCode ? result
            : result with { ErrorCode = safeCode, Proposals = [], FreshnessVerified = false };
    }

    internal static SceneReadiness Readiness(SceneReadiness? result, SceneBackendIdentity expected)
    {
        if (result is null) return new("unavailable", "probe_failed", false, expected);
        if (result.Provider != expected && SceneJson.Write(result.Provider) != SceneJson.Write(expected))
            return new("degraded", "model_revision_mismatch", false, expected);
        if (result.State is not ("disabled" or "loading" or "warming_up" or "ready" or "busy" or "degraded" or "unavailable"))
            return new("unavailable", "probe_failed", false, expected);
        bool knownReason = result.ReasonCode == "ready" || ReadinessFailure(result.ReasonCode);
        if (!knownReason || (result.State == "ready" && (result.ReasonCode != "ready" || !result.DeploymentVerified))
            || (result.State != "ready" && result.ReasonCode == "ready"))
            return new(result.State == "ready" ? "unavailable" : result.State, "probe_failed", false, expected);
        return result with { Provider = expected };
    }
}
