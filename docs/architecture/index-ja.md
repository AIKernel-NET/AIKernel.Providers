# AIKernel.Providers Architecture

[English](index.md)

AIKernel.Providers は、AIKernel.NET 向けの公式外部 Provider layer です。
service-specific、runtime-specific、native-driver-specific な logic を
AIKernel.Core と AIKernel.Tools の外へ分離します。

## Layer Boundary

- `AIKernel.NET` / `AIKernel.Abstractions` は compute、process、network、
  logging、routing、VFS、EventBus、Provider contract などの syscall-level
  OS contract を定義します。
- `AIKernel.Core` は deterministic pipeline behavior、DSL、VFS-backed
  capability、semantic routing、Core.Control / Bonsai rule evaluation を
  実装します。
- `AIKernel.Providers.Standard` は CPU compute、file system、logging、
  process supervision、network、scheduler、profiler、EventBus の host-side
  OS driver を実装します。
- `AIKernel.Wasm` は browser / WebAssembly runtime Provider と WASM WebGPU
  backend を所有します。
- `AIKernel.Tools` は replay、inspection、canonical formatting、CLI などの
  instrumentation utility を提供します。
- 外部 `AIKernel.Providers` project は公式 extension driver と provider
  manifest を実装します。

この repository は第二の Core runtime になってはいけません。Provider は
capability と invoker を公開し、contract model は Core が保持します。

## Inclusion Boundary

AIKernel.Providers には provider substrate、manifest、registry、router、
deterministic fallback policy、runtime-configurable provider、pure managed
multi-platform .NET dependency を置けます。

ビルド時に native runtime、OS SDK、browser/WASM runtime、vendor SDK、scenario
runtime、または別の top-level AIKernel implementation repository へ固定依存する
Provider は dedicated package / repository に分離します。

[Provider development guidelines](../guidelines/provider-development-guidelines-ja.md) と
[Dependency boundary checklist](../guidelines/dependency-boundary-checklist-ja.md) を参照してください。

## Provider Type

AIKernel.Providers には、現時点で以下の Provider category が含まれます。

- OpenAI 互換 remote model provider
- Microsoft.Extensions.AI based managed provider
- Local LLM runtime provider
- CUDA compute などの native compute provider
- semantic pipeline construction 向け dynamic compiler provider
- deterministic context record 向け chat history provider

## Standard OS Driver

`AIKernel.Providers.Standard` は AIKernel OS における Linux の `drivers/` 相当です。
以下を公開します。

- deterministic local compute と CPU fallback 向け `CpuComputeProvider`
- Core の `IFileSystemProvider` alias 経由で利用する
  `MemoryFileSystemProvider`、`PhysicalFileSystemProvider`、`ZipFileSystemProvider`
- `ConsoleLoggingProvider` と `FileLoggingProvider`
- process supervision と process hosting を実装する `DefaultProcessSupervisorProvider`
- `HttpNetworkProvider` と `WebSocketNetworkProvider`
- `SchedulerProvider`、`ProfilerProvider`、`EventBusProvider`

これらは `AddAIKernelStandardProviders()` で登録され、WASM 固有の runtime
concern から分離されます。

## WASM Runtime Layer

`AIKernel.Wasm` は browser / WebAssembly layer を完成させます。WASM process
lifecycle、linear memory、stdin、file surface、event、audio buffer、
screenshot / framebuffer capture、save-state、deterministic time control、
JavaScript interop backend 経由の WebGPU compute を実装します。WebGPU が利用できない
場合、compute Provider は `AIKernel.Providers.Standard.Compute.CpuComputeProvider`
へ fallback execution を委譲します。

## CLI OS Command

`AIKernel.Tools` は `aik` command surface 経由で OS operation を公開します。

- `aik ps`、`aik kill <pid-or-name>`、`aik restart <pid-or-name>`
- `aik logs <process>`
- `aik gpu list`、`aik gpu run vector-add --a a.bin --b b.bin`
- `aik schedule add --every 1m "aik system info"`

CLI command layer は Core contract と standard Provider の operational surface
であり、別 runtime ではありません。

## Dynamic Loading

Provider manifest file は、host や CLI が動的ロードする assembly と capability
を記述します。manifest は小さく決定論的で、provider、assembly、公開
capability、metadata、CLI hint を識別します。

host は manifest を provider directory へコピーし、対応 assembly を解決して、
AIKernel capability registry に provider を登録できます。

`AIKernel.Providers.Substrate` は、この flow で使う shared manifest loader、
validator、registry、deterministic router を提供します。Resolution は missing
provider、duplicate provider、明示的 fallback selection を structured result として返します。

Forward-compatible manifest は任意の `capabilities`、`metadata`、
`backendMetadata`、`vendorMetadata`、`cli`、未知 JSON block を追加できます。
未知 JSON は raw extension JSON として保持されるため、将来の schema version が field を
追加しても古い loader を壊しません。

## Council, Audio, And Compute Substrates

Council Provider は semantic material と diagnostics のみを出力します。downstream
decision や control-state semantics は所有しません。

Audio substrate 型は、backend dependency を持たずに audio format、frame、playback
request、recording request、capability descriptor、validation を表します。NAudio、
SDL、WebAudio、WASM、OS-specific audio stack は dedicated package が所有します。

Compute substrate 型は、標準化された dtype string、comma-separated shape / stride
string、任意 layout、backend-specific detail 用 metadata map により tensor-like
buffer reference を表します。

## MicrosoftAI Migration

`AIKernel.Providers.MicrosoftAI` は、0.1.1 公開からこの repository で管理します。
この実装は AIKernel.Core から移管されました。これにより Core は外部 Provider
packaging や Microsoft.Extensions.AI integration detail を所有しません。

移管後も Provider contract boundary は維持されますが、所有場所が変わります。

- Core は abstraction と runtime contract を保持します。
- AIKernel.Providers は MicrosoftAI Provider implementation、test、package
  metadata、documentation、Python wrapper 参照資料を所有します。

## Dependency Rule

- Provider は AIKernel.NET contract と AIKernel.Core に依存できます。
- Provider は AIKernel.Tools に依存してはいけません。
- Core は外部 Provider に依存してはいけません。
- Tools は必要に応じて Provider を NuGet package として利用できますが、
  Provider 固有 capability implementation を含めてはいけません。

## Python Boundary

`aikernel-providers` は、0.1.3 正典シリーズで同じ Provider boundary を Python host
向けに公開する wrapper 名です。Python packaging では managed assembly と manifest
JSON file を pythonnet 経由で読み込み、薄い wrapper object を公開します。

Python code は Provider semantics を再実装しません。public C# package surface
へ委譲します。
