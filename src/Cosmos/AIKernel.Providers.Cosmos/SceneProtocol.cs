namespace AIKernel.Providers.Cosmos;

public static class SceneProtocol
{
    public static string SerializeRequest(SceneRequest request) => SceneJson.Write(request);
    public static SceneAnalysisResult DeserializeResult(string json, int maxBytes = 64 * 1024)
        => SceneJson.Read<SceneAnalysisResult>(json, maxBytes);
    public static string ComputeInputHash(string requestJson) => SceneJson.Hash(requestJson);
    public static string ComputeMediaHash(ReadOnlySpan<byte> mediaBytes) => SceneJson.Hash(mediaBytes);
}
