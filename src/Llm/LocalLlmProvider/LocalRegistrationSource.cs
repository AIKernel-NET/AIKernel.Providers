using AIKernel.Kernel.Inference;

namespace AIKernel.Providers.LocalLlm;

internal static class LocalRegistrationSource
{
    internal static DenseModelRegistration Read(string model, string? profile)
    {
        var (id, resource) = Path.GetExtension(model).ToLowerInvariant() switch
        {
            ".onnx" => ("qwen3-0.6b", "AIKernel.LocalLlm.qwen3-06b-onnx"),
            ".safetensors" or ".json" => ("qwen3-0.6b", "AIKernel.LocalLlm.qwen3-06b-safetensors"),
            _ => ("bonsai-1.7b", "AIKernel.LocalLlm.BonsaiRegistration")
        };
        return ReadRegistration(profile, id, resource);
    }

    private static DenseModelRegistration ReadRegistration(string? path, string id, string resource)
    {
        using var reader = path is null
            ? new StreamReader(System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream(resource)!)
            : File.OpenText(path);
        if (reader.BaseStream.Length > 64 * 1024) throw new InvalidDataException("The model registration exceeds 64 KiB.");
        return DenseModelRegistration.Parse(reader.ReadToEnd(), id);
    }
}
