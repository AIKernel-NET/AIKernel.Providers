namespace AIKernel.Providers.Audio.Tests;

using AIKernel.Providers.Audio.Base;
using AIKernel.Providers.Audio.Capabilities;
using AIKernel.Providers.Audio.DependencyInjection;
using AIKernel.Providers.Audio.Diagnostics;
using AIKernel.Providers.Audio.Routing;
using AIKernel.Providers.Audio.Validation;
using Microsoft.Extensions.DependencyInjection;

public sealed class AudioProviderTests
{
    [Fact]
    public void AudioFormatValidator_SupportedFormat_Succeeds()
    {
        var result = new AudioFormatValidator().Validate(new AudioFormat(), CreateCapability());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void AudioFormatValidator_UnsupportedEncoding_FailsClosed()
    {
        var result = new AudioFormatValidator().Validate(
            new AudioFormat { Encoding = "native-only" },
            CreateCapability());

        Assert.False(result.Succeeded);
        Assert.Equal(AudioProviderDiagnostics.FormatUnsupported, result.ErrorCode);
    }

    [Fact]
    public async Task AudioPlayBase_MissingPayload_ReturnsFailureEnvelope()
    {
        var provider = new TestAudioPlayProvider(CreateCapability());

        var result = await provider.PlayAsync(new AudioPlayRequest(), TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(AudioProviderDiagnostics.PayloadMissing, result.ErrorCode);
    }

    [Fact]
    public async Task AudioRecBase_InvalidDuration_ReturnsFailureEnvelope()
    {
        var provider = new TestAudioRecProvider(CreateCapability());

        var result = await provider.RecordAsync(new AudioRecordRequest(), TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(AudioProviderDiagnostics.DurationInvalid, result.ErrorCode);
    }

    [Fact]
    public void AudioProviderResolutionPolicy_Playback_UsesAudioCapability()
    {
        var policy = new AudioProviderResolutionPolicy().CreatePlaybackPolicy();

        Assert.Equal("audio.playback", policy.RequiredCapability);
    }

    [Fact]
    public async Task AudioRecBase_RecordCore_CanReturnProviderNeutralFrames()
    {
        var provider = new TestAudioRecProvider(CreateCapability());

        var result = await provider.RecordAsync(new AudioRecordRequest
        {
            Duration = TimeSpan.FromMilliseconds(20)
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(0, result.Frames.Single().FrameIndex);
        Assert.Equal(TimeSpan.Zero, result.Frames.Single().Timestamp);
        Assert.Equal(0, result.RecordFrames.Single().FrameIndex);
        Assert.Equal(0, result.RecordFrames.Single().SampleOffset);
        Assert.Equal(TimeSpan.Zero, result.RecordFrames.Single().Timestamp);
    }

    [Fact]
    public void AddAIKernelAudioProviderSubstrate_RegistersHelpers()
    {
        using var provider = new ServiceCollection()
            .AddAIKernelAudioProviderSubstrate()
            .BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<AudioFormatValidator>());
        Assert.NotNull(provider.GetRequiredService<AudioProviderResolutionPolicy>());
    }

    [Fact]
    public void AudioAssembly_DoesNotReferenceBackendSpecificPackages()
    {
        var references = typeof(AudioPlayBase)
            .Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain("NAudio", references);
        Assert.DoesNotContain("SDL", references);
        Assert.DoesNotContain("Microsoft.JSInterop", references);
        Assert.DoesNotContain("AIKernel.Wasm", references);
    }

    private static AudioCapabilityDescriptor CreateCapability()
        => new()
        {
            ProviderId = "test.audio",
            SupportsPlayback = true,
            SupportsRecording = true,
            SupportedEncodings = ["pcm16"],
            MinSampleRateHz = 8_000,
            MaxSampleRateHz = 48_000,
            MinChannels = 1,
            MaxChannels = 2
        };

    private sealed class TestAudioPlayProvider(AudioCapabilityDescriptor capability)
        : AudioPlayBase(capability)
    {
        protected override ValueTask<AudioProviderResult> PlayCoreAsync(
            AudioPlayRequest request,
            CancellationToken cancellationToken)
            => ValueTask.FromResult(new AudioProviderResult { Succeeded = true });
    }

    private sealed class TestAudioRecProvider(AudioCapabilityDescriptor capability)
        : AudioRecBase(capability)
    {
        protected override ValueTask<AudioRecordResult> RecordCoreAsync(
            AudioRecordRequest request,
            CancellationToken cancellationToken)
            => ValueTask.FromResult(new AudioRecordResult
            {
                Succeeded = true,
                Payload = [1, 2, 3],
                Frames =
                [
                    new AudioFrame
                    {
                        FrameIndex = 0,
                        Timestamp = TimeSpan.Zero,
                        Payload = [1, 2, 3]
                    }
                ],
                RecordFrames =
                [
                    new AudioRecordFrame
                    {
                        FrameIndex = 0,
                        SampleOffset = 0,
                        Timestamp = TimeSpan.Zero,
                        Payload = [1, 2, 3]
                    }
                ]
            });
    }
}
