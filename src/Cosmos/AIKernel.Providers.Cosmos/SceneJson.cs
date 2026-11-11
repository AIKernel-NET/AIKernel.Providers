using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace AIKernel.Providers.Cosmos;

internal static partial class SceneJson
{
    internal static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true,
        MaxDepth = 24
    };

    internal static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);
    internal static T Read<T>(string json, int maximumBytes)
    {
        if (Encoding.UTF8.GetByteCount(json) > maximumBytes) throw new SceneException("payload_too_large");
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 24 });
            RejectDuplicates(document.RootElement);
            return JsonSerializer.Deserialize<T>(json, Options) ?? throw new SceneException("invalid_json");
        }
        catch (JsonException) { throw new SceneException("invalid_json"); }
        catch (NotSupportedException) { throw new SceneException("invalid_json"); }
    }

    internal static void RejectDuplicates(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new SceneException("duplicate_field");
                RejectDuplicates(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) RejectDuplicates(item);
    }

    // New scene contract only. Existing ROM/StepId/ReplayLogHash formats are untouched.
    internal static string Hash(ReadOnlySpan<byte> bytes) => "sha256:" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    internal static string Hash(string value) => Hash(Encoding.UTF8.GetBytes(value));
    internal static string Scope(SceneGrant grant) => Hash(Write(new { grant.CallerId, grant.PermissionScope, grant.PolicyRevision }));
    internal static SceneSnapshot Snapshot(SceneRequest r) => new(r.SessionEpoch, r.SnapshotId, r.CaptureSource,
        r.CapturedAtUtc, r.SceneRevision, r.LayoutRevision, r.CameraId);
    internal static string Generation(SceneRequest r, SceneBackendIdentity backend) => Hash(Write(new
    {
        r.SessionEpoch, r.SceneRevision, r.LayoutRevision, r.CameraId, r.PolicyRevision,
        assets = r.Actors.OrderBy(a => a.ActorId, StringComparer.Ordinal).Select(a => new { a.ActorId, a.AssetRevision }), backend
    }));

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9_.:-]{0,127}$", RegexOptions.CultureInvariant)]
    private static partial Regex IdPattern();
    internal static void Id(string? id)
    {
        if (id is null || !IdPattern().IsMatch(id)) throw new SceneException("invalid_identifier");
    }
    internal static void Unique(IEnumerable<string> ids)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in ids) { Id(id); if (!seen.Add(id)) throw new SceneException("duplicate_identifier"); }
    }

    internal static void Validate(SceneRequest r, SceneGrant grant, SceneOptions options)
    {
        foreach (var id in new[] { r.RequestId, r.SessionEpoch, r.SnapshotId, r.CaptureSource, r.CameraId, r.PolicyRevision, r.PermissionScope }) Id(id);
        if (r.SchemaVersion != "scene-observation.v1") throw new SceneException("schema_unsupported");
        if (r.PermissionScope != grant.PermissionScope || r.PolicyRevision != grant.PolicyRevision) throw new SceneException("permission_denied");
        if (r.Mode is not ("shadow" or "observe-only" or "propose")) throw new SceneException("mode_unsupported");
        if (r.SceneRevision < 0 || r.LayoutRevision < 0 || r.CapturedAtUtc.Offset != TimeSpan.Zero) throw new SceneException("inconsistent_snapshot");
        if (r.MaxObservationAgeMs <= 0 || r.MaxObservationAgeMs > options.MaxObservationAgeMs
            || r.RemainingBudgetMs <= 0 || r.RemainingBudgetMs > options.MaxObservationAgeMs)
            throw new SceneException("invalid_deadline");
        if (r.Media is null || r.Actors is null || r.Targets is null || r.AllowedActions is null
            || r.Media.Count is < 1 || r.Media.Count > options.MaxFrames || r.Actors.Count > options.MaxActors
            || r.Targets.Count > 128 || r.AllowedActions.Count > 128 || r.MaxProposals < 0 || r.MaxProposals > options.MaxProposals)
            throw new SceneException("input_limit");
        Unique(r.Media.Select(m => m.FrameId)); Unique(r.Media.Select(m => m.MediaId));
        Unique(r.Actors.Select(a => a.ActorId)); Unique(r.Targets.Select(t => t.TargetId)); Unique(r.AllowedActions.Select(a => a.ActionId));
        int previousOffset = int.MinValue;
        foreach (var media in r.Media)
        {
            if (media.MimeType != "image/png") throw new SceneException("media_unsupported");
            if (media.Width < 1 || media.Height < 1 || (long)media.Width * media.Height > options.MaxImagePixels) throw new SceneException("media_too_large");
            if (media.OffsetMs < -options.MaxObservationAgeMs || media.OffsetMs > 0 || media.OffsetMs <= previousOffset)
                throw new SceneException("inconsistent_snapshot");
            previousOffset = media.OffsetMs;
        }
        if (previousOffset != 0) throw new SceneException("inconsistent_snapshot");
        foreach (var actor in r.Actors)
        {
            Id(actor.Activity); Id(actor.Facing); Id(actor.SpeakerState);
            if (!grant.ActorRevisions.TryGetValue(actor.ActorId, out var revision) || revision != actor.AssetRevision)
                throw new SceneException("actor_revision_mismatch");
        }
        foreach (var target in r.Targets)
        {
            Id(target.Kind);
            if (!grant.TargetIds.Contains(target.TargetId)) throw new SceneException("target_not_authorized");
        }
        foreach (var action in r.AllowedActions)
        {
            if (!grant.ActionIds.Contains(action.ActionId) || action.ActorIds is null || action.TargetIds is null
                || action.ActorIds.Count > options.MaxActors || action.TargetIds.Count > 128) throw new SceneException("action_not_authorized");
            Unique(action.ActorIds); Unique(action.TargetIds);
            if (action.ActorIds.Any(id => !r.Actors.Any(a => a.ActorId == id)) || action.TargetIds.Any(id => !r.Targets.Any(t => t.TargetId == id)))
                throw new SceneException("catalog_mismatch");
        }
    }

    private static readonly HashSet<string> Reasons = new(StringComparer.Ordinal)
    { "unknown", "no_issue", "attention_target_mismatch", "target_not_visible", "occlusion", "overlap", "motion_observed", "activity_mismatch", "visual_anomaly" };
    internal static SceneModelResult ValidateOutput(string json, SceneRequest r, SceneOptions options)
    {
        var result = Read<SceneModelResult>(json, options.MaxResponseBytes);
        if (result.Observations is null || result.Proposals is null || result.Observations.Count > 128 || result.Proposals.Count > r.MaxProposals)
            throw new SceneException("output_limit");
        Unique(result.Observations.Select(o => o.ObservationId));
        foreach (var observation in result.Observations)
        {
            if (!Reasons.Contains(observation.ReasonCode) || observation.EvidenceFrameIds is null || observation.EvidenceFrameIds.Count is < 1
                || observation.EvidenceFrameIds.Count > r.Media.Count) throw new SceneException("invalid_evidence");
            Unique(observation.EvidenceFrameIds);
            if (observation.EvidenceFrameIds.Any(id => !r.Media.Any(m => m.FrameId == id))) throw new SceneException("invalid_evidence");
            if (observation.ActorId is not null && !r.Actors.Any(a => a.ActorId == observation.ActorId)) throw new SceneException("unknown_actor");
            if (observation.TargetId is not null && !r.Targets.Any(t => t.TargetId == observation.TargetId)) throw new SceneException("unknown_target");
            if (observation.Unknown != (observation.ReasonCode == "unknown")) throw new SceneException("invalid_unknown");
            if (observation.ReasonCode == "motion_observed" && observation.EvidenceFrameIds.Count < 2) throw new SceneException("temporal_evidence_required");
            if (observation.Region is { } box && (!double.IsFinite(box.X) || !double.IsFinite(box.Y) || !double.IsFinite(box.Width) || !double.IsFinite(box.Height)
                || box.X < 0 || box.Y < 0 || box.Width <= 0 || box.Height <= 0 || box.X + box.Width > 1 || box.Y + box.Height > 1))
                throw new SceneException("invalid_region");
        }
        var proposals = new HashSet<string>(StringComparer.Ordinal);
        foreach (var proposal in result.Proposals)
        {
            var actor = r.Actors.SingleOrDefault(a => a.ActorId == proposal.ActorId);
            var action = r.AllowedActions.SingleOrDefault(a => a.ActionId == proposal.ActionId);
            if (actor is null || actor.AssetRevision != proposal.AssetRevision) throw new SceneException("actor_revision_mismatch");
            if (action is null || !action.ActorIds.Contains(proposal.ActorId) || !action.TargetIds.Contains(proposal.TargetId)) throw new SceneException("catalog_mismatch");
            if (proposal.ObservationIds is null || proposal.ObservationIds.Count is < 1 || proposal.ObservationIds.Count > 128) throw new SceneException("invalid_evidence");
            Unique(proposal.ObservationIds);
            if (proposal.ObservationIds.Any(id => !result.Observations.Any(o => o.ObservationId == id && !o.Unknown && o.ActorId == proposal.ActorId)))
                throw new SceneException("invalid_evidence");
            if (!proposals.Add(Write(proposal))) throw new SceneException("duplicate_proposal");
        }
        return result;
    }
}
