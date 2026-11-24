using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AIKernel.Dtos.Capabilities;
using AIKernel.Kernel.Inference;

namespace AIKernel.Providers.LocalLlm;

internal static class LocalLlmResponse
{
    private static string OutputHash(string text) => "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    internal static CapabilityInvocationResult Success(CapabilityInvocationRequest request, CpuGeneration generation)
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["provider"] = "LocalLlmProvider",
            ["operation"] = "chat.local",
            ["text"] = generation.Text,
            ["input_tokens"] = generation.InputTokens.ToString(CultureInfo.InvariantCulture),
            ["output_tokens"] = generation.OutputTokens.ToString(CultureInfo.InvariantCulture),
            ["finish_reason"] = generation.FinishReason,
            ["cpu_backend"] = generation.CpuBackend
        };
        string hash = OutputHash(generation.Text);
        return new(request.InvocationId, request.CapabilityId, true, hash, null, null, request.ReplayLogHash, metadata);
    }
}
