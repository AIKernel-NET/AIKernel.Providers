from __future__ import annotations

from pathlib import Path

from .managed import ManagedObject, create_managed, managed_type, to_python_dict
from .native import load_provider_runtime, provider_assemblies


class MicrosoftAIProviderOptions(ManagedObject):
    """[EN]
    Wrapper for the public OpenAI-compatible MicrosoftAI provider options.

    [JA]
    OpenAI-compatible MicrosoftAI Provider の public options wrapper です。
    """

    @classmethod
    def create(cls) -> "MicrosoftAIProviderOptions":
        """[EN] Create default managed provider options.

        [JA] default managed provider options を作成します。
        """
        return cls(create_managed("AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions", "AIKernel.Providers.MicrosoftAI"))

    @property
    def provider_id(self) -> str:
        """[EN] Return the provider identifier.

        [JA] provider identifier を返します。
        """
        return str(self.managed.ProviderId)

    @property
    def name(self) -> str:
        """[EN] Return the provider display name.

        [JA] provider display name を返します。
        """
        return str(self.managed.Name)

    @property
    def version(self) -> str:
        """[EN] Return the provider runtime version.

        [JA] provider runtime version を返します。
        """
        return str(self.managed.Version)

    @property
    def model_id(self) -> str:
        """[EN] Return the configured model identifier.

        [JA] configured model identifier を返します。
        """
        return str(self.managed.ModelId)

    @property
    def endpoint(self) -> str | None:
        """[EN] Return the configured endpoint.

        [JA] configured endpoint を返します。
        """
        value = self.managed.Endpoint
        return None if value is None else str(value)

    @property
    def max_input_tokens(self) -> int:
        """[EN] Return the maximum input token count.

        [JA] maximum input token count を返します。
        """
        return int(self.managed.MaxInputTokens)

    @property
    def max_output_tokens(self) -> int | None:
        """[EN] Return the optional maximum output token count.

        [JA] 任意の maximum output token count を返します。
        """
        value = self.managed.MaxOutputTokens
        return None if value is None else int(value)

    @property
    def supports_system_role(self) -> bool:
        """[EN] Return whether system-role messages are supported.

        [JA] system-role message がサポートされるかを返します。
        """
        return bool(self.managed.SupportsSystemRole)

    @property
    def supports_assistant_role(self) -> bool:
        """[EN] Return whether assistant-role messages are supported.

        [JA] assistant-role message がサポートされるかを返します。
        """
        return bool(self.managed.SupportsAssistantRole)

    @property
    def supports_tool_role(self) -> bool:
        """[EN] Return whether tool-role messages are supported.

        [JA] tool-role message がサポートされるかを返します。
        """
        return bool(self.managed.SupportsToolRole)

    @property
    def supports_streaming(self) -> bool:
        """[EN] Return whether streaming responses are supported.

        [JA] streaming response がサポートされるかを返します。
        """
        return bool(self.managed.SupportsStreaming)

    @property
    def stop_sequences(self) -> tuple[str, ...]:
        """[EN] Return configured stop sequences.

        [JA] configured stop sequence を返します。
        """
        return tuple(str(item) for item in self.managed.StopSequences)

    @property
    def secret_key_name(self) -> str | None:
        """[EN] Return the configured secret key name.

        [JA] configured secret key name を返します。
        """
        value = self.managed.SecretKeyName
        return None if value is None else str(value)

    @property
    def api_key(self) -> str | None:
        """[EN] Return the inline API key, when present.

        [JA] inline API key がある場合に返します。
        """
        value = self.managed.ApiKey
        return None if value is None else str(value)


class MicrosoftAIProvider(ManagedObject):
    """[EN]
    Python handle for a managed OpenAI-compatible MicrosoftAI provider instance.

    [JA]
    managed OpenAI-compatible MicrosoftAI provider instance を保持する Python handle です。
    """

    @property
    def provider_id(self) -> str:
        """[EN] Return the provider identifier.

        [JA] provider identifier を返します。
        """
        return str(self.managed.ProviderId)

    @property
    def name(self) -> str:
        """[EN] Return the provider display name.

        [JA] provider display name を返します。
        """
        return str(self.managed.Name)

    @property
    def version(self) -> str:
        """[EN] Return the provider version.

        [JA] provider version を返します。
        """
        return str(self.managed.Version)


