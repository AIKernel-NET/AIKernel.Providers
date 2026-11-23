# AIKernel.Providers.Tts

Connect a user-provisioned text-to-speech engine to AIKernel 0.1.4 on .NET 10. The NuGet package includes the managed adapter, capability manifest, settings example and a loopback HTTP sample for Kokoro-82M. Provision the engine, model, voice files and device separately.

## Register and call

The provider and capability module ID are both `tts`. The operation is `tts.synthesize`. The descriptor declares `network.egress`. Supply the endpoint through trusted host configuration.

```csharp
using AIKernel.Abstractions.Capabilities;
using AIKernel.Core.Capabilities;
using AIKernel.Core.Providers;
using AIKernel.Dtos.Capabilities;
using AIKernel.Providers.Tts;

var tts = new TtsProvider(new TtsSettings
{
    Engine = "kokoro-82m",
    Endpoint = new Uri("http://127.0.0.1:8765/"),
    Language = "en-US",
    Voice = "af_heart"
});
await tts.InitializeAsync();
ICapabilityModuleRegistry capabilities = new InMemoryCapabilityModuleRegistry();
var providers = new InMemoryProviderRegistry(capabilities, [tts], [tts]);
await capabilities.RegisterAsync(tts.ToCapabilityDescriptor());
var descriptor = await capabilities.ResolveAsync("tts");
if (descriptor is null || !await tts.IsAvailableAsync())
    throw new InvalidOperationException("TTS is unavailable.");

ICapabilityModuleInvoker invoker = providers.GetRegisteredInvokers().OfType<TtsProvider>().Single();
var request = new CapabilityInvocationRequest(
    Guid.NewGuid().ToString("N"), descriptor.CapabilityId, "tts.synthesize",
    new Dictionary<string, string>
    {
        ["text"] = "The quick brown fox jumps over the lazy dog.",
        ["language"] = "en-US",
        ["voice"] = "af_bella",
        ["speed"] = "1.0"
    }, null, null, new Dictionary<string, string>());
var result = await invoker.InvokeAsync(request);
if (!result.Succeeded)
    throw new InvalidOperationException(result.ErrorCode);
await File.WriteAllBytesAsync("speech.wav", Convert.FromBase64String(result.Metadata["audio_base64"]));
```

With an existing host, use its capability and provider registries instead of creating new ones. Initialize the configured provider once, register its descriptor and route this operation to that provider's invoker.

The request requires `text` with 1–4096 UTF-16 code units. Omitted `language` uses the configured language, initially `en-US`. Omitted `voice` uses the configured voice and is required when no default exists. `speed` defaults to `1.0` and accepts invariant decimals from `0.5` to `2.0`. Kokoro's sample accepts US English and the available American-English stock voices, including `af_heart` and `af_bella`.

A success returns `provider`, `engine`, `voice`, `format=wav`, `sample_rate_hz`, `channels`, `sample_frames` and `audio_base64` in result metadata. `OutputHash` is `sha256:<lowercase hex>` over the WAV bytes. Decode the Base64 value immediately or persist the bytes in caller-owned storage. The result has no server-side artifact or expiration. Store only audit fields needed for tracing, with audio excluded from normal logs.

## Runtime protocol

Configure a base URL ending in `/`, with loopback HTTP or HTTPS. The Provider sends requests directly, without redirects, cookies or system proxies.

| Request | Response |
| --- | --- |
| `GET health` | On a ready runtime, `200` with JSON such as `{"engine":"kokoro-82m","languages":["en-US"],"voices":["af_heart","af_bella"]}`. The Provider verifies the engine and configured defaults. |
| `POST synthesize` | JSON body `{"text":"Hello.","language":"en-US","voice":"af_bella","speed":1.0}`. Return `200`, `Content-Type: audio/wav`, `X-Tts-Engine: kokoro-82m`, `X-Tts-Voice: af_bella` and 16-bit PCM RIFF WAV bytes. Mono and stereo are supported, up to 16 MiB. |
| Unsupported selection | `422` with `X-Tts-Error: unsupported_language` or `X-Tts-Error: unsupported_voice`. |

Error codes distinguish missing configuration (`TTS_NOT_CONFIGURED`), invalid settings (`TTS_INVALID_CONFIGURATION`), missing initialization (`TTS_NOT_INITIALIZED`), invalid arguments or operation (`TTS_INVALID_ARGUMENT`, `TTS_OPERATION_NOT_SUPPORTED`), unavailable endpoint (`TTS_ENDPOINT_UNAVAILABLE`), unsupported language or voice (`TTS_UNSUPPORTED_LANGUAGE`, `TTS_UNSUPPORTED_VOICE`), deadline (`TTS_TIMEOUT`), invalid response (`TTS_INVALID_RESPONSE`) and other runtime failures (`TTS_RUNTIME_FAILURE`). Calls cancelled by the caller throw `OperationCanceledException`. The deadline defaults to 30 seconds and covers the WAV body. The runtime may continue a synthesis after its HTTP client disconnects. Each failed call has no audio or output hash.

## Connect a T1000 runtime on Windows

The following PowerShell example uses an existing environment and placeholder paths. Set the UUID for the intended T1000 before launching the runtime so it appears as CUDA device 0. Use `--cuda-dll-directory` with the directory containing the compatible CUDA and cuDNN DLLs if your existing environment requires it.

```powershell
nvidia-smi --query-gpu=index,name,uuid --format=csv
$env:CUDA_DEVICE_ORDER = 'PCI_BUS_ID'
$env:CUDA_VISIBLE_DEVICES = 'GPU-00000000-0000-0000-0000-000000000000'
& 'C:\path\env\Scripts\python.exe' .\samples\kokoro_http.py --model 'C:\models\kokoro-v1.0.onnx' --voices 'C:\models\voices-v1.0.bin' --cuda --profile 'C:\logs\kokoro'
```

Confirm the startup provider list, run the same text with `af_heart` and `af_bella`, then stop the sample with Ctrl+C and inspect the ONNX Runtime profile for `CUDAExecutionProvider` node events. Record the selected GPU UUID, provider counts, WAV format and SHA-256. Auxiliary operations may use the CPU. The sample binds to 127.0.0.1 and is intended for a trusted local client. Device allocation and runtime licensing belong to the deployment. [Kokoro model card](https://huggingface.co/hexgrad/Kokoro-82M) · [ONNX Runtime CUDA settings](https://onnxruntime.ai/docs/execution-providers/CUDA-ExecutionProvider.html)
