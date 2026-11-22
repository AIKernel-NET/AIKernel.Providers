using System.Security.Claims;
using AIKernel.Abstractions.Capabilities;
using AIKernel.Dtos.Capabilities;

namespace AIKernel.Providers.Cosmos;

public sealed class SceneService : ISceneService
{
    public const string CapabilityId = "vision.scene.reasoning";
    public const string ResultMetadataKey = "scene.result_json";
    private readonly SceneOptions _options;
    private readonly ISceneReasonerBackend _backend;
    private readonly ISceneAuthorizer _authorizer;
    private readonly TimeProvider _clock;
    private readonly SceneMediaStore _media;
    private readonly SceneJournal _journal;
    private readonly object _sync = new();
    private readonly Dictionary<string, Job> _jobs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ContextGeneration> _generations = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SceneRequest> _latest = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> _retiredEpochs = new(StringComparer.Ordinal);
    private bool _quarantined;
    private bool _disposed;
    private Task? _shutdown;
    private int _active;
    private SceneReadiness _readiness;
    private DateTimeOffset _probedAt;

    // A fingerprint identifies equal settings; an incarnation identifies a
    // continuous lifetime of those settings. Returning to A after B must not
    // make results from the earlier A lifetime current again.
    private sealed record ContextGeneration(string Fingerprint, string Incarnation);

    private sealed class Job
    {
        internal required SceneJournalEntry Entry;
        internal string Caller = "";
        internal SceneRequest? Request;
        // In-memory credentials only; never serialized into the job journal.
        internal ClaimsPrincipal? Principal;
        internal TaskCompletionSource? DispatchRegistered;
        internal long Start;
        internal TimeSpan Budget;
        internal TimeSpan FreshnessBudget;
        internal bool HasMonotonicOrigin;
        internal bool ClockVerified;
        internal bool Active;
        internal bool InferenceFinished;
        internal bool UnknownPhysical;
        internal ITimer? Timer;
        internal Task Work = Task.CompletedTask;
    }

    public SceneService(SceneOptions options, ISceneReasonerBackend backend, ISceneAuthorizer authorizer, TimeProvider? timeProvider = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        _authorizer = authorizer ?? throw new ArgumentNullException(nameof(authorizer));
        _clock = timeProvider ?? TimeProvider.System;
        if (options.MaxFrames is < 1 or > 32 || options.MaxActors is < 1 or > 256 || options.MaxProposals is < 1 or > 128
            || options.MaxRequestBytes is < 1024 or > 1048576 || options.MaxResponseBytes is < 1024 or > 1048576
            || options.MaxImageBytes is < 64 or > 16777216 || options.MaxImagePixels is < 1 or > 16777216
            || options.MaxStoredMediaBytes < options.MaxImageBytes || options.MaxStoredMediaBytes > 1073741824
            || options.MaxJobs is < 1 or > 1000 || options.MaxConcurrentInference is < 1 or > 16
            || options.MaxConcurrentUploads is < 1 or > 16
            || options.MaxRunningPerCaller is < 1 or > 4 || options.MaxPendingPerCaller is < 1 or > 8
            || options.MaxObservationAgeMs is < 1 or > 600000 || options.MediaRetention <= TimeSpan.Zero
            || options.MediaRetention > TimeSpan.FromHours(1) || options.IdempotencyRetention < TimeSpan.FromMinutes(10)
            || options.IdempotencyRetention > TimeSpan.FromDays(7) || options.ReadinessMaxAge <= TimeSpan.Zero)
            throw new ArgumentException("Invalid bounded scene service options.", nameof(options));
        _media = new(options, _clock);
        try { _readiness = new(options.Enabled ? "unavailable" : "disabled", "not_probed", false, backend.Identity); }
        catch (Exception error) { throw SceneHostErrors.Sanitize(error); }
        try { _journal = new(options.JournalDirectory, options.MaxJobs, options.MaxResponseBytes); }
        catch (Exception error) { throw SceneHostErrors.Sanitize(error, "journal_unavailable"); }
        try
        {
            _quarantined = _journal.RecoveryRequired;
            foreach (var stored in _journal.Load())
            {
                if (stored.RetainUntilUtc <= _clock.GetUtcNow() && !stored.PhysicalOutstanding && stored.Result.Status != "running") continue;
                var entry = stored with { Result = SceneRuntimeErrors.Result(stored.Result) };
                if (entry.PhysicalOutstanding) _quarantined = true;
                if (entry.Result.Status is "running" or "queued" or "accepted")
                {
                    if (entry.Result.Status == "running") _quarantined = true;
                    entry = entry with { Result = entry.Result with { Status = "interrupted", ErrorCode = "runtime_restarted", FreshnessVerified = false, Proposals = [] } };
                }
                if (_jobs.Count >= options.MaxJobs || !_jobs.TryAdd(entry.Key, new() { Entry = entry })) throw new SceneException("journal_invalid");
            }
            Persist();
        }
        catch (Exception error)
        {
            try { _journal.Dispose(); } catch { /* Preserve the sanitized initialization failure. */ }
            throw SceneHostErrors.Sanitize(error, "journal_unavailable");
        }
    }

