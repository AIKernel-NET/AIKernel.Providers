using AIKernel.Kernel.Inference;

namespace AIKernel.Providers.LocalLlm;

internal static class LocalModelReader
{
    internal static DenseModelSession Open(string path, LocalLlmSettings settings, CancellationToken cancellation)
    {
        var options = settings as RegisteredLocalLlmSettings;
        var registration = ReadForModel(path, options?.RegistrationFile);
        var model = registration.OpenSession(path, settings.CpuThreads, settings.ContextTokens, options?.WeightMemory);
        if (cancellation.IsCancellationRequested)
        {
            model.Dispose();
            cancellation.ThrowIfCancellationRequested();
        }
        return model;
    }

    private static DenseModelRegistration ReadForModel(string model, string? profile) =>
        LocalRegistrationSource.Read(model, profile);
}
