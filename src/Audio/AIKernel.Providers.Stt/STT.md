# AIKernel local speech recognition

`LocalSpeechRecognitionProvider` transcribes Japanese and English WAV audio locally with whisper.cpp. Each result carries the original audio duration, the resolved language, the recognized text and time-aligned segments. Each provider instance processes one request at a time.

## Model and settings

Download [ggml-small.bin](https://huggingface.co/ggerganov/whisper.cpp/blob/5359861c739e955e79d9a303bcbc70fb988958b1/ggml-small.bin) and set `ModelPath` to its local path. Initialization verifies the file length and SHA-256. The model is distributed separately from the package.

Use `LocalSpeechRecognitionSettings` directly or bind the host's `SpeechRecognition` section with `Microsoft.Extensions.Configuration.Binder`. Create a new provider instance to apply settings.

```json
{
  "SpeechRecognition": {
    "ModelPath": "models/ggml-small.bin",
    "MaxAudioDuration": "00:10:00",
    "MaxRequestBytes": 134217728,
    "CpuThreads": 4
  }
}
```

`CpuThreads` defaults to the smaller of four and the available logical processors. Zero uses whisper.cpp's default. The byte limit includes the WAV headers and chunks. The duration limit refers to the original audio.

```csharp
using AIKernel.Dtos.Providers.SpeechRecognition;
using AIKernel.Providers.Stt;
using Microsoft.Extensions.Configuration;

var settings = configuration.GetRequiredSection("SpeechRecognition").Get<LocalSpeechRecognitionSettings>()
    ?? throw new InvalidOperationException("Missing speech recognition configuration.");
await using var provider = new LocalSpeechRecognitionProvider(settings);
await provider.InitializeAsync();

await using var wave = File.OpenRead(wavePath);
var result = await provider.TranscribeAsync(new()
{
    Audio = new AudioSource.FromStream(wave, "audio/wav"),
    Language = "ja",
    Timestamps = TimestampGranularity.Segment
}, cancellationToken);
```

Use little-endian PCM16 WAV with one or two channels and a sample rate from 8 to 48 kHz. Inputs are downmixed and resampled to 16 kHz mono before recognition. The caller retains ownership of its stream.

`Language` accepts `ja` and `en` and defaults to detection. Japanese segment text is joined directly and English segment text is joined with a space. `Timestamps=None` returns full text with an empty segment list. Silence returns empty text and segments. Size and duration overflow fail with `provider.input_limit_exceeded`. Invalid WAV data fails with `asr.decode_failed`. Cancellation waits for native computation to stop and throws `OperationCanceledException`.

## Building the native bridge

Use [whisper.cpp v1.9.5](https://github.com/ggml-org/whisper.cpp/releases/tag/v1.9.5) at commit `d1be6fde11ac6e0407606b4e42fe72d34add8037`. Configure `native/` against that source with `WHISPER_SOURCE_DIR`. Build and install the `aikernel_whisper` target in Release mode to the corresponding `runtimes/<RID>/native` directory. The tested Windows x64 build uses AVX2, BMI2, FMA and F16C. The bridge statically links whisper.cpp and its CPU backend. See the third-party notices included in the package.

Pack the AIKernel.NET speech recognition contracts first. Pack this project with `-p:AsrRuntimeDirectory=<runtimes-directory>` and a restore source for the contracts. The build includes the native bridge for each supplied RID.
