namespace AIKernel.Providers.Cosmos;

// Host-owned, short-lived inventory written by the operator/control plane.
// Scene requests cannot choose this path or assert their own GPU placement.
public sealed class SceneDeploymentFileSource : ISceneDeploymentEvidenceSource
{
    private readonly string _path;
    public SceneDeploymentFileSource(string path)
    {
        if (!Path.IsPathFullyQualified(path) || path.StartsWith("\\\\", StringComparison.Ordinal))
            throw new ArgumentException("An explicit host-owned local inventory path is required.", nameof(path));
        _path = Path.GetFullPath(path);
    }
    public async ValueTask<SceneDeploymentEvidence> ReadAsync(CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
        if (stream.Length > 8192) throw new SceneException("deployment_evidence_invalid");
        var bytes = new byte[8193];
        int length = 0;
        while (length < bytes.Length)
        {
            int count = await stream.ReadAsync(bytes.AsMemory(length), cancellationToken).ConfigureAwait(false);
            if (count == 0) break;
            length += count;
        }
        if (length > 8192) throw new SceneException("deployment_evidence_invalid");
        return SceneJson.Read<SceneDeploymentEvidence>(System.Text.Encoding.UTF8.GetString(bytes, 0, length), 8192);
    }
}
