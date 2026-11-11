using System.Security.Claims;
using AIKernel.Abstractions.Capabilities;
using AIKernel.Dtos.Capabilities;

namespace AIKernel.Providers.Cosmos;

public sealed class SceneService : IAsyncDisposable
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
        _journal = new(options.JournalDirectory, options.MaxJobs, options.MaxResponseBytes);
        _readiness = new(options.Enabled ? "unavailable" : "disabled", "not_probed", false, backend.Identity);
        try
        {
            foreach (var stored in _journal.Load())
            {
                if (stored.RetainUntilUtc <= _clock.GetUtcNow() && !stored.PhysicalOutstanding && stored.Result.Status != "running") continue;
                var entry = stored;
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
        catch { _journal.Dispose(); throw; }
    }

    public SceneSession OpenSession(ClaimsPrincipal authenticatedCaller)
    {
        var principal = new ClaimsPrincipal(authenticatedCaller.Identities.Select(identity => new ClaimsIdentity(identity)));
        var grant = Authorize(principal);
        return new(this, principal, grant.CallerId);
    }

    private SceneGrant Authorize(ClaimsPrincipal principal, string? expectedCaller = null)
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

    internal Task<SceneMediaReceipt> RegisterAsync(ClaimsPrincipal principal, string caller, SceneMediaRegistration registration,
        Stream source, CancellationToken cancellationToken)
    {
        return Task.Run(async () =>
        {
            var grant = Authorize(principal, caller);
            lock (_sync) { CheckEnabled(); }
            return await _media.RegisterAsync(grant, registration, source, cancellationToken).ConfigureAwait(false);
        }, cancellationToken);
    }

    // Calling this probe never starts/restarts a model or changes GPU placement.
    public async ValueTask<SceneReadiness> ProbeAsync(CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            if (_disposed || !_options.Enabled) return _readiness with { State = "disabled", ReasonCode = "disabled" };
            if (_quarantined) return _readiness with { State = "degraded", ReasonCode = "backend_work_unconfirmed", DeploymentVerified = false };
        }
        SceneReadiness result;
        try { result = await _backend.ProbeAsync(cancellationToken).ConfigureAwait(false); }
        catch (OperationCanceledException) { throw; }
        catch { result = new("unavailable", "probe_failed", false, _backend.Identity); }
        lock (_sync)
        {
            if (result.Provider != _backend.Identity && SceneJson.Write(result.Provider) != SceneJson.Write(_backend.Identity))
                result = new("degraded", "model_revision_mismatch", false, _backend.Identity);
            _readiness = result; _probedAt = _clock.GetUtcNow();
            return result;
        }
    }

    // Operational API, intentionally absent from the caller capability surface.
    // The host must establish that previous backend work has actually stopped.
    public void ConfirmBackendQuiescent()
    {
        lock (_sync)
        {
            if (_active != 0) throw new SceneException("backend_still_active");
            _quarantined = false;
            foreach (var job in _jobs.Values) job.Entry = job.Entry with { PhysicalOutstanding = false };
            Persist();
            _readiness = new("unavailable", "probe_required", false, _backend.Identity);
        }
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
            SceneGrant grant;
            try { grant = Authorize(principal, caller); }
            catch
            {
                // The session identity was authenticated when opened. Failure
                // to obtain current authority must not leave its known completed
                // candidates available for revival after authority returns.
                if (!token.IsCancellationRequested) RevokeCompletedLookup(caller, envelope, null);
                throw;
            }
            RevokeCompletedLookup(caller, envelope, grant);
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
                    Persist(); // Durable identity is established before any inference dispatch.
                    job.Timer = _clock.CreateTimer(_ => Expire(job), null, budget, Timeout.InfiniteTimeSpan);
                    Pump();
                    return Reply(envelope, Present(job, grant), true);
                }
            }
            if (envelope.Operation is "scene.status" or "scene.cancel")
            {
                if (envelope.Arguments.Count != 1 || !envelope.Arguments.TryGetValue("requestId", out var id)) throw new SceneException("unknown_argument");
                SceneJson.Id(id);
                lock (_sync)
                {
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
        catch (OperationCanceledException) { throw; }
        catch (SceneException ex) { return Failure(envelope, ex.Code); }
        catch (Exception) { return Failure(envelope, "invalid_request"); }
    }

    private static string Key(SceneGrant grant, string id) => Key(grant.CallerId, id);
    private static string Key(string caller, string id) => SceneJson.Hash(SceneJson.Write(new { CallerId = caller, requestId = id }));

    private void RevokeCompletedLookup(string caller, CapabilityInvocationRequest envelope, SceneGrant? grant)
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
            RevokeIfUnauthorized(job, grant);
        }
    }

    // Called under _sync, including at the final result projection. Persist
    // before acknowledging revocation; older concurrent grants cannot undo it.
    private void RevokeIfUnauthorized(Job job, SceneGrant? grant)
    {
        var result = job.Entry.Result;
        if (result.Status != "completed" || result.Proposals.Count == 0) return;
        if (grant is not null && job.Entry.Scope == SceneJson.Scope(grant) && result.Proposals.All(p =>
            grant.ActorRevisions.TryGetValue(p.ActorId, out var revision) && revision == p.AssetRevision
            && grant.ActionIds.Contains(p.ActionId) && grant.TargetIds.Contains(p.TargetId))) return;
        job.Entry = job.Entry with { Result = result with { Proposals = [], FreshnessVerified = false, ErrorCode = result.ErrorCode ?? "stale_context" } };
        Persist();
    }
    private static bool Terminal(string state) => state is not ("accepted" or "queued" or "running");
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

    private void Finish(Job job, string state, string code)
    {
        if (Terminal(job.Entry.Result.Status)) return;
        job.Timer?.Dispose();
        job.Entry = job.Entry with { Result = job.Entry.Result with { Status = state, ErrorCode = code, FreshnessVerified = false, Proposals = [] } };
        if (job.DispatchRegistered is { } dispatch)
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

    private SceneGrant? ReauthorizeForDispatch(Job job)
    {
        string state, reason;
        try
        {
            // The host resolves current permissions for the original submitter.
            // Never hold _sync while invoking host authorization code.
            var grant = Authorize(job.Principal!);
            if (grant.CallerId != job.Caller || SceneJson.Scope(grant) != job.Entry.Scope)
                throw new SceneException("permission_denied");
            SceneJson.Validate(job.Request!, grant, _options);
            return grant;
        }
        catch (SceneException ex) when (ex.Code is "permission_denied" or "unauthenticated"
            or "actor_revision_mismatch" or "action_not_authorized" or "target_not_authorized")
        {
            state = "rejected"; reason = ex.Code;
        }
        catch
        {
            // No backend work has started. An authorization outage (including
            // its own cancellation) must fail closed without quarantining GPUs
            // or exposing provider exception details.
            state = "failed"; reason = "authorization_unavailable";
        }
        lock (_sync) { Finish(job, state, reason); }
        return null;
    }

    private void Pump()
    {
        if (_disposed || _quarantined) return;
        while (_active < _options.MaxConcurrentInference)
        {
            var next = _jobs.Values.Where(j => j.Request is not null && j.Entry.Result.Status == "queued"
                && _jobs.Values.Count(a => a.Caller == j.Caller && a.Active) < _options.MaxRunningPerCaller).OrderBy(j => j.Start).FirstOrDefault();
            if (next is null) return;
            if (_clock.GetElapsedTime(next.Start) >= next.Budget) { Finish(next, "timed_out", "observation_deadline"); continue; }
            next.Active = true; _active++;
            next.Entry = next.Entry with { Result = next.Entry.Result with { Status = "running" }, PhysicalOutstanding = true };
            try { Persist(); }
            catch
            {
                next.Active = false; _active--;
                next.Entry = next.Entry with { PhysicalOutstanding = false };
                throw;
            }
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
            var grant = ReauthorizeForDispatch(job);
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
            if (SceneJson.Write(reply.Provider) != SceneJson.Write(_backend.Identity)) throw new SceneException("model_revision_mismatch");
            var parsed = SceneJson.ValidateOutput(reply.ResultJson, job.Request!, _options);
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
                job.Entry = job.Entry with { Result = job.Entry.Result with
                {
                    Status = "completed", Observations = parsed.Observations, Proposals = proposals,
                    FreshnessVerified = job.ClockVerified,
                    ErrorCode = job.ClockVerified ? null : "clock_unverified", Provider = reply.Provider,
                    Timings = new(_clock.GetElapsedTime(job.Start, started).TotalMilliseconds,
                        _clock.GetElapsedTime(started, prepared).TotalMilliseconds, _clock.GetElapsedTime(prepared, inferred).TotalMilliseconds,
                        _clock.GetElapsedTime(inferred, validated).TotalMilliseconds,
                        job.Entry.Result.Timings.ObservationAgeMs + _clock.GetElapsedTime(job.Start).TotalMilliseconds)
                } };
                if (System.Text.Encoding.UTF8.GetByteCount(SceneJson.Write(job.Entry.Result)) > _options.MaxResponseBytes)
                    job.Entry = job.Entry with { Result = job.Entry.Result with { Status = "failed", ErrorCode = "output_limit", Observations = [], Proposals = [], FreshnessVerified = false } };
                Persist();
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
                job.Entry = job.Entry with { PhysicalOutstanding = job.UnknownPhysical };
                job.Request = null; job.Principal = null;
                TryPersistBackground();
                if (!_disposed) { try { Pump(); } catch (SceneException) { _quarantined = true; } }
            }
        }
    }

    private void Purge()
    {
        foreach (var pair in _jobs.Where(p => !p.Value.Active && !p.Value.Entry.PhysicalOutstanding && Terminal(p.Value.Entry.Result.Status) && p.Value.Entry.RetainUntilUtc <= _clock.GetUtcNow()).ToArray())
            _jobs.Remove(pair.Key);
    }
    private void Persist()
    {
        try { _journal.Save(_jobs.Values.Select(j => j.Entry)); }
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
            var readiness = _quarantined ? _readiness with { State = "degraded", ReasonCode = "backend_work_unconfirmed", DeploymentVerified = false }
                : _clock.GetUtcNow() - _probedAt > _options.ReadinessMaxAge && _options.Enabled
                    ? _readiness with { State = "unavailable", ReasonCode = "probe_required", DeploymentVerified = false }
                    : _active >= _options.MaxConcurrentInference ? _readiness with { State = "busy", ReasonCode = "inference_capacity", RetryAfterMs = 1000 } : _readiness;
            return new(CapabilityId, "scene-catalog.v1", "0.1.4-dev4",
                ["scene.analyze", "scene.status", "scene.cancel", "scene.invalidate", "scene.capabilities"],
                "request_json", ResultMetadataKey, ["image/png"], false, true,
                "image-normalized; origin=top-left; x=right; y=down; orientation=stored-pixels; no-3d",
                _options.MaxFrames, _options.MaxActors, _options.MaxProposals, _options.MaxImageBytes, _options.MaxImagePixels,
                _options.MaxStoredMediaBytes, _options.MaxRequestBytes, _options.MaxResponseBytes, _options.MaxRunningPerCaller, _options.MaxPendingPerCaller,
                _options.MaxConcurrentInference, _options.MaxJobs, _options.MaxObservationAgeMs, _options.MediaRetention.TotalSeconds,
                _options.IdempotencyRetention.TotalSeconds, readiness, _active, _jobs.Values.Count(j => j.Entry.Result.Status == "queued"), false,
                _options.MaxConcurrentUploads);
        }
    }

    private static CapabilityInvocationResult Reply(CapabilityInvocationRequest request, object result, bool succeeded)
    {
        var json = SceneJson.Write(result);
        return new(request.InvocationId, request.CapabilityId, succeeded, SceneJson.Hash(json), null, null, request.ReplayLogHash,
            new Dictionary<string, string> { [ResultMetadataKey] = json });
    }
    private static CapabilityInvocationResult Failure(CapabilityInvocationRequest request, string code)
        => new(request.InvocationId, request.CapabilityId, false, null, code, code, request.ReplayLogHash, new Dictionary<string, string>());

    public async ValueTask DisposeAsync()
    {
        Task[] work;
        lock (_sync)
        {
            if (_disposed) return;
            foreach (var job in _jobs.Values.Where(j => !Terminal(j.Entry.Result.Status)).ToArray())
                try { Finish(job, "canceled", "service_stopping"); } catch (SceneException) { _quarantined = true; }
            _disposed = true;
            work = _jobs.Values.Select(j => j.Work).ToArray();
        }
        try { await Task.WhenAll(work).ConfigureAwait(false); }
        finally { _media.Clear(); _journal.Dispose(); }
    }
}

public sealed class SceneSession : ICapabilityModuleInvoker
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
