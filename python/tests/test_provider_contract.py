from aikernel_providers import (
    ChatOpenAICapability,
    ChatOpenAIClient,
    ChatOpenAIInvoker,
    ChatOpenAIProvider,
    ChatOpenAISettings,
    ChatHistory,
    ChatHistoryCapability,
    ChatHistoryInvoker,
    ChatHistoryProvider,
    ChatHistoryRecord,
    ChatHistorySettings,
    ChatMessage,
    ConsoleLoggingProvider,
    CpuComputeProvider,
    CudaComputeCapability,
    CudaComputeInvoker,
    CudaComputeProvider,
    CudaComputeSettings,
    DefaultProcessSupervisorProvider,
    DynamicPipelineCompilerCapability,
    DynamicPipelineCompilerInvoker,
    DynamicPipelineCompilerProvider,
    DynamicPipelineCompilerSettings,
    EventBusProvider,
    FileLoggingProvider,
    HttpNetworkProvider,
    LocalLlmCapability,
    LocalLlmInvoker,
    LocalLlmProvider,
    LocalLlmSettings,
    MemoryFileSystemProvider,
    managed_api_summary,
    managed_type_names,
    MicrosoftAICredential,
    MicrosoftAIHealthContext,
    MicrosoftAIProvider,
    MicrosoftAIProviderCapabilities,
    MicrosoftAIProviderOptions,
    MicrosoftAIProviderOptionsValidator,
    MicrosoftAIResponseMapper,
    MicrosoftAIResponseProjection,
    NetworkProvider,
    PhysicalFileSystemProvider,
    ProcessSupervisorProvider,
    ProfilerProvider,
    SchedulerProvider,
    WebSocketNetworkProvider,
    ZipFileSystemProvider,
    microsoft_ai_assembly_path,
    microsoft_ai_exception_types,
    provider_assemblies,
    standard_driver_contracts,
)


def test_public_import_surface():
    assert ChatOpenAICapability
    assert ChatOpenAISettings
    assert ChatOpenAIClient
    assert ChatOpenAIProvider
    assert ChatOpenAIInvoker
    assert ChatHistory
    assert ChatHistoryCapability
    assert ChatHistoryProvider
    assert ChatHistoryInvoker
    assert ChatHistoryRecord
    assert ChatHistorySettings
    assert ChatMessage
    assert ConsoleLoggingProvider
    assert CpuComputeProvider
    assert CudaComputeCapability
    assert CudaComputeProvider
    assert CudaComputeInvoker
    assert CudaComputeSettings
    assert DefaultProcessSupervisorProvider
    assert DynamicPipelineCompilerCapability
    assert DynamicPipelineCompilerProvider
    assert DynamicPipelineCompilerInvoker
    assert DynamicPipelineCompilerSettings
    assert EventBusProvider
    assert FileLoggingProvider
    assert HttpNetworkProvider
    assert LocalLlmCapability
    assert LocalLlmProvider
    assert LocalLlmInvoker
    assert LocalLlmSettings
    assert MemoryFileSystemProvider
    assert MicrosoftAICredential
    assert MicrosoftAIHealthContext
    assert MicrosoftAIProvider
    assert MicrosoftAIProviderCapabilities
    assert MicrosoftAIProviderOptions
    assert MicrosoftAIProviderOptionsValidator
    assert MicrosoftAIResponseMapper
    assert MicrosoftAIResponseProjection
    assert NetworkProvider
    assert PhysicalFileSystemProvider
    assert ProcessSupervisorProvider
    assert ProfilerProvider
    assert SchedulerProvider
    assert WebSocketNetworkProvider
    assert ZipFileSystemProvider


def test_chat_openai_capability_contract_calls_managed_mapper():
    contract = ChatOpenAICapability("openai.chat").to_contract()

    assert contract.capability_id == "openai.chat"
    assert contract.invocation_mode == "Remote"
    assert contract.provided_operations == (
        "chat.completion",
        "embedding",
        "moderation",
    )
    assert contract.metadata["endpoint"] == "https://api.openai.com/v1"


def test_provider_and_invoker_wrappers_create_managed_objects():
    provider = ChatOpenAIProvider.create()
    invoker = ChatOpenAIInvoker.create()
    history_provider = ChatHistoryProvider.create()
    history_invoker = ChatHistoryInvoker.create()
    cuda_provider = CudaComputeProvider.create()
    cuda_invoker = CudaComputeInvoker.create()
    pipeline_provider = DynamicPipelineCompilerProvider.create()
    pipeline_invoker = DynamicPipelineCompilerInvoker.create()
    local_provider = LocalLlmProvider.create()
    local_invoker = LocalLlmInvoker.create()

    assert provider.provider_id
    assert provider.version == "0.1.1"
    assert invoker.managed is not None
    assert history_provider.provider_id
    assert history_provider.version == "0.1.1"
    assert history_invoker.managed is not None
    assert cuda_provider.provider_id
    assert cuda_provider.version == "0.1.1"
    assert cuda_invoker.managed is not None
    assert pipeline_provider.provider_id
    assert pipeline_provider.version == "0.1.1"
    assert pipeline_invoker.managed is not None
    assert local_provider.provider_id
    assert local_provider.version == "0.1.1"
    assert local_invoker.managed is not None


