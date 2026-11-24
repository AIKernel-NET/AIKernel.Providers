using AIKernel.Kernel.Inference;

namespace AIKernel.Providers.LocalLlm;

internal static class LocalModelReader
{
    internal static Qwen3CpuModel Open(string path, LocalLlmSettings settings, CancellationToken cancellation)
    {
        var options = settings as RegisteredLocalLlmSettings;
        var registration = ReadRegistration(options?.RegistrationFile);
        var model = registration.Open(path, settings.CpuThreads, settings.ContextTokens, options?.WeightMemory);
        if (cancellation.IsCancellationRequested)
        {
            model.Dispose();
            cancellation.ThrowIfCancellationRequested();
        }
        return model;
    }

    private static DenseModelRegistration ReadRegistration(string? path)
    {
        using var stream = path is null
            ? typeof(LocalModelReader).Assembly.GetManifestResourceStream("AIKernel.LocalLlm.BonsaiRegistration")!
            : File.OpenRead(path);
        if (stream.Length > 64 * 1024) throw new InvalidDataException("The model registration exceeds 64 KiB.");
        using var reader = new StreamReader(stream);
        return DenseModelRegistration.Parse(reader.ReadToEnd(), "bonsai-1.7b");
    }
}
