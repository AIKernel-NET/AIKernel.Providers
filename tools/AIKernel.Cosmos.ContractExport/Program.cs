using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization.Metadata;
using AIKernel.Providers.Cosmos;

if (args.Length is < 1 or > 2) throw new ArgumentException("Usage: ContractExport <schema-directory> [--check]");
var output = Path.GetFullPath(args[0]);
Directory.CreateDirectory(output);
var serializer = new JsonSerializerOptions(SceneJson.Options) { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
var pretty = new JsonSerializerOptions { WriteIndented = true };
var types = new (string Name, Type Type)[]
{
    ("request", typeof(SceneRequest)), ("result", typeof(SceneAnalysisResult)),
    ("model-result", typeof(SceneModelResult)), ("catalog", typeof(SceneCapabilityCatalog)),
    ("media-registration", typeof(SceneMediaRegistration)), ("media-receipt", typeof(SceneMediaReceipt)),
    ("deployment-evidence", typeof(SceneDeploymentEvidence))
};
foreach (var (name, type) in types)
{
    var schema = serializer.GetJsonSchemaAsNode(type, new JsonSchemaExporterOptions { TreatNullObliviousAsNonNullable = true });
    schema["$schema"] = "https://json-schema.org/draft/2020-12/schema";
    schema["$id"] = "urn:aikernel:cosmos:scene-v1:" + name;
    schema["description"] = "Wire shape. Cross-field authorization, catalog, snapshot, duplicate keys, byte limits and configured limits are additionally enforced by SceneService. See COSMOS_SCENE_P0.md.";
    Constrain(schema);
    var props = schema["properties"]!;
    if (name == "request")
    {
        props["schemaVersion"]!["const"] = "scene-observation.v1";
        props["media"]!["minItems"] = 1; props["media"]!["maxItems"] = 32;
        props["actors"]!["maxItems"] = 256;
        props["targets"]!["maxItems"] = 128; props["allowedActions"]!["maxItems"] = 128;
    }
    if (name == "result")
    {
        props["schemaVersion"]!["const"] = "scene-analysis.v1";
        props["analysisOnly"]!["const"] = true;
        props["status"]!["enum"] = Values("accepted", "queued", "running", "completed", "canceled", "timed_out", "superseded", "rejected", "failed", "interrupted");
        props["backendCancellation"]!["enum"] = Values("not_requested", "requested", "unsupported", "confirmed", "unconfirmed");
    }
    if (name is "result" or "model-result")
    {
        props["observations"]!["maxItems"] = 128; props["proposals"]!["maxItems"] = 128;
    }
    if (name == "catalog")
    {
        props["schemaVersion"]!["const"] = "scene-catalog.v1";
        props["capabilityId"]!["const"] = SceneService.CapabilityId;
        props["executesActions"]!["const"] = false; props["videoSupported"]!["const"] = false;
    }
    Save(name + ".schema.json", schema.ToJsonString(pretty));
}
var now = new DateTimeOffset(2026, 11, 11, 3, 0, 0, TimeSpan.Zero);
var snapshot = new SceneSnapshot("epoch-example", "snap-example", "program-composite", now, 41, 8, "camera-wide");
var request = new SceneRequest("scene-observation.v1", "req-example", snapshot.SessionEpoch, snapshot.SnapshotId,
    snapshot.CaptureSource, now, 5000, 3000, 41, 8, snapshot.CameraId, "public-policy-3", "public", "shadow",
    [new("media-example", "frame-1", "image/png", 960, 540, 0)], [new("actor-a", 12, "listening", true)],
    [new("board-a", "presentation-surface")], [new("look_at", ["actor-a"], ["board-a"])], 2);
var result = new SceneAnalysisResult("scene-analysis.v1", request.RequestId, request.SessionEpoch, request.SnapshotId,
    "completed", true, false, "example_only", 41, 8, request.CameraId, request.PolicyRevision, now, now.AddSeconds(5), [], [],
    new("example-only", "not-a-real-model", "not-measured", "none", "none", "none", [], true), new(0, 0, 0, 0, 0), "not_requested", "shadow");
Save("request.example.json", JsonSerializer.Serialize(request, new JsonSerializerOptions(serializer) { WriteIndented = true }));
Save("result.example.json", JsonSerializer.Serialize(result, new JsonSerializerOptions(serializer) { WriteIndented = true }));
Console.WriteLine("7 schemas and 2 illustrative examples " + (args.Contains("--check") ? "verified." : "written."));

void Save(string filename, string content)
{
    var path = Path.Combine(output, filename); content = content.Replace("\r\n", "\n") + "\n";
    if (args.Contains("--check"))
    {
        if (!File.Exists(path) || File.ReadAllText(path).Replace("\r\n", "\n") != content) throw new InvalidOperationException("Contract drift: " + filename);
    }
    else File.WriteAllText(path, content, new System.Text.UTF8Encoding(false));
}
static JsonArray Values(params string[] values) => new(values.Select(v => JsonValue.Create(v)).ToArray<JsonNode?>());
static void Constrain(JsonNode? node)
{
    if (node is JsonObject obj)
    {
        if (obj["properties"] is JsonObject properties)
        {
            foreach (var (name, value) in properties)
            {
                if (value is not JsonObject property || property.ContainsKey("$ref")) continue;
                if (name.EndsWith("Id", StringComparison.Ordinal) || name is "sessionEpoch" or "captureSource" or "permissionScope" or "policyRevision" or "activity" or "facing" or "speakerState" or "kind")
                { property["pattern"] = "^[A-Za-z0-9][A-Za-z0-9_.:-]{0,127}$"; property["maxLength"] = 128; }
                if (name == "mode") property["enum"] = Values("shadow", "observe-only", "propose");
                if (name == "mimeType") property["const"] = "image/png";
                if (name == "contentHash") property["pattern"] = "^sha256:[a-f0-9]{64}$";
                if (name == "reasonCode" && properties.ContainsKey("observationId")) property["enum"] = Values("unknown", "no_issue", "attention_target_mismatch", "target_not_visible", "occlusion", "overlap", "motion_observed", "activity_mismatch", "visual_anomaly");
                if (name == "state") property["enum"] = Values("disabled", "loading", "warming_up", "ready", "busy", "degraded", "unavailable");
                if (name is "sceneRevision" or "layoutRevision" or "assetRevision" or "maxProposals") property["minimum"] = 0;
                if (name is "maxObservationAgeMs" or "remainingBudgetMs") { property["minimum"] = 1; property["maximum"] = 600000; }
                if (name == "offsetMs") { property["minimum"] = -600000; property["maximum"] = 0; }
                if (name is "x" or "y" or "width" or "height" && properties.ContainsKey("x")) { property["minimum"] = 0; property["maximum"] = 1; }
            }
        }
        foreach (var child in obj.ToArray()) Constrain(child.Value);
    }
    else if (node is JsonArray array) foreach (var child in array) Constrain(child);
}
