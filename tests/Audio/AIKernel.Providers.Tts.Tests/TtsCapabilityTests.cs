using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using AIKernel.Abstractions.Capabilities;
using AIKernel.Core.Capabilities;
using AIKernel.Core.Providers;
using AIKernel.Dtos.Capabilities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace AIKernel.Providers.Tts.Tests;

public sealed class TtsCapabilityTests
{
    private const string Sentence = "The quick brown fox jumps over the lazy dog.";
    private const string ReplayHash = "replay-reference";
    private static CancellationToken TestCancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RegistersDiscoversAndInvokesThroughThePublishedCapabilityContracts()
    {
        var wave = Wave();
        await using var host = await StartAsync(async context =>
        {
            using var json = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted);
            Assert.Equal(Sentence, json.RootElement.GetProperty("text").GetString());
            Assert.Equal("en-US", json.RootElement.GetProperty("language").GetString());
            Assert.Equal("af_heart", json.RootElement.GetProperty("voice").GetString());
            Assert.Equal(1, json.RootElement.GetProperty("speed").GetDouble());
            await WriteWaveAsync(context, wave);
        });
        var provider = new TtsProvider(new TtsSettings { Endpoint = Endpoint(host), Voice = "af_heart" });
        await provider.InitializeAsync();
        Assert.True(await provider.IsAvailableAsync());
        ICapabilityModuleRegistry capabilities = new InMemoryCapabilityModuleRegistry();
        var providers = new InMemoryProviderRegistry(capabilities, [provider], [provider]);
        await capabilities.RegisterAsync(provider.ToCapabilityDescriptor(), TestCancellation);