    ISceneSession ISceneService.OpenSession(ClaimsPrincipal authenticatedCaller) => OpenSession(authenticatedCaller);

    public SceneSession OpenSession(ClaimsPrincipal authenticatedCaller)
    {
        try
        {
            lock (_sync) { CheckNotDisposed(); }
            var principal = new ClaimsPrincipal(authenticatedCaller.Identities.Select(identity => new ClaimsIdentity(identity)));
            var grant = Authorize(principal);
            lock (_sync) { CheckNotDisposed(); return new(this, principal, grant.CallerId); }
        }
        catch (Exception error) { throw SceneIntakeErrors.Sanitize(error); }
    }

    private SceneGrant Authorize(ClaimsPrincipal principal, string? expectedCaller = null)
    {
        try
        {
            if (principal.Identity?.IsAuthenticated != true) throw new SceneException("unauthenticated");
            var grant = _authorizer.Authorize(principal) ?? throw new SceneException("permission_denied");
            SceneJson.Id(grant.CallerId); SceneJson.Id(grant.PolicyRevision); SceneJson.Id(grant.PermissionScope);
            if (expectedCaller is not null && grant.CallerId != expectedCaller) throw new SceneException("permission_denied");
            return grant with
            {
                ActorRevisions = grant.ActorRevisions.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal),
                ActionIds = new HashSet<string>(grant.ActionIds, StringComparer.Ordinal),
                TargetIds = new HashSet<string>(grant.TargetIds, StringComparer.Ordinal)
            };
        }
        catch (Exception error) { throw SceneIntakeErrors.Authorization(error); }
    }

    internal Task<SceneMediaReceipt> RegisterAsync(ClaimsPrincipal principal, string caller, SceneMediaRegistration registration,
        Stream source, CancellationToken cancellationToken)
    {
        return Task.Run(async () =>
        {
            try
            {
                lock (_sync) { CheckNotDisposed(); }
                var grant = Authorize(principal, caller);
                lock (_sync) { CheckEnabled(); }
                return await _media.RegisterAsync(grant, registration, source, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception error) { throw SceneIntakeErrors.Sanitize(error, cancellationToken); }
        }, cancellationToken);
    }

    // Calling this probe never starts/restarts a model or changes GPU placement.
    public async ValueTask<SceneReadiness> ProbeAsync(CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            if (_disposed || !_options.Enabled) return DisabledReadiness();
            if (_quarantined) return _readiness with { State = "degraded", ReasonCode = "backend_work_unconfirmed", DeploymentVerified = false };
        }
        try
        {
            var result = await _backend.ProbeAsync(cancellationToken).ConfigureAwait(false);
            lock (_sync)
            {
                if (_disposed) return DisabledReadiness();
                if (_quarantined) return _readiness with { State = "degraded", ReasonCode = "backend_work_unconfirmed", DeploymentVerified = false };
                var readiness = SceneRuntimeErrors.Readiness(result, _backend.Identity);
                var probedAt = _clock.GetUtcNow();
                if (_disposed) return DisabledReadiness();
                if (_quarantined) return _readiness with { State = "degraded", ReasonCode = "backend_work_unconfirmed", DeploymentVerified = false };
                _readiness = readiness;
                _probedAt = probedAt;
                return _readiness;
            }
        }
        catch (OperationCanceledException canceled) { throw SceneIntakeErrors.Cancellation(canceled, cancellationToken); }
        catch
        {
            lock (_sync)
            {
                if (_disposed) return DisabledReadiness();
                if (_quarantined) return _readiness with { State = "degraded", ReasonCode = "backend_work_unconfirmed", DeploymentVerified = false };
                var failed = new SceneReadiness("unavailable", "probe_failed", false, _readiness.Provider);
                // Revoke cached approval before consulting a host clock that
                // can itself fail, cancel, or reenter service operations.
                _readiness = failed;
                _probedAt = default;
                var failedAt = default(DateTimeOffset);
                try { failedAt = _clock.GetUtcNow(); }
                catch (OperationCanceledException canceled) { throw SceneIntakeErrors.Cancellation(canceled, cancellationToken); }
                catch { /* A missing timestamp cannot turn probe failure into readiness. */ }
                if (_disposed) return DisabledReadiness();
                if (_quarantined) return _readiness with { State = "degraded", ReasonCode = "backend_work_unconfirmed", DeploymentVerified = false };
                _readiness = failed;
                _probedAt = failedAt;
                return failed;
            }
        }
    }

    // Operational API, intentionally absent from the caller capability surface.
    // The host must establish that previous backend work has actually stopped.
    public void ConfirmBackendQuiescent()
    {
        try
        {
            lock (_sync)
            {
                CheckNotDisposed();
                if (_active != 0) throw new SceneException("backend_still_active");
                // Prepare recovery without changing the live entries. A failed save
                // must leave leases intact for later host and background writes.
                var readiness = new SceneReadiness("unavailable", "probe_required", false, _backend.Identity);
                CheckNotDisposed(); // Host identity lookup may reenter shutdown.
                var recovered = _jobs.Values.Select(job =>
                    (Job: job, Entry: job.Entry with { PhysicalOutstanding = false })).ToArray();
                Persist(recovered.Select(update => update.Entry), confirmRecovery: true);
                // Publish only after the complete snapshot is durable, under the
                // same lock as other writers. No host callbacks follow the save.
                foreach (var update in recovered) update.Job.Entry = update.Entry;
                _quarantined = false;
                _readiness = readiness;
            }
        }
        catch (Exception error) { throw SceneHostErrors.Sanitize(error); }
    }

    // Trusted host API, intentionally absent from SceneSession/capabilities.
    // Notify an accepted request's revocation before restoring its authority.
    // This does not require the revoked caller to remain authorized to cancel.
    public void RevokeRequestAuthorization(string callerId, string requestId)
    {
        try
        {
            SceneJson.Id(callerId); SceneJson.Id(requestId);
            lock (_sync)
            {
                CheckNotDisposed();
                if (!_jobs.TryGetValue(Key(callerId, requestId), out var job)
                    || job.Entry.RetainUntilUtc <= _clock.GetUtcNow()) throw new SceneException("request_not_found");
                RevokeIfUnauthorized(job, null);
                // Also retry durability for an already-terminal in-memory record.
                // A failed journal write must never be acknowledged as persisted.
                Persist();
            }
        }
        catch (Exception error) { throw SceneHostErrors.Sanitize(error); }
    }

    internal async ValueTask<CapabilityInvocationResult> InvokeAsync(ClaimsPrincipal principal, string caller, CapabilityInvocationRequest envelope, CancellationToken token)
    {
        // Validation, hashing, and durable journal writes run away from the UI caller.
        return await Task.Run(() => InvokeCore(principal, caller, envelope, token), token).ConfigureAwait(false);
    }

    private CapabilityInvocationResult InvokeCore(ClaimsPrincipal principal, string caller, CapabilityInvocationRequest envelope, CancellationToken token)
    {
        try
        {
            token.ThrowIfCancellationRequested();
            lock (_sync) { CheckNotDisposed(); }
            SceneGrant grant;
            try { grant = Authorize(principal, caller); }
            catch (Exception error)
            {
                // The session identity was authenticated when opened. Failure
                // to obtain authority must not let a known request regain its
                // eligibility merely because authority returns before completion.
                if (!token.IsCancellationRequested) RevokeLookup(caller, envelope, null, error);
                throw;
            }
            lock (_sync) { CheckNotDisposed(); }
            RevokeLookup(caller, envelope, grant);
            if (envelope.CapabilityId != CapabilityId) throw new SceneException("capability_unsupported");
            SceneJson.Id(envelope.InvocationId);
            if (envelope.Arguments is null || envelope.Metadata is null) throw new SceneException("invalid_request");
            if (envelope.Operation == "scene.capabilities")
            {
                if (envelope.Arguments.Count != 0) throw new SceneException("unknown_argument");
                return Reply(envelope, Catalog(), true);
            }
            if (envelope.Operation == "scene.invalidate")
            {
                if (envelope.Arguments.Count != 0) throw new SceneException("unknown_argument");
                lock (_sync)
                {
                    CheckNotDisposed();
                    var fingerprint = _generations.TryGetValue(grant.CallerId, out var prior) ? prior.Fingerprint : "";
                    _generations[grant.CallerId] = new(fingerprint, Guid.NewGuid().ToString("N"));
                    if (_latest.TryGetValue(grant.CallerId, out var latest)) Retire(grant.CallerId, latest.SessionEpoch);
                    foreach (var job in _jobs.Values.Where(j => j.Caller == grant.CallerId && !Terminal(j.Entry.Result.Status)).ToArray())
                        Finish(job, "superseded", "context_invalidated");
                    Pump();
                }
                return Reply(envelope, new { status = "invalidated", analysisOnly = true }, true);
            }
            if (envelope.Operation == "scene.analyze")
            {
                if (envelope.Arguments.Count != 1 || !envelope.Arguments.TryGetValue("request_json", out var json)) throw new SceneException("unknown_argument");
                var request = SceneJson.Read<SceneRequest>(json, _options.MaxRequestBytes);
                var inputHash = SceneJson.Hash(json);
                if (envelope.InputHash is not null && envelope.InputHash != inputHash) throw new SceneException("hash_mismatch");
                SceneJson.Validate(request, grant, _options);
                var key = Key(grant, request.RequestId);
                lock (_sync)
                {
                    CheckEnabled();
                    Purge();
                    if (_jobs.TryGetValue(key, out var previous))
                    {
                        if (previous.Entry.InputHash != inputHash) throw new SceneException("request_id_conflict");
                        return Reply(envelope, Present(previous, grant), true);
                    }
                    var (budget, freshnessBudget, verified, age) = Deadline(request, grant);
                    if (_jobs.Count >= _options.MaxJobs) throw new SceneException("job_capacity");
                    _media.Resolve(request, grant, copy: false);
                    token.ThrowIfCancellationRequested();
                    var generation = UpdateGeneration(request, grant);
                    var waiting = _jobs.Values.Where(j => j.Caller == grant.CallerId && j.Entry.Result.Status == "queued").OrderBy(j => j.Start).ToArray();
                    foreach (var old in waiting.Take(Math.Max(0, waiting.Length - _options.MaxPendingPerCaller + 1)))
                        Finish(old, "superseded", "newer_observation");
                    var initial = new SceneAnalysisResult("scene-analysis.v1", request.RequestId, request.SessionEpoch,
                        request.SnapshotId, "queued", true, false, null, request.SceneRevision, request.LayoutRevision,
                        request.CameraId, request.PolicyRevision, request.CapturedAtUtc,
                        request.CapturedAtUtc.AddMilliseconds(request.MaxObservationAgeMs), [], [], _backend.Identity,
                        new(0, 0, 0, 0, age), "not_requested", request.Mode);
                    var job = new Job
                    {
                        Entry = new(key, SceneJson.Scope(grant), inputHash, generation,
                            _clock.GetUtcNow() + _options.IdempotencyRetention, initial),
                        Caller = grant.CallerId, Request = request,
                        Principal = new ClaimsPrincipal(principal.Identities.Select(identity => new ClaimsIdentity(identity))),
                        Start = _clock.GetTimestamp(), Budget = budget,
                        FreshnessBudget = freshnessBudget, HasMonotonicOrigin = true, ClockVerified = verified
                    };
                    _jobs.Add(key, job);
                    try
                    {
                        Persist(); // Durable identity is established before any inference dispatch.
                        job.Timer = _clock.CreateTimer(_ => Expire(job), null, budget, Timeout.InfiniteTimeSpan);
                        Pump();
                    }
                    catch (SceneException error) when (error.Code == "journal_unavailable")
                    {
                        // Never dispatch an intake whose durable acceptance or
                        // reservation failed, even after explicit host recovery.
                        if (!job.Active && !Terminal(job.Entry.Result.Status))
                        {
                            job.Timer?.Dispose();
                            job.Entry = job.Entry with { Result = job.Entry.Result with { Status = "failed", ErrorCode = "journal_unavailable" } };
                            job.Request = null; job.Principal = null;
                        }
                        throw;
                    }
                    return Reply(envelope, Present(job, grant), true);
                }
            }
            if (envelope.Operation is "scene.status" or "scene.cancel")
            {
                if (envelope.Arguments.Count != 1 || !envelope.Arguments.TryGetValue("requestId", out var id)) throw new SceneException("unknown_argument");
                SceneJson.Id(id);
                lock (_sync)
                {
                    CheckNotDisposed();
                    if (!_jobs.TryGetValue(Key(grant, id), out var job)
                        || job.Entry.RetainUntilUtc <= _clock.GetUtcNow()) throw new SceneException("request_not_found");
                    RevokeIfUnauthorized(job, grant);
                    if (job.Entry.Scope != SceneJson.Scope(grant)) throw new SceneException("request_not_found");
                    if (envelope.Operation == "scene.cancel" && !Terminal(job.Entry.Result.Status))
                    { Finish(job, "canceled", "caller_canceled"); Pump(); }
                    return Reply(envelope, Present(job, grant), true);
                }
            }
            throw new SceneException("operation_unsupported");
        }
        catch (OperationCanceledException canceled) { throw SceneIntakeErrors.Cancellation(canceled, token); }
        catch (SceneException ex) { return Failure(envelope, ex.Code); }
        catch (Exception) { return Failure(envelope, "invalid_request"); }
    }

    private static string Key(SceneGrant grant, string id) => Key(grant.CallerId, id);
    private static string Key(string caller, string id) => SceneJson.Hash(SceneJson.Write(new { CallerId = caller, requestId = id }));

    private void RevokeLookup(string caller, CapabilityInvocationRequest envelope, SceneGrant? grant, Exception? error = null)
    {
        string id;
        string? inputHash = null;
        // Validate only the lookup identity here. A denied or malformed request
        // must neither reveal a record nor invalidate a different input/owner.
        try
        {
            if (envelope.CapabilityId != CapabilityId || envelope.Arguments is null || envelope.Metadata is null) return;
            SceneJson.Id(envelope.InvocationId);
            if (envelope.Arguments.Count != 1) return;
            if (envelope.Operation is "scene.status" or "scene.cancel")
            {
                if (!envelope.Arguments.TryGetValue("requestId", out id!)) return;
            }
            else if (envelope.Operation == "scene.analyze")
            {
                if (!envelope.Arguments.TryGetValue("request_json", out var json)) return;
                id = SceneJson.Read<SceneRequest>(json, _options.MaxRequestBytes).RequestId;
                inputHash = SceneJson.Hash(json);
                if (envelope.InputHash is not null && envelope.InputHash != inputHash) return;
            }
            else return;
            SceneJson.Id(id);
        }
        catch { return; }
        lock (_sync)
        {
            if (_disposed) throw new SceneException("disabled");
            if (!_jobs.TryGetValue(Key(caller, id), out var job) || job.Entry.RetainUntilUtc <= _clock.GetUtcNow()
                || (inputHash is not null && inputHash != job.Entry.InputHash)) return;
            RevokeIfUnauthorized(job, grant, error);
        }
    }

    // Called under _sync, including at the final result projection. Persist
    // before acknowledging revocation; older concurrent grants cannot undo it.
    private void RevokeIfUnauthorized(Job job, SceneGrant? grant, Exception? error = null)
    {
        var result = job.Entry.Result;
        if (!Terminal(result.Status))
        {
            try
            {
                if (error is not null) throw error;
                ValidateAuthority(job, grant);
                return;
            }
            catch (Exception failure)
            {
                var (state, reason) = AuthorizationFailure(failure);
                Finish(job, state, reason);
                return;
            }
        }
        if (result.Status != "completed" || result.Proposals.Count == 0) return;
        if (grant is not null && job.Entry.Scope == SceneJson.Scope(grant) && result.Proposals.All(p =>
            grant.ActorRevisions.TryGetValue(p.ActorId, out var revision) && revision == p.AssetRevision
            && grant.ActionIds.Contains(p.ActionId) && grant.TargetIds.Contains(p.TargetId))) return;
        job.Entry = job.Entry with { Result = result with { Proposals = [], FreshnessVerified = false, ErrorCode = result.ErrorCode ?? "stale_context" } };
        Persist();
    }
    private static bool Terminal(string state) => state is not ("accepted" or "queued" or "running");
    // Called under _sync. Stopping is already closed to new callers, while
    // accepted physical work retains journal ownership until shutdown drains.
    private void CheckNotDisposed()
    {
        if (_disposed) throw new SceneException("disabled");
    }
    private SceneReadiness DisabledReadiness() => _readiness with
    { State = "disabled", ReasonCode = "disabled", DeploymentVerified = false, RetryAfterMs = null };
    private void CheckEnabled()
    {
        if (_disposed || !_options.Enabled) throw new SceneException("disabled");
        if (_quarantined) throw new SceneException("backend_work_unconfirmed");
    }

    private (TimeSpan Budget, TimeSpan FreshnessBudget, bool Verified, double Age) Deadline(SceneRequest request, SceneGrant grant)
    {
        var now = _clock.GetUtcNow();
        var calibration = grant.ClockCalibration;
        bool known = calibration is not null && calibration.ValidUntilUtc > now && calibration.Uncertainty >= TimeSpan.Zero
            && calibration.Uncertainty <= TimeSpan.FromMilliseconds(250);
        var age = (now - (request.CapturedAtUtc + (known ? calibration!.Offset : TimeSpan.Zero))).TotalMilliseconds;
        var uncertainty = known ? calibration!.Uncertainty.TotalMilliseconds : 0;
        if (age < -250 - uncertainty) throw new SceneException("future_capture");
        var freshness = request.MaxObservationAgeMs - Math.Max(0, age + uncertainty);
        var remaining = Math.Min(request.RemainingBudgetMs, freshness);
        if (remaining <= 0) throw new SceneException("observation_expired");
        return (TimeSpan.FromMilliseconds(remaining), TimeSpan.FromMilliseconds(freshness), known, Math.Max(0, age));
    }

    private string UpdateGeneration(SceneRequest request, SceneGrant grant)
    {
        if (!_latest.ContainsKey(grant.CallerId) && _latest.Count >= _options.MaxJobs) throw new SceneException("caller_capacity");
        if (_retiredEpochs.TryGetValue(grant.CallerId, out var epochs) && epochs.Contains(request.SessionEpoch)) throw new SceneException("stale_context");
        if (_latest.TryGetValue(grant.CallerId, out var latest))
        {
            if (latest.SessionEpoch != request.SessionEpoch) Retire(grant.CallerId, latest.SessionEpoch);
            else if (request.SceneRevision < latest.SceneRevision || request.LayoutRevision < latest.LayoutRevision || request.CapturedAtUtc < latest.CapturedAtUtc)
                throw new SceneException("stale_context");
        }
        var fingerprint = SceneJson.Generation(request, _backend.Identity);
        if (!_generations.TryGetValue(grant.CallerId, out var current) || current.Fingerprint != fingerprint)
            _generations[grant.CallerId] = current = new(fingerprint, Guid.NewGuid().ToString("N"));
        var generation = current.Incarnation;
        _latest[grant.CallerId] = request;
        foreach (var old in _jobs.Values.Where(j => j.Caller == grant.CallerId && j.Entry.Generation != generation && !Terminal(j.Entry.Result.Status)).ToArray())
            Finish(old, "superseded", "context_changed");
        return generation;
    }

    private void Retire(string caller, string epoch)
    {
        if (!_retiredEpochs.TryGetValue(caller, out var epochs)) _retiredEpochs[caller] = epochs = new(StringComparer.Ordinal);
        if (epochs.Count >= _options.MaxJobs) throw new SceneException("session_capacity");
        epochs.Add(epoch);
    }

    private SceneAnalysisResult Present(Job job, SceneGrant grant)
    {
        RevokeIfUnauthorized(job, grant);
        var result = job.Entry.Result;
        var calibration = grant.ClockCalibration;
        bool fresh = job.HasMonotonicOrigin && _clock.GetElapsedTime(job.Start) < job.FreshnessBudget
            && calibration is not null && calibration.ValidUntilUtc > _clock.GetUtcNow()
            && calibration.Uncertainty >= TimeSpan.Zero && calibration.Uncertainty <= TimeSpan.FromMilliseconds(250)
            && _clock.GetUtcNow() + calibration.Uncertainty < result.ExpiresAtUtc + calibration.Offset;
        bool current = _generations.TryGetValue(grant.CallerId, out var generation) && generation.Incarnation == job.Entry.Generation;
        bool assets = result.Proposals.All(p => grant.ActorRevisions.TryGetValue(p.ActorId, out var revision) && revision == p.AssetRevision
            && grant.ActionIds.Contains(p.ActionId) && grant.TargetIds.Contains(p.TargetId));
        if (!fresh || !current || !assets || SceneJson.Scope(grant) != job.Entry.Scope)
            return result with { FreshnessVerified = false, Proposals = [], ErrorCode = result.ErrorCode ?? (!fresh ? "freshness_unverified_or_expired" : "stale_context") };
        return result;
    }

    private void Expire(Job job)
    {
        lock (_sync)
        {
            if (_disposed || Terminal(job.Entry.Result.Status)) return;
            try { Finish(job, "timed_out", "observation_deadline"); Pump(); }
            catch (SceneException) { _quarantined = true; }
        }
    }

    private void Finish(Job job, string state, string? code)
    {
        if (Terminal(job.Entry.Result.Status)) return;
        job.Timer?.Dispose();
        job.Entry = job.Entry with { Result = job.Entry.Result with { Status = state, ErrorCode = SceneRuntimeErrors.JobCode(code), FreshnessVerified = false, Proposals = [] } };
        if (job.DispatchRegistered is { } dispatch && !job.InferenceFinished)
        {
            job.Entry = job.Entry with { Result = job.Entry.Result with { BackendCancellation = "requested" } };
            _ = Task.Run(async () =>
            {
                string receipt;
                try
                {
                    // Do not let cancellation overtake registration of the
                    // inference call after the final dispatch check.
                    await dispatch.Task.ConfigureAwait(false);
                    using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    receipt = await _backend.RequestCancellationAsync(job.Entry.Result.RequestId, limit.Token).ConfigureAwait(false);
                    if (receipt is not ("unsupported" or "requested" or "confirmed")) receipt = "unconfirmed";
                }
                catch { receipt = "unconfirmed"; }
                lock (_sync)
                {
                    if (_disposed) return;
                    job.Entry = job.Entry with { Result = job.Entry.Result with { BackendCancellation = receipt } };
                    TryPersistBackground();
                }
            });
        }
        if (!job.Active) { job.Request = null; job.Principal = null; }
        Persist();
    }

    private SceneGrant? ReauthorizeJob(Job job)
    {
        try
        {
            // The host resolves current permissions for the original submitter.
            // Never hold _sync while invoking host authorization code.
            var grant = Authorize(job.Principal!);
            ValidateAuthority(job, grant);
            return grant;
        }
        catch (Exception error)
        {
            // Called before dispatch or after a successful backend return. An
            // authorization outage is not evidence of outstanding GPU work,
            // and must not expose provider exception details.
            var (state, reason) = AuthorizationFailure(error);
            lock (_sync) { Finish(job, state, reason); }
            return null;
        }
    }

    private void ValidateAuthority(Job job, SceneGrant? grant)
    {
        if (grant is null || grant.CallerId != job.Caller || SceneJson.Scope(grant) != job.Entry.Scope)
            throw new SceneException("permission_denied");
        SceneJson.Validate(job.Request!, grant, _options);
    }

    private static (string State, string Reason) AuthorizationFailure(Exception error)
        => error is SceneException { Code: "permission_denied" or "unauthenticated" or "actor_revision_mismatch"
            or "action_not_authorized" or "target_not_authorized" } known
            ? ("rejected", known.Code) : ("failed", "authorization_unavailable");

    private void Pump()
    {
        if (_disposed || _quarantined) return;
        while (_active < _options.MaxConcurrentInference)
        {
            var next = _jobs.Values.Where(j => j.Request is not null && j.Entry.Result.Status == "queued"
                && _jobs.Values.Count(a => a.Caller == j.Caller && a.Active) < _options.MaxRunningPerCaller).OrderBy(j => j.Start).FirstOrDefault();
            if (next is null) return;
            if (_clock.GetElapsedTime(next.Start) >= next.Budget) { Finish(next, "timed_out", "observation_deadline"); continue; }
            PersistUpdate(next, next.Entry with { Result = next.Entry.Result with { Status = "running" }, PhysicalOutstanding = true });
            next.Active = true; _active++;
            next.Work = Task.Run(() => RunAsync(next));
        }
    }

    private async Task RunAsync(Job job)
    {
        IReadOnlyList<ReadOnlyMemory<byte>> images = [];
        try
        {
            var started = _clock.GetTimestamp();
            var readiness = await ProbeAsync().ConfigureAwait(false);
            if (readiness.State != "ready" || !readiness.DeploymentVerified) throw new SceneException(readiness.ReasonCode);
            lock (_sync) { if (Terminal(job.Entry.Result.Status)) return; }
            var grant = ReauthorizeJob(job);
            if (grant is null) return;
            images = _media.Resolve(job.Request!, grant);
            lock (_sync)
            {
                if (Terminal(job.Entry.Result.Status)) return;
                if (_clock.GetElapsedTime(job.Start) >= job.Budget) { Finish(job, "timed_out", "observation_deadline"); return; }
                CheckEnabled();
                job.DispatchRegistered = new(TaskCreationOptions.RunContinuationsAsynchronously);
            }
            var prepared = _clock.GetTimestamp();
            // Logical cancellation does not abort this await. The backend owns
            // physical completion; releasing its slot early would amplify load.
            Task<SceneBackendReply> inference;
            try { inference = _backend.AnalyzeAsync(new(job.Request!, images), CancellationToken.None); }
            finally { job.DispatchRegistered.TrySetResult(); }
            var reply = await inference.ConfigureAwait(false);
            var inferred = _clock.GetTimestamp();
            lock (_sync)
            {
                job.InferenceFinished = true;
                if (Terminal(job.Entry.Result.Status)) return;
                if (_clock.GetElapsedTime(job.Start) >= job.Budget) { Finish(job, "timed_out", "observation_deadline"); return; }
            }
            if (SceneJson.Write(reply.Provider) != SceneJson.Write(_backend.Identity)) throw new SceneException("model_revision_mismatch");
            var parsed = SceneJson.ValidateOutput(reply.ResultJson, job.Request!, _options);
            // Validate the original submitter's current authority before any
            // observations or candidates become a durable completed result.
            if (ReauthorizeJob(job) is null) return;
            var validated = _clock.GetTimestamp();
            lock (_sync)
            {
                if (Terminal(job.Entry.Result.Status)) return;
                if (_clock.GetElapsedTime(job.Start) >= job.Budget) { Finish(job, "timed_out", "observation_deadline"); return; }
                var request = job.Request!;
                var proposals = request.Mode == "observe-only" || !job.ClockVerified ? [] : parsed.Proposals.Select((p, index) => new SceneProposal(
                    "proposal-" + SceneJson.Hash("scene-proposal.v1\n" + job.Entry.Key + "\n" + job.Entry.InputHash + "\n" + index + "\n" + SceneJson.Write(p))[7..],
                    p.ActorId, p.AssetRevision, p.ActionId, p.TargetId, p.ObservationIds, job.Entry.Result.ExpiresAtUtc)).ToArray();
                job.Timer?.Dispose();
                var completed = job.Entry with { Result = job.Entry.Result with
                {
                    Status = "completed", Observations = parsed.Observations, Proposals = proposals,
                    FreshnessVerified = job.ClockVerified,
                    ErrorCode = job.ClockVerified ? null : "clock_unverified", Provider = reply.Provider,
                    Timings = new(_clock.GetElapsedTime(job.Start, started).TotalMilliseconds,
                        _clock.GetElapsedTime(started, prepared).TotalMilliseconds, _clock.GetElapsedTime(prepared, inferred).TotalMilliseconds,
                        _clock.GetElapsedTime(inferred, validated).TotalMilliseconds,
                        job.Entry.Result.Timings.ObservationAgeMs + _clock.GetElapsedTime(job.Start).TotalMilliseconds)
                } };
                if (System.Text.Encoding.UTF8.GetByteCount(SceneJson.Write(completed.Result)) > _options.MaxResponseBytes)
                    completed = completed with { Result = completed.Result with { Status = "failed", ErrorCode = "output_limit", Observations = [], Proposals = [], FreshnessVerified = false } };
                PersistUpdate(job, completed);
            }
        }
        catch (SceneException ex)
        {
            lock (_sync)
            {
                if (ex.WorkMayStillBeRunning) { _quarantined = true; job.UnknownPhysical = true; }
                try { Finish(job, "failed", ex.Code); } catch (SceneException) { _quarantined = true; }
                if (ex.Code is "backend_busy" or "backend_unavailable" or "backend_oom")
                    job.Entry = job.Entry with { Result = job.Entry.Result with { RetryAfterMs = ex.Code == "backend_busy" ? 1000 : 5000 } };
            }
        }
        catch
        {
            lock (_sync)
            {
                _quarantined = true; job.UnknownPhysical = true;
                try { Finish(job, "failed", "backend_failed_unconfirmed"); } catch (SceneException) { }
            }
        }
        finally
        {
            foreach (var image in images)
                if (System.Runtime.InteropServices.MemoryMarshal.TryGetArray(image, out var array) && array.Array is not null)
                    System.Security.Cryptography.CryptographicOperations.ZeroMemory(array.Array);
            lock (_sync)
            {
                job.Active = false; _active--;
                // A failed transition cannot be repaired implicitly by later
                // physical completion. Keep the lease until explicit recovery.
                var finalized = job.Entry with { PhysicalOutstanding = job.UnknownPhysical || (_quarantined && job.Entry.PhysicalOutstanding) };
                job.Request = null; job.Principal = null;
                try { PersistUpdate(job, finalized); } catch (SceneException) { /* Preserve the live lease and quarantine. */ }
                if (!_disposed) { try { Pump(); } catch (SceneException) { _quarantined = true; } }
            }
        }
    }

    private void Purge()
    {
        foreach (var pair in _jobs.Where(p => !p.Value.Active && !p.Value.Entry.PhysicalOutstanding && Terminal(p.Value.Entry.Result.Status) && p.Value.Entry.RetainUntilUtc <= _clock.GetUtcNow()).ToArray())
            _jobs.Remove(pair.Key);
    }
    private void Persist() => Persist(_jobs.Values.Select(j => j.Entry));

    private void PersistUpdate(Job job, SceneJournalEntry entry)
    {
        Persist(_jobs.Values.Select(existing => ReferenceEquals(existing, job) ? entry : existing.Entry));
        job.Entry = entry;
    }

    private void Persist(IEnumerable<SceneJournalEntry> entries, bool confirmRecovery = false)
    {
        try { _journal.Save(entries, _quarantined, confirmRecovery); }
        catch { _quarantined = true; throw new SceneException("journal_unavailable"); }
    }
    private void TryPersistBackground()
    {
        try { Persist(); } catch (SceneException) { _quarantined = true; }
    }
    private SceneCapabilityCatalog Catalog()
    {
        lock (_sync)
        {
            CheckNotDisposed();
            var readiness = _quarantined ? _readiness with { State = "degraded", ReasonCode = "backend_work_unconfirmed", DeploymentVerified = false }
                : _clock.GetUtcNow() - _probedAt > _options.ReadinessMaxAge && _options.Enabled
                    ? _readiness with { State = "unavailable", ReasonCode = "probe_required", DeploymentVerified = false }
                    : _active >= _options.MaxConcurrentInference ? _readiness with { State = "busy", ReasonCode = "inference_capacity", RetryAfterMs = 1000 } : _readiness;
            return new(CapabilityId, "scene-catalog.v1", "0.1.4-dev13",
                ["scene.analyze", "scene.status", "scene.cancel", "scene.invalidate", "scene.capabilities"],
                "request_json", ResultMetadataKey, ["image/png"], false, true,
                "image-normalized; origin=top-left; x=right; y=down; orientation=stored-pixels; no-3d",
                _options.MaxFrames, _options.MaxActors, _options.MaxProposals, _options.MaxImageBytes, _options.MaxImagePixels,
                _options.MaxStoredMediaBytes, _options.MaxRequestBytes, _options.MaxResponseBytes, _options.MaxRunningPerCaller, _options.MaxPendingPerCaller,
                _options.MaxConcurrentInference, _options.MaxJobs, _options.MaxObservationAgeMs, _options.MediaRetention.TotalSeconds,
                _options.IdempotencyRetention.TotalSeconds, SceneRuntimeErrors.Readiness(readiness, readiness.Provider), _active, _jobs.Values.Count(j => j.Entry.Result.Status == "queued"), false,
                _options.MaxConcurrentUploads);
        }
    }

    private static CapabilityInvocationResult Reply(CapabilityInvocationRequest request, object result, bool succeeded)
    {
        if (result is SceneAnalysisResult analysis) result = SceneRuntimeErrors.Result(analysis);
        var json = SceneJson.Write(result);
        return new(request.InvocationId, request.CapabilityId, succeeded, SceneJson.Hash(json), null, null, request.ReplayLogHash,
            new Dictionary<string, string> { [ResultMetadataKey] = json });
    }
    private static CapabilityInvocationResult Failure(CapabilityInvocationRequest request, string? code)
    {
        var safeCode = SceneIntakeErrors.Code(code);
        return new(request.InvocationId, request.CapabilityId, false, null, safeCode, safeCode, request.ReplayLogHash, new Dictionary<string, string>());
    }

    public ValueTask DisposeAsync()
    {
        try
        {
            lock (_sync)
            {
                if (_shutdown is not null) return new(_shutdown);
                foreach (var job in _jobs.Values.Where(j => !Terminal(j.Entry.Result.Status)).ToArray())
                    try { Finish(job, "canceled", "service_stopping"); } catch (SceneException) { _quarantined = true; }
                _disposed = true;
                // All callers await the same drain and close, including a
                // failed close. Never acknowledge release of a live lease.
                _shutdown = CompleteShutdownAsync(_jobs.Values.Select(j => j.Work).ToArray());
                return new(_shutdown);
            }
        }
        catch (Exception error) { return ShutdownPreparationFailureAsync(error); }
    }

    private static async ValueTask ShutdownPreparationFailureAsync(Exception error)
    {
        // An async builder preserves a canceled task for cancellation errors;
        // FromException would silently change that state to Faulted.
        await Task.CompletedTask.ConfigureAwait(false);
        throw SceneHostErrors.Sanitize(error);
    }

    private async Task CompleteShutdownAsync(Task[] work)
    {
        try
        {
            try { await Task.WhenAll(work).ConfigureAwait(false); }
            finally { _media.Clear(); _journal.Close(_quarantined); }
        }
        catch (Exception error) { throw SceneHostErrors.Sanitize(error); }
    }
}

public sealed class SceneSession : ISceneSession
{
    private readonly SceneService service;
    private readonly ClaimsPrincipal principal;
    private readonly string caller;
    internal SceneSession(SceneService service, ClaimsPrincipal principal, string caller) { this.service = service; this.principal = principal; this.caller = caller; }
    public Task<SceneMediaReceipt> RegisterMediaAsync(SceneMediaRegistration registration, Stream data, CancellationToken cancellationToken = default)
        => service.RegisterAsync(principal, caller, registration, data, cancellationToken);
    public ValueTask<CapabilityInvocationResult> InvokeAsync(CapabilityInvocationRequest request, CancellationToken cancellationToken = default)
        => service.InvokeAsync(principal, caller, request, cancellationToken);
}
