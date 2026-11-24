using System.Text.Json.Nodes;
using AIKernel.Dtos.Capabilities;
using AIKernel.Kernel.Inference;
using AIKernel.Providers.LocalLlm;

namespace AIKernel.Providers.Tests;

public sealed class RegistrationTests
{
    private static string Profile()
    {
        using var stream = typeof(LocalLlmInvoker).Assembly.GetManifestResourceStream("AIKernel.LocalLlm.BonsaiRegistration")!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [Theory]
    [InlineData("extra", "unrecognized")]
    [InlineData("extends", "bonsai-1.7b")]
    [InlineData("modelId", "unknown")]
    [InlineData("version", "next")]
    [InlineData("tokenizerVersion", "next")]
    [InlineData("readerId", "onnx")]
    [InlineData("weightsSha256", "not-a-digest")]
    public void UnknownOrInvalidRegistrationFailsClosed(string field, string value)
    {
        var document = JsonNode.Parse(Profile())!;
        document[field] = value;
        Assert.Throws<InvalidDataException>(() => DenseModelRegistration.Parse(document.ToJsonString(), "bonsai-1.7b"));
    }

    [Theory]
    [InlineData("modelId", "\"bonsai-1.7b\"")]
    [InlineData("width", "2048")]
    public void DuplicateKeysAtEitherDepthAreRejected(string field, string value)
    {
        string key = $"\"{field}\":";
        string duplicate = Profile().Replace(key, $"{key}{value},{key}", StringComparison.Ordinal);
        Assert.Throws<InvalidDataException>(() => DenseModelRegistration.Parse(duplicate, "bonsai-1.7b"));
    }

    public static bool HasModel => File.Exists(Environment.GetEnvironmentVariable("AIKERNEL_TEST_BONSAI_GGUF"));

    [Fact(SkipUnless = nameof(HasModel), Skip = "Requires the registered Bonsai GGUF.")]
    public async Task RejectedArtifactsCanBeCorrectedOnTheSameInvoker()
    {
        var cancellation = TestContext.Current.CancellationToken;
        string filename = Path.GetTempFileName();
        try
        {
            using var invoker = new LocalLlmInvoker(new RegisteredLocalLlmSettings
            {
                BonsaiModelPath = Environment.GetEnvironmentVariable("AIKERNEL_TEST_BONSAI_GGUF"),
                RegistrationFile = filename, CpuThreads = 2, ContextTokens = 256, MaxNewTokens = 16,
                SystemPrompt = "Follow the user's formatting request."
            });
            var request = new CapabilityInvocationRequest("probe", "local-llm", "chat.local",
                new Dictionary<string,string> { ["prompt"] = "Reply with exactly the word BLUE." }, null, null, new Dictionary<string,string>());
            foreach (var field in new[] { "weightsSha256", "tokenizerSha256", "width" })
            {
                var document = JsonNode.Parse(Profile())!;
                if (field == "width") document["shape"]!["width"] = 1024;
                else document[field] = new string('0', 64);
                await File.WriteAllTextAsync(filename, document.ToJsonString(), cancellation);
                var rejected = await invoker.InvokeAsync(request, cancellation);
                Assert.Equal("LOCAL_LLM_MODEL_UNAVAILABLE", rejected.ErrorCode);
                Assert.False(rejected.Succeeded);
                Assert.Null(rejected.OutputHash);
            }
            await File.WriteAllTextAsync(filename, Profile(), cancellation);
            var recovered = await invoker.InvokeAsync(request, cancellation);
            Assert.True(recovered.Succeeded, recovered.ErrorMessage);
            Assert.Contains("BLUE", recovered.Metadata["text"], StringComparison.OrdinalIgnoreCase);
        }
        finally { File.Delete(filename); }
    }

    [Fact(SkipUnless = nameof(HasModel), Skip = "Requires the registered Bonsai GGUF.")]
    public async Task PagedStoragePreservesTokenIdsWithinCacheLimit()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            var registration = DenseModelRegistration.Parse(Profile(), "bonsai-1.7b");
            var options = new GgufMemoryOptions { WeightCacheBytes = 64L << 20, SwapDirectory = directory.FullName };
            using var model = registration.Open(Environment.GetEnvironmentVariable("AIKERNEL_TEST_BONSAI_GGUF")!, 2, 256, options);
            var generated = await model.GenerateBenchmarkAsync("Follow the user's formatting request.",
                "Reply with exactly the word BLUE.", 16, cancellation);
            using var mapped = new Qwen3CpuModel(Environment.GetEnvironmentVariable("AIKERNEL_TEST_BONSAI_GGUF")!, 2, 256, "auto");
            var expected = await mapped.GenerateBenchmarkAsync("Follow the user's formatting request.",
                "Reply with exactly the word BLUE.", 16, cancellation);
            Assert.NotNull(expected.GeneratedTokenSha256);
            Assert.Equal(expected.GeneratedTokenSha256, generated.GeneratedTokenSha256);
            Assert.InRange(model.WeightCacheCapacityBytes!.Value, 1, options.WeightCacheBytes);
            Assert.True(model.WeightBackingBytesRead > 0);
        }
        finally { directory.Delete(true); }
    }

    [Fact(SkipUnless = nameof(HasModel), Skip = "Requires the registered Bonsai GGUF.")]
    public async Task InheritedRegistrationVerifiesTheResolvedArtifactsOnOpen()
    {
        var stop = TestContext.Current.CancellationToken;
        var catalog = new JsonObject
        {
            ["profile"] = "release",
            ["profiles"] = new JsonObject
            {
                ["base"] = JsonNode.Parse(Profile()),
                ["release"] = JsonNode.Parse("""{"extends":"base","shape":{"layers":28}}""")
            }
        };
        var file = Path.GetTempFileName();
        try
        {
            using var invoker = new LocalLlmInvoker(new RegisteredLocalLlmSettings
            {
                RegistrationFile = file, BonsaiModelPath = Environment.GetEnvironmentVariable("AIKERNEL_TEST_BONSAI_GGUF"),
                CpuThreads = 2, ContextTokens = 256, MaxNewTokens = 16, SystemPrompt = "Follow the user's formatting request."
            });
            var request = new CapabilityInvocationRequest("catalog", "local-llm", "chat.local",
                new Dictionary<string, string> { ["prompt"] = "Reply with exactly the word BLUE." }, null, null,
                new Dictionary<string, string>());
            var delta = catalog["profiles"]!["release"]!.AsObject();
            delta["tokenizerSha256"] = new string('0', 64);
            await File.WriteAllTextAsync(file, catalog.ToJsonString(), stop);
            Assert.Equal("LOCAL_LLM_MODEL_UNAVAILABLE", (await invoker.InvokeAsync(request, stop)).ErrorCode);
            delta.Remove("tokenizerSha256");
            await File.WriteAllTextAsync(file, catalog.ToJsonString(), stop);
            var reply = await invoker.InvokeAsync(request, stop);
            Assert.True(reply.Succeeded, reply.ErrorMessage);
            Assert.Equal("sha256:24a866f4940f1b00083a71ba4df7323bac1486f28dc81d500bbc4bb02c6e0e16", reply.OutputHash);
        }
        finally { File.Delete(file); }
    }
}