        var descriptor = Assert.IsType<CapabilityModuleDescriptor>(await capabilities.ResolveAsync("tts", TestCancellation));
        Assert.Contains("tts.synthesize", descriptor.ProvidedOperations);
        Assert.Equal("0.1.4", descriptor.Version);
        ICapabilityModuleInvoker invoker = Assert.Single(providers.GetRegisteredInvokers());
        var result = await invoker.InvokeAsync(Request(), TestCancellation);

        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.Equal(ReplayHash, result.ReplayLogHash);
        Assert.Equal("sha256:" + Convert.ToHexStringLower(SHA256.HashData(wave)), result.OutputHash);
        Assert.Equal(wave, Convert.FromBase64String(result.Metadata["audio_base64"]));
        Assert.Equal("24000", result.Metadata["sample_rate_hz"]);
        Assert.Equal("4", result.Metadata["sample_frames"]);
        Assert.Equal("af_heart", result.Metadata["voice"]);
        Assert.Equal("kokoro-82m", result.Metadata["engine"]);
        Assert.Equal("tts", result.Metadata["provider"]);
        await provider.ShutdownAsync();
        Assert.False(await provider.IsAvailableAsync());
    }

    [Theory]
    [InlineData("unsupported_voice", "TTS_UNSUPPORTED_VOICE")]
    [InlineData("unsupported_language", "TTS_UNSUPPORTED_LANGUAGE")]
    public async Task RuntimeRejectionRemovesAllAudioEvenWithSpoofedRequestMetadata(string runtimeError, string code)
    {
        await using var host = await StartAsync(context =>
        {
            context.Response.StatusCode = 422;
            context.Response.Headers["X-Tts-Error"] = runtimeError;
            return Task.CompletedTask;
        });
        var result = await (await ConfigureAsync(Endpoint(host))).InvokeAsync(Request("af_unknown"), TestCancellation);
        AssertFailure(result, code);
    }

    [Fact]
    public async Task ValidatesResponseIdentityAndWave()
    {
        await using var host = await StartAsync(async context =>
        {
            using var json = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted);
            var voice = json.RootElement.GetProperty("voice").GetString();
            if (voice == "af_bella")
                await WriteWaveAsync(context, Wave(), "af_heart");
            else
                await WriteWaveAsync(context, new byte[44]);
        });
        var provider = await ConfigureAsync(Endpoint(host));
        AssertFailure(await provider.InvokeAsync(Request("af_bella"), TestCancellation), "TTS_INVALID_RESPONSE");
        AssertFailure(await provider.InvokeAsync(Request("af_heart"), TestCancellation), "TTS_INVALID_RESPONSE");
    }

    [Fact]
    public async Task SeparatesUnconfiguredInvalidSettingsAndUnreachableEndpoint()
    {
        var provider = new TtsProvider();
        await provider.InitializeAsync();
        Assert.False(await provider.IsAvailableAsync());
        AssertFailure(await provider.InvokeAsync(Request(), TestCancellation), "TTS_NOT_CONFIGURED");

        var invalid = await ConfigureAsync(new Uri("http://example.invalid:8765/"));
        AssertFailure(await invalid.InvokeAsync(Request(), TestCancellation), "TTS_INVALID_CONFIGURATION");

        using var unused = new TcpListener(IPAddress.Loopback, 0);
        unused.Start();
        var port = ((IPEndPoint)unused.LocalEndpoint).Port;
        unused.Stop();
        var unreachable = await ConfigureAsync(new Uri($"http://127.0.0.1:{port}/"));
        Assert.False(await unreachable.IsAvailableAsync());
        AssertFailure(await unreachable.InvokeAsync(Request(), TestCancellation), "TTS_ENDPOINT_UNAVAILABLE");
    }

    [Fact]
    public async Task DistinguishesCallerCancellationFromTheProviderDeadlineWhileReadingAudio()
    {
        var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var host = await StartAsync(async context =>
        {
            context.Response.ContentType = "audio/wav";
            context.Response.Headers["X-Tts-Engine"] = "kokoro-82m";
            context.Response.Headers["X-Tts-Voice"] = "af_heart";
            await context.Response.StartAsync(context.RequestAborted);
            received.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, context.RequestAborted);
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { }
        });
        var shortTimeout = await ConfigureAsync(Endpoint(host), TimeSpan.FromMilliseconds(300));
        AssertFailure(await shortTimeout.InvokeAsync(Request(), TestCancellation), "TTS_TIMEOUT");

        received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var provider = await ConfigureAsync(Endpoint(host));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestCancellation);
        var pending = provider.InvokeAsync(Request(), cancellation.Token).AsTask();
        await received.Task.WaitAsync(TimeSpan.FromSeconds(5), TestCancellation);
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }

    private static void AssertFailure(CapabilityInvocationResult result, string code)
    {
        Assert.False(result.Succeeded);
        Assert.Equal(code, result.ErrorCode);
        Assert.Null(result.OutputHash);
        Assert.DoesNotContain("audio_base64", result.Metadata.Keys);
    }

    private static CapabilityInvocationRequest Request(string? voice = null)
    {
        var arguments = new Dictionary<string, string> { ["text"] = Sentence };
        if (voice is not null)
            arguments["voice"] = voice;
        return new("audio-test", "tts", "tts.synthesize", arguments, null, ReplayHash,
            new Dictionary<string, string> { ["audio_base64"] = "untrusted" });
    }

    private static async Task<TtsProvider> ConfigureAsync(Uri uri, TimeSpan? timeout = null)
    {
        var provider = new TtsProvider(new TtsSettings
        {
            Endpoint = uri,
            Voice = "af_heart",
            Timeout = timeout ?? TimeSpan.FromSeconds(5)
        });
        await provider.InitializeAsync();
        return provider;
    }

    private static Uri Endpoint(WebApplication app) => new(app.Urls.Single().TrimEnd('/') + "/");

    private static async Task<WebApplication> StartAsync(RequestDelegate synthesize)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(server => server.Listen(IPAddress.Loopback, 0));
        var app = builder.Build();
        app.MapGet("/health", () => Results.Json(new
        {
            engine = "kokoro-82m", languages = new[] { "en-US" }, voices = new[] { "af_heart", "af_bella" }
        }));
        app.MapPost("/synthesize", synthesize);
        await app.StartAsync(TestCancellation);
        return app;
    }

    private static byte[] Wave()
        => Convert.FromHexString("524946462C00000057415645666D74201000000001000100C05D000080BB00000200100064617461080000001000F0FF1000F0FF");

    private static async Task WriteWaveAsync(HttpContext context, byte[] bytes, string voice = "af_heart")
    {
        context.Response.Headers["X-Tts-Engine"] = "kokoro-82m";
        context.Response.Headers["X-Tts-Voice"] = voice;
        context.Response.ContentType = "audio/wav";
        await context.Response.Body.WriteAsync(bytes, context.RequestAborted);
    }
}
