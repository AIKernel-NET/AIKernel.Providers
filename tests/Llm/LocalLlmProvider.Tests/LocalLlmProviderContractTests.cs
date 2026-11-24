using System.Security.Cryptography;
using System.Text;
using AIKernel.Dtos.Capabilities;
using AIKernel.Enums;
using AIKernel.Kernel.Inference;
using AIKernel.Providers.LocalLlm;

namespace AIKernel.Providers.Tests;

public sealed class LocalLlmProviderContractTests
{
    [Fact]
    public void ToContract_ExposesLocalLlmOperations()
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["version"] = "0.1.3",
            ["runtime"] = "ollama",
            ["runtime_uri"] = "ollama://localhost"
        };

        var contract = LocalLlmCapabilityContracts.ToContract(
            new LocalLlmCapabilityDescriptor(
                "local-llm",
                "ollama",
                metadata));

        Assert.Equal("local-llm", contract.CapabilityId);
        Assert.Equal("Local LLM Provider", contract.Name);
        Assert.Equal(CapabilityModuleKind.ManagedAssembly, contract.Kind);
        Assert.Equal(CapabilityInvocationMode.AssemblyReference, contract.InvocationMode);
        Assert.Equal("ollama", contract.EntryPoint);
        Assert.Equal("ollama://localhost", contract.ArtifactUri);
        Assert.Equal(["chat.local"], contract.ProvidedOperations);
        Assert.Equal(["local.process", "llm.local"], contract.RequiredPermissions);
    }

    [Fact]
    public async Task Provider_LifecycleAndCapabilitiesRemainContractPure()
    {
        var provider = new global::AIKernel.Providers.LocalLlm.LocalLlmProvider(new LocalLlmSettings
        {
            ProviderId = "local-llm",
            Runtime = "ollama"
        });

        Assert.False(await provider.IsAvailableAsync());
        await provider.InitializeAsync();

        Assert.True(await provider.IsAvailableAsync());
        Assert.True(provider.GetCapabilities().SupportsOperation("chat.local"));
        Assert.False(provider.GetCapabilities().SupportsOperation("embedding.local"));
        Assert.False(provider.GetCapabilities().SupportsEmbedding);
        Assert.Equal("local-llm", provider.ToCapabilityDescriptor().CapabilityId);

        await provider.ShutdownAsync();
        Assert.False(await provider.IsAvailableAsync());
    }

    [Fact]
    public void ProviderAssembly_DoesNotReferenceTools()
    {
        var referenced = typeof(global::AIKernel.Providers.LocalLlm.LocalLlmProvider).Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name)
            .ToArray();

        Assert.DoesNotContain("AIKernel.Tools", referenced);
        Assert.DoesNotContain(referenced, name => name is not null && name.StartsWith("AIKernel.Tools.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Invoker_RejectsUnsupportedOperationFailClosed()
    {
        using var invoker = new LocalLlmInvoker();

        var result = await invoker.InvokeAsync(new CapabilityInvocationRequest(
            "invoke-1",
            "local-llm",
            "unknown.operation",
            new Dictionary<string, string>(),
            null,
            "sha256:replay",
            new Dictionary<string, string>()),
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("LOCAL_LLM_OPERATION_NOT_SUPPORTED", result.ErrorCode);
    }

    [Fact]
    public async Task Invoker_RejectsEmbeddingWithoutRegisteredModel()
    {
        using var invoker = new LocalLlmInvoker();
        var result = await invoker.InvokeAsync(new CapabilityInvocationRequest(
            "invoke-2", "local-llm", "embedding.local", new Dictionary<string, string>(),
            null, "sha256:replay", new Dictionary<string, string> { ["input"] = "private text" }),
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Null(result.OutputHash);
        Assert.Equal("provider.model_not_registered", result.ErrorCode);
        Assert.False(result.Metadata.ContainsKey("input"));
    }

    [Fact]
    public async Task Chat_RequiresRegisteredModelAndValidArguments()
    {
        var cancellation = TestContext.Current.CancellationToken;
        using var unconfigured = new LocalLlmInvoker(new LocalLlmSettings());
        var missing = await unconfigured.InvokeAsync(Chat(new() { ["prompt"] = "Hello" }), cancellation);
        Assert.False(missing.Succeeded);
        Assert.Equal("provider.model_not_registered", missing.ErrorCode);
        Assert.Null(missing.OutputHash);

        using var configured = new LocalLlmInvoker(new LocalLlmSettings
        {
            BonsaiModelPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".gguf"),
            MaxPromptCharacters = 5,
            MaxNewTokens = 8
        });
        foreach (var arguments in new Dictionary<string, string>[]
        {
            new(),
            new() { ["prompt"] = "too long" },
            new() { ["prompt"] = "Hello", ["max_new_tokens"] = "9" },
            new() { ["prompt"] = "Hello", ["max_new_tokens"] = "-1" },
            new() { ["prompt"] = "Hello", ["model_path"] = "request-supplied.gguf" }
        })
        {
            var invalid = await configured.InvokeAsync(Chat(arguments), cancellation);
            Assert.False(invalid.Succeeded);
            Assert.Equal("LOCAL_LLM_INVALID_ARGUMENT", invalid.ErrorCode);
            Assert.Single(invalid.Metadata);
        }

        var absent = await configured.InvokeAsync(Chat(new() { ["prompt"] = "Hello" }), cancellation);
        Assert.False(absent.Succeeded);
        Assert.Equal("provider.model_not_registered", absent.ErrorCode);

        using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        await cancelled.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => configured.InvokeAsync(Chat(new()), cancelled.Token).AsTask());
    }

    public static bool HasBonsaiModel => File.Exists(Environment.GetEnvironmentVariable("AIKERNEL_TEST_BONSAI_GGUF"));

    [Fact]
    public async Task Chat_RejectsAnUnreadableModelFormat()
    {
        var cancellation = TestContext.Current.CancellationToken;
        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, "not a GGUF model", cancellation);
            using var invoker = new LocalLlmInvoker(new LocalLlmSettings { BonsaiModelPath = file });

            var result = await invoker.InvokeAsync(Chat(new() { ["prompt"] = "Hello" }), cancellation);

            Assert.False(result.Succeeded);
            Assert.Equal("LOCAL_LLM_MODEL_UNAVAILABLE", result.ErrorCode);
            Assert.Null(result.OutputHash);
            Assert.Single(result.Metadata);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact(SkipUnless = nameof(HasBonsaiModel), Skip = "Set AIKERNEL_TEST_BONSAI_GGUF to run the CPU model check.")]
    public async Task Chat_GeneratesBonsaiReplyAndRecoversAfterCancellation()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var settings = new LocalLlmSettings
        {
            BonsaiModelPath = Environment.GetEnvironmentVariable("AIKERNEL_TEST_BONSAI_GGUF"),
            CpuThreads = 2,
            ContextTokens = 256,
            MaxNewTokens = 96,
            SystemPrompt = "Follow the user's formatting request."
        };
        var provider = new global::AIKernel.Providers.LocalLlm.LocalLlmProvider(settings);
        using var invoker = provider.CreateInvoker();
        const string prompt = "Reply with exactly the word BLUE.";
        var request = Chat(new() { ["prompt"] = prompt, ["max_new_tokens"] = "16" });
        var result = await invoker.InvokeAsync(request, cancellation);

        Assert.True(result.Succeeded, result.ErrorMessage);
        var text = result.Metadata["text"];
        Assert.Contains("BLUE", text, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text))), result.OutputHash);
        Assert.Equal(request.ReplayLogHash, result.ReplayLogHash);
        Assert.DoesNotContain("input", result.Metadata.Keys);
        Assert.InRange(int.Parse(result.Metadata["output_tokens"]), 1, 16);
        Assert.False(string.IsNullOrEmpty(result.Metadata["cpu_backend"]));

        using (var reference = new Qwen3CpuModel(settings.BonsaiModelPath!, settings.CpuThreads, settings.ContextTokens, "auto"))
        {
            var direct = await reference.GenerateAsync(settings.SystemPrompt, prompt, 16, cancellation);
            Assert.Equal(direct.Text, text);
            Assert.Equal(direct.CpuBackend, result.Metadata["cpu_backend"]);
        }

        var longPrompt = Chat(new() { ["prompt"] = "Write a long explanation of how calendars work." });
        using var interrupted = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        var pending = invoker.InvokeAsync(longPrompt, interrupted.Token).AsTask();
        await Task.Delay(25, cancellation);
        Assert.False(pending.IsCompleted);
        await interrupted.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);

        var recovered = await invoker.InvokeAsync(request, cancellation);
        Assert.True(recovered.Succeeded, recovered.ErrorMessage);
        Assert.Equal(result.OutputHash, recovered.OutputHash);

        var oversizedContext = await invoker.InvokeAsync(
            Chat(new() { ["prompt"] = string.Concat(Enumerable.Repeat("ä café number 12 ", 160)) }), cancellation);
        Assert.False(oversizedContext.Succeeded);
        Assert.Equal("LOCAL_LLM_INVALID_ARGUMENT", oversizedContext.ErrorCode);
        Assert.Single(oversizedContext.Metadata);
    }

    public static bool HasManifestModel => File.Exists(Environment.GetEnvironmentVariable("AIKERNEL_LOCAL_LLM_MODEL_PATH"));

    [Fact(SkipUnless = nameof(HasManifestModel), Skip = "Set AIKERNEL_LOCAL_LLM_MODEL_PATH to run the manifest invoker check.")]
    public async Task ManifestInvoker_GeneratesJapaneseReply()
    {
        using var invoker = new LocalLlmInvoker();
        var result = await invoker.InvokeAsync(Chat(new()
        {
            ["prompt"] = "「こんにちは」とだけ答えてください。",
            ["max_new_tokens"] = "16"
        }), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Contains("こんにちは", result.Metadata["text"], StringComparison.Ordinal);
    }

    private static CapabilityInvocationRequest Chat(Dictionary<string, string> arguments)
        => new("chat-1", "local-llm", "chat.local", arguments, null, "sha256:replay",
            new Dictionary<string, string> { ["input"] = "private request metadata" });

    [Fact]
    public void ProviderManifest_IncludesCliInstallSettings()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "local-llm.provider.json");

        Assert.True(File.Exists(path));

        var json = File.ReadAllText(path);
        Assert.Contains("\"cli\"", json);
        Assert.Contains("\"command\": \"local-llm\"", json);
        Assert.Contains("\"defaultOperation\": \"chat.local\"", json);
    }
}
