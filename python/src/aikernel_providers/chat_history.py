from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path

from .chat_openai import CapabilityContract
from .managed import ManagedObject, call_static, create_managed, to_python_dict


@dataclass(frozen=True)
class ChatHistoryCapability:
    """[EN]
    Wrapper for the official chat history provider capability contract.

    [JA]
    公式 ChatHistory Provider capability contract の wrapper です。
    """

    provider_id: str

    def to_contract(self) -> CapabilityContract:
        """[EN] Create the managed capability descriptor.

        [JA] managed capability descriptor を作成します。
        """
        return CapabilityContract(
            call_static(
                "AIKernel.Providers.ChatHistory.ChatHistoryPythonBridge",
                "ChatHistoryProvider",
                "ToContract",
                self.provider_id,
            )
        )


class ChatHistorySettings(ManagedObject):
    """[EN]
    Wrapper for ChatHistoryProvider public settings.

    [JA]
    ChatHistoryProvider の public settings wrapper です。
    """

    @classmethod
    def create(cls) -> "ChatHistorySettings":
        """[EN] Create default managed settings.

        [JA] default managed settings を作成します。
        """
        return cls(create_managed("AIKernel.Providers.ChatHistory.ChatHistorySettings", "ChatHistoryProvider"))

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
    def source_uri(self) -> str | None:
        """[EN] Return the configured source URI.

        [JA] configured source URI を返します。
        """
        value = self.managed.SourceUri
        return None if value is None else str(value)

    @property
    def artifact_hash(self) -> str | None:
        """[EN] Return the optional artifact hash.

        [JA] optional artifact hash を返します。
        """
        value = self.managed.ArtifactHash
        return None if value is None else str(value)

    def to_metadata(self) -> dict[str, str]:
        """[EN] Return deterministic provider metadata.

        [JA] 決定論的 provider metadata を返します。
        """
        return to_python_dict(self.managed.ToMetadata())


class ChatHistoryRecord(ManagedObject):
    """[EN]
    Wrapper for deterministic chat history records.

    [JA]
    deterministic chat history record の wrapper です。
    """

    @property
    def role(self) -> str:
        """[EN] Return the chat role.

        [JA] chat role を返します。
        """
        return str(self.managed.Role)

    @property
    def content(self) -> str:
        """[EN] Return the chat content.

        [JA] chat content を返します。
        """
        return str(self.managed.Content)

    @property
    def timestamp(self) -> str:
        """[EN] Return the record timestamp.

        [JA] record timestamp を返します。
        """
        return str(self.managed.Timestamp)


@dataclass(frozen=True)
class ChatMessage:
    """[EN]
    Python view of the public immutable chat message DTO.

    [JA]
    public immutable chat message DTO の Python view です。
    """

    role: str
    content: str
    timestamp: str


class ChatHistory(ManagedObject):
    """[EN]
    Wrapper for the public ordered chat history model.

    [JA]
    public ordered chat history model の wrapper です。
    """

    @property
    def messages(self) -> tuple[ChatMessage, ...]:
        """[EN] Return ordered chat messages.

        [JA] ordered chat message を返します。
        """
        return tuple(ChatMessage(str(item.Role), str(item.Content), str(item.Timestamp)) for item in self.managed.Messages)


class ChatHistoryProvider(ManagedObject):
    """[EN]
    Wrapper for the public ChatHistoryProvider object.

    [JA]
    公開 ChatHistoryProvider object の wrapper です。
    """

    @classmethod
    def create(cls) -> "ChatHistoryProvider":
        """[EN] Create a default managed provider.

        [JA] default managed provider を作成します。
        """
        return cls(
            call_static(
                "AIKernel.Providers.ChatHistory.ChatHistoryPythonBridge",
                "ChatHistoryProvider",
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


class ChatHistoryInvoker(ManagedObject):
    """[EN]
    Wrapper for the chat history capability module invoker.

    [JA]
    ChatHistory capability module invoker の wrapper です。
    """

    @classmethod
    def create(cls) -> "ChatHistoryInvoker":
        """[EN] Create a managed invoker.

        [JA] managed invoker を作成します。
        """
        return cls(
            call_static(
                "AIKernel.Providers.ChatHistory.ChatHistoryPythonBridge",
                "ChatHistoryProvider",
                "CreateInvoker",
            )
        )


def chat_history_manifest_path() -> Path:
    """[EN] Return the bundled provider manifest path.

    [JA] 同梱 provider manifest path を返します。
    """
    return Path(__file__).resolve().parent / "native" / "chat-history.provider.json"
