from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path

from .managed import ManagedObject, call_static, create_managed, to_python_dict


@dataclass(frozen=True)
class CapabilityContract:
    """[EN]
    Python view of AIKernel.Dtos.Capabilities.CapabilityModuleDescriptor.

    [JA]
    AIKernel.Dtos.Capabilities.CapabilityModuleDescriptor の Python view です。
    """

    _managed: object

    @property
    def managed(self):
        """[EN] Return the underlying C# descriptor.

        [JA] 背後の C# descriptor を返します。
        """
        return self._managed

    def to_managed(self):
        """[EN] Return the underlying C# descriptor.

        [JA] 背後の C# descriptor を返します。
        """
        return self._managed

    @property
    def capability_id(self) -> str:
        """[EN] Return the capability identifier.

        [JA] capability identifier を返します。
        """
        return str(self.managed.CapabilityId)

    @property
    def name(self) -> str:
        """[EN] Return the capability display name.

        [JA] capability display name を返します。
        """
        return str(self.managed.Name)

    @property
    def kind(self) -> str:
        """[EN] Return the capability kind.

        [JA] capability kind を返します。
        """
        return str(self.managed.Kind)

    @property
    def invocation_mode(self) -> str:
        """[EN] Return the invocation mode.

        [JA] invocation mode を返します。
        """
        return str(self.managed.InvocationMode)

    @property
    def version(self) -> str:
        """[EN] Return the contract version.

        [JA] contract version を返します。
        """
        return str(self.managed.Version)

    @property
    def provided_operations(self) -> tuple[str, ...]:
        """[EN] Return provided operation names.

        [JA] provided operation name を返します。
        """
        return tuple(str(value) for value in self.managed.ProvidedOperations)

    @property
    def required_permissions(self) -> tuple[str, ...]:
        """[EN] Return required permission names.

        [JA] required permission name を返します。
        """
        return tuple(str(value) for value in self.managed.RequiredPermissions)

    @property
    def metadata(self) -> dict[str, str]:
        """[EN] Return contract metadata as a Python dictionary.

        [JA] contract metadata を Python dictionary として返します。
        """
        return {str(key): str(self.managed.Metadata[key]) for key in self.managed.Metadata.Keys}


@dataclass(frozen=True)
class ChatOpenAICapability:
    """[EN]
    Wrapper for the official ChatOpenAI provider capability contract.

    [JA]
    公式 ChatOpenAI Provider capability contract の wrapper です。
    """

    provider_id: str
    model: str = "gpt-4o"
    endpoint: str = "https://api.openai.com/v1"

    def to_contract(self) -> CapabilityContract:
        """[EN] Create the managed capability descriptor.

        [JA] managed capability descriptor を作成します。
        """
        return CapabilityContract(
            call_static(
                "AIKernel.Providers.ChatOpenAI.ChatOpenAIPythonBridge",
                "ChatOpenAIProvider",
                "ToContract",
                self.provider_id,
                self.model,
                self.endpoint,
            )
        )


class ChatOpenAISettings(ManagedObject):
    """[EN]
    Wrapper for ChatOpenAIProvider public settings.

    [JA]
    ChatOpenAIProvider の public settings wrapper です。
    """

    @classmethod
    def create(cls) -> "ChatOpenAISettings":
        """[EN] Create default managed settings.

        [JA] default managed settings を作成します。
        """
        return cls(create_managed("AIKernel.Providers.ChatOpenAI.ChatOpenAISettings", "ChatOpenAIProvider"))

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
    def endpoint(self) -> str:
        """[EN] Return the configured endpoint.

        [JA] configured endpoint を返します。
        """
        return str(self.managed.Endpoint)

    @property
    def model(self) -> str:
        """[EN] Return the configured chat model name.

        [JA] configured chat model name を返します。
        """
        return str(self.managed.Model)

    @property
    def embedding_model(self) -> str | None:
        """[EN] Return the optional embedding model name.

        [JA] optional embedding model name を返します。
        """
        value = self.managed.EmbeddingModel
        return None if value is None else str(value)

    @property
    def api_key(self) -> str | None:
        """[EN] Return the optional inline API key.

        [JA] optional inline API key を返します。
        """
        value = self.managed.ApiKey
        return None if value is None else str(value)

    def to_metadata(self) -> dict[str, str]:
        """[EN] Return deterministic provider metadata.

        [JA] 決定論的 provider metadata を返します。
        """
        return to_python_dict(self.managed.ToMetadata())


class ChatOpenAIProvider(ManagedObject):
    """[EN]
    Wrapper for the public ChatOpenAIProvider object.

    [JA]
    公開 ChatOpenAIProvider object の wrapper です。
    """

    @classmethod
    def create(cls) -> "ChatOpenAIProvider":
        """[EN] Create a default managed provider.

        [JA] default managed provider を作成します。
        """
        return cls(
            call_static(
                "AIKernel.Providers.ChatOpenAI.ChatOpenAIPythonBridge",
                "ChatOpenAIProvider",
                "CreateProvider",
            )
        )

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


class ChatOpenAIInvoker(ManagedObject):
    """[EN]
    Wrapper for the ChatOpenAI capability module invoker.

    [JA]
    ChatOpenAI capability module invoker の wrapper です。
    """

    @classmethod
    def create(cls) -> "ChatOpenAIInvoker":
        """[EN] Create a managed invoker.

        [JA] managed invoker を作成します。
        """
        return cls(
            call_static(
                "AIKernel.Providers.ChatOpenAI.ChatOpenAIPythonBridge",
                "ChatOpenAIProvider",
                "CreateInvoker",
            )
        )


class ChatOpenAIClient(ManagedObject):
    """[EN]
    Wrapper handle for the public ChatOpenAIClient managed object.

    [JA]
    public ChatOpenAIClient managed object の wrapper handle です。
    """


def openai_manifest_path() -> Path:
    """[EN] Return the bundled provider manifest path.

    [JA] 同梱 provider manifest path を返します。
    """
    return Path(__file__).resolve().parent / "native" / "openai.provider.json"