class MicrosoftAIProviderCapabilities(ManagedObject):
    """[EN]
    Wrapper for MicrosoftAI public provider capabilities.

    [JA]
    MicrosoftAI public provider capabilities の wrapper です。
    """

    @classmethod
    def create(cls) -> "MicrosoftAIProviderCapabilities":
        """[EN] Create default managed provider capabilities.

        [JA] default managed provider capabilities を作成します。
        """
        return cls(
            create_managed(
                "AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities",
                "AIKernel.Providers.MicrosoftAI",
            )
        )

    @property
    def supported_operations(self) -> tuple[str, ...]:
        """[EN] Return supported operation names.

        [JA] supported operation name を返します。
        """
        return tuple(str(item) for item in self.managed.SupportedOperations)

    @property
    def supported_data_types(self) -> tuple[str, ...]:
        """[EN] Return supported data type names.

        [JA] supported data type name を返します。
        """
        return tuple(str(item) for item in self.managed.SupportedDataTypes)

    @property
    def max_concurrent_connections(self) -> int:
        """[EN] Return the maximum concurrent connection count.

        [JA] maximum concurrent connection count を返します。
        """
        return int(self.managed.MaxConcurrentConnections)

    @property
    def supports_query_augmentation(self) -> bool:
        """[EN] Return whether query augmentation is supported.

        [JA] query augmentation がサポートされるかを返します。
        """
        return bool(self.managed.SupportsQueryAugmentation)

    @property
    def supports_query_decomposition(self) -> bool:
        """[EN] Return whether query decomposition is supported.

        [JA] query decomposition がサポートされるかを返します。
        """
        return bool(self.managed.SupportsQueryDecomposition)

    @property
    def supports_query_routing(self) -> bool:
        """[EN] Return whether query routing is supported.

        [JA] query routing がサポートされるかを返します。
        """
        return bool(self.managed.SupportsQueryRouting)

    @property
    def max_query_parts(self) -> int:
        """[EN] Return the maximum query part count.

        [JA] maximum query part count を返します。
        """
        return int(self.managed.MaxQueryParts)

    @property
    def supported_query_processing_operations(self) -> tuple[str, ...]:
        """[EN] Return supported query processing operations.

        [JA] supported query processing operation を返します。
        """
        return tuple(str(item) for item in self.managed.SupportedQueryProcessingOperations)

    @property
    def supports_embedding(self) -> bool:
        """[EN] Return whether embedding is supported.

        [JA] embedding がサポートされるかを返します。
        """
        return bool(self.managed.SupportsEmbedding)

    @property
    def embedding_dimensions(self) -> int | None:
        """[EN] Return configured embedding dimensions.

        [JA] configured embedding dimensions を返します。
        """
        value = self.managed.EmbeddingDimensions
        return None if value is None else int(value)

    @property
    def supported_embedding_models(self) -> tuple[str, ...]:
        """[EN] Return supported embedding model names.

        [JA] supported embedding model name を返します。
        """
        return tuple(str(item) for item in self.managed.SupportedEmbeddingModels)

    def supports_operation(self, operation: str) -> bool:
        """[EN] Return whether the operation is supported.

        [JA] operation がサポートされるかを返します。
        """
        return bool(self.managed.SupportsOperation(operation))

    def supports_data_type(self, data_type: str) -> bool:
        """[EN] Return whether the data type is supported.

        [JA] data type がサポートされるかを返します。
        """
        return bool(self.managed.SupportsDataType(data_type))

    def supports_quantization(self, quantization_level: str) -> bool:
        """[EN] Return whether the quantization level is supported.

        [JA] quantization level がサポートされるかを返します。
        """
        return bool(self.managed.SupportsQuantization(quantization_level))

    def supports_query_processing_operation(self, operation: str) -> bool:
        """[EN] Return whether the query processing operation is supported.

        [JA] query processing operation がサポートされるかを返します。
        """
        return bool(self.managed.SupportsQueryProcessingOperation(operation))


class MicrosoftAICredential(ManagedObject):
    """[EN]
    Wrapper for OpenAI-compatible MicrosoftAI credentials.

    [JA]
    OpenAI-compatible MicrosoftAI credential の wrapper です。
    """

    @classmethod
    def create(cls, key_name: str, api_key: str, expires_at_utc=None) -> "MicrosoftAICredential":
        """[EN] Create a managed OpenAI-compatible credential.

        [JA] managed OpenAI-compatible credential を作成します。
        """
        credential_type = managed_type("AIKernel.Providers.MicrosoftAI.OpenAICompatibleCredential", "AIKernel.Providers.MicrosoftAI")
        method = credential_type.GetMethod("Create")
        return cls(method.Invoke(None, _object_array((key_name, api_key, expires_at_utc, None))))

    @property
    def api_key(self) -> str:
        """[EN] Return the API key value.

        [JA] API key value を返します。
        """
        return str(self.managed.ApiKey)

    @property
    def expires_at_utc(self) -> str | None:
        """[EN] Return the optional expiration timestamp.

        [JA] 任意の expiration timestamp を返します。
        """
        value = self.managed.ExpiresAtUtc
        return None if value is None else str(value)


class MicrosoftAIHealthContext(ManagedObject):
    """[EN]
    Python view of the MicrosoftAI provider health context.

    [JA]
    MicrosoftAI provider health context の Python view です。
    """

    @property
    def provider_id(self) -> str:
        """[EN] Return the provider identifier.

        [JA] provider identifier を返します。
        """
        return str(self.managed.ProviderId)

    @property
    def name(self) -> str:
        """[EN] Return the provider display name.

        [JA] provider display name を返します。
        """
        return str(self.managed.Name)

    @property
    def version(self) -> str:
        """[EN] Return the provider version.

        [JA] provider version を返します。
        """
        return str(self.managed.Version)

    @property
    def model_id(self) -> str:
        """[EN] Return the model identifier.

        [JA] model identifier を返します。
        """
        return str(self.managed.ModelId)

    @property
    def is_initialized(self) -> bool:
        """[EN] Return whether the provider is initialized.

        [JA] provider が initialized かを返します。
        """
        return bool(self.managed.IsInitialized)

    @property
    def checked_at_utc(self) -> str:
        """[EN] Return the health check timestamp.

        [JA] health check timestamp を返します。
        """
        return str(self.managed.CheckedAtUtc)