def test_cuda_compute_capability_contract_calls_managed_mapper():
    contract = CudaComputeCapability("cuda.compute").to_contract()

    assert contract.capability_id == "cuda.compute"
    assert contract.invocation_mode == "NativeAbi"
    assert contract.provided_operations == (
        "tensor.matmul",
        "tensor.softmax",
        "tensor.conv2d",
        "tensor.layernorm",
    )
    assert contract.metadata["device_profile"] == "cuda13"


def test_chat_history_capability_contract_calls_managed_mapper():
    contract = ChatHistoryCapability("chat-history").to_contract()

    assert contract.capability_id == "chat-history"
    assert contract.invocation_mode == "AssemblyReference"
    assert contract.provided_operations == (
        "chat.history.read",
        "chat.history.filter",
        "chat.history.latest",
    )


def test_dynamic_pipeline_capability_contract_calls_managed_mapper():
    contract = DynamicPipelineCompilerCapability("dynamic-pipeline").to_contract()

    assert contract.capability_id == "dynamic-pipeline"
    assert contract.invocation_mode == "AssemblyReference"
    assert contract.provided_operations == (
        "pipeline.compile",
        "pipeline.execute",
        "pipeline.validate",
    )
    assert contract.metadata["dsl_schema_version"] == "0.1"


def test_local_llm_capability_contract_calls_managed_mapper():
    contract = LocalLlmCapability("local-llm").to_contract()

    assert contract.capability_id == "local-llm"
    assert contract.invocation_mode == "AssemblyReference"
    assert contract.provided_operations == (
        "chat.local",
        "embedding.local",
    )
    assert contract.metadata["runtime"] == "ollama"


def test_provider_assembly_manifest_names():
    names = {path.name for path in provider_assemblies().assemblies}

    assert "ChatOpenAIProvider.dll" in names
    assert "ChatHistoryProvider.dll" in names
    assert "CudaComputeProvider.dll" in names
    assert "DynamicPipelineCompilerProvider.dll" in names
    assert "LocalLlmProvider.dll" in names
    assert "AIKernel.Providers.MicrosoftAI.dll" in names
    assert "AIKernel.Providers.Standard.dll" in names
    assert "AIKernel.Core.dll" in names
    assert "AIKernel.Dtos.dll" in names


def test_standard_driver_contracts_cover_public_standard_providers():
    provider_ids = {contract.provider_id for contract in standard_driver_contracts()}

    assert provider_ids == {
        "providers.compute.cpu",
        "providers.eventbus.memory",
        "providers.fs.memory",
        "providers.fs.physical",
        "providers.fs.zip",
        "providers.logging.console",
        "providers.logging.file",
        "providers.network.http",
        "providers.network.standard",
        "providers.network.websocket",
        "providers.process.supervisor",
        "providers.process.supervisor.default",
        "providers.profiler.standard",
        "providers.scheduler.standard",
    }


def test_microsoft_ai_options_wrapper_resolves_managed_type():
    options = MicrosoftAIProviderOptions.create()

    assert options.provider_id == "openai-compatible"
    assert options.name == "OpenAI Compatible Provider"
    assert options.version == "0.1.1"
    assert options.max_input_tokens == 8192
    assert options.stop_sequences == ()
    assert microsoft_ai_assembly_path().name == "AIKernel.Providers.MicrosoftAI.dll"


def test_public_settings_wrappers_cover_provider_metadata():
    openai = ChatOpenAISettings.create()
    history = ChatHistorySettings.create()
    cuda = CudaComputeSettings.create()
    pipeline = DynamicPipelineCompilerSettings.create()
    local_llm = LocalLlmSettings.create()

    assert openai.provider_id == "providers.openai"
    assert openai.to_metadata()["endpoint"] == "https://api.openai.com/v1"
    assert history.provider_id == "chat-history"
    assert history.to_metadata()["source_uri"] == "rom://providers/chat-history/history.json"
    assert cuda.device_profile == "cuda13"
    assert cuda.to_metadata()["entry_point"] == "libtorch_bridge"
    assert pipeline.dsl_schema_version == "0.1"
    assert pipeline.to_metadata()["dsl_schema_uri"] == "rom://providers/dynamic-pipeline/schema.json"
    assert local_llm.runtime == "ollama"
    assert local_llm.to_metadata()["runtime_uri"] == "ollama://localhost"


def test_microsoft_ai_additional_public_wrappers_resolve_managed_surface():
    capabilities = MicrosoftAIProviderCapabilities.create()
    mapper = MicrosoftAIResponseMapper.create()
    validator = MicrosoftAIProviderOptionsValidator.create()
    exceptions = microsoft_ai_exception_types()

    assert "chat" in capabilities.supported_operations
    assert "text-generation" in capabilities.supported_operations
    assert capabilities.supports_operation("chat")
    assert capabilities.max_concurrent_connections == 1
    assert mapper.managed is not None
    assert validator.managed is not None
    assert "ProviderApiException" in exceptions
    assert exceptions["ProviderApiException"].Name == "ProviderApiException"


def test_managed_api_catalog_covers_provider_substrate_and_perception():
    names = set(managed_type_names())
    summary = managed_api_summary()

    assert "AIKernel.Providers.Substrate.ProviderRouter" in names
    assert "AIKernel.Providers.Perception.DefaultResidentPerceptionAlgorithmKernel" in names
    assert "AIKernel.Providers.Council.Providers.CouncilSemanticEvaluationProviderBase" in names
    assert summary["AIKernel.Providers.Substrate"] > 0
    assert summary["AIKernel.Providers.Perception"] > 0