class MicrosoftAIResponseProjection(ManagedObject):
    """[EN]
    Python view of the MicrosoftAI response projection DTO.

    [JA]
    MicrosoftAI response projection DTO の Python view です。
    """

    @property
    def model_id(self) -> str:
        """[EN] Return the response model identifier.

        [JA] response model identifier を返します。
        """
        return str(self.managed.ModelId)

    @property
    def raw_response(self) -> str:
        """[EN] Return the raw response payload.

        [JA] raw response payload を返します。
        """
        return str(self.managed.RawResponse)

    @property
    def primary_text(self) -> str:
        """[EN] Return the primary response text.

        [JA] primary response text を返します。
        """
        return str(self.managed.PrimaryText)

    @property
    def is_truncated(self) -> bool:
        """[EN] Return whether the projection is truncated.

        [JA] projection が truncated かを返します。
        """
        return bool(self.managed.IsTruncated)

    @property
    def observed_at_utc(self) -> str:
        """[EN] Return the observation timestamp.

        [JA] observation timestamp を返します。
        """
        return str(self.managed.ObservedAtUtc)

    @property
    def metadata(self) -> dict[str, str]:
        """[EN] Return response metadata as a Python dictionary.

        [JA] response metadata を Python dictionary として返します。
        """
        return to_python_dict(self.managed.Metadata)


class MicrosoftAIResponseMapper(ManagedObject):
    """[EN]
    Wrapper for the MicrosoftAI response mapper.

    [JA]
    MicrosoftAI response mapper の wrapper です。
    """

    @classmethod
    def create(cls) -> "MicrosoftAIResponseMapper":
        """[EN] Create a managed response mapper.

        [JA] managed response mapper を作成します。
        """
        return cls(create_managed("AIKernel.Providers.MicrosoftAI.OpenAICompatibleResponseMapper", "AIKernel.Providers.MicrosoftAI"))

    def get_primary_text(self, response) -> str:
        """[EN] Extract primary text from a managed chat response.

        [JA] managed chat response から primary text を抽出します。
        """
        return str(self.managed.GetPrimaryText(response))

    def create_projection(self, response, fallback_model_id: str, observed_at_utc) -> MicrosoftAIResponseProjection:
        """[EN] Create a response projection from a managed chat response.

        [JA] managed chat response から response projection を作成します。
        """
        return MicrosoftAIResponseProjection(self.managed.CreateProjection(response, fallback_model_id, observed_at_utc))


class MicrosoftAIProviderOptionsValidator(ManagedObject):
    """[EN]
    Wrapper for MicrosoftAI provider options validation.

    [JA]
    MicrosoftAI provider options validation の wrapper です。
    """

    @classmethod
    def create(cls) -> "MicrosoftAIProviderOptionsValidator":
        """[EN] Create a managed options validator.

        [JA] managed options validator を作成します。
        """
        return cls(
            create_managed(
                "AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptionsValidator",
                "AIKernel.Providers.MicrosoftAI",
            )
        )

    def validate(self, options: MicrosoftAIProviderOptions, name: str | None = None):
        """[EN] Validate managed provider options.

        [JA] managed provider options を検証します。
        """
        return self.managed.Validate(name, options.to_managed())


def microsoft_ai_exception_types() -> dict[str, object]:
    """[EN] Return public MicrosoftAI provider exception managed types.

    [JA] public MicrosoftAI provider exception managed type を返します。
    """
    names = [
        "ProviderExecutionException",
        "ProviderInvalidResponseException",
        "ProviderCapabilityMismatchException",
        "ProviderExecutionTimeoutException",
        "ProviderRateLimitException",
        "ProviderApiException",
    ]
    return {
        name: managed_type(f"AIKernel.Providers.MicrosoftAI.{name}", "AIKernel.Providers.MicrosoftAI")
        for name in names
    }


def _object_array(values):
    load_provider_runtime()
    from System import Array, Object  # type: ignore[import-not-found]

    items = Array[Object](len(values))
    for index, value in enumerate(values):
        items[index] = value
    return items


def microsoft_ai_assembly_path() -> Path:
    """[EN] Return the resolved MicrosoftAI provider assembly path.

    [JA] 解決済み MicrosoftAI provider assembly path を返します。
    """
    for assembly in provider_assemblies().assemblies:
        if assembly.name == "AIKernel.Providers.MicrosoftAI.dll":
            return assembly
    return Path(__file__).resolve().parent / "native" / "AIKernel.Providers.MicrosoftAI.dll"
