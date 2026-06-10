from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path

from .chat_openai import CapabilityContract
from .managed import ManagedObject, call_static, create_managed, to_python_dict


@dataclass(frozen=True)
class LocalLlmCapability:
    """[EN]
    Wrapper for the official local LLM provider capability contract.

    [JA]
    公式 Local LLM Provider capability contract の wrapper です。
    """

    provider_id: str
    runtime: str = "ollama"

    def to_contract(self) -> CapabilityContract:
        """[EN] Create the managed capability descriptor.

        [JA] managed capability descriptor を作成します。
        """
        return CapabilityContract(
            call_static(
                "AIKernel.Providers.LocalLlm.LocalLlmPythonBridge",
                "LocalLlmProvider",
                "ToContract",
                self.provider_id,
                self.runtime,
            )
        )


class LocalLlmSettings(ManagedObject):
    """[EN]
    Wrapper for LocalLlmProvider public settings.

    [JA]
    LocalLlmProvider の public settings wrapper です。
    """

    @classmethod
    def create(cls) -> "LocalLlmSettings":
        """[EN] Create default managed settings.

        [JA] default managed settings を作成します。
        """
        return cls(create_managed("AIKernel.Providers.LocalLlm.LocalLlmSettings", "LocalLlmProvider"))

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
    def runtime(self) -> str:
        """[EN] Return the local LLM runtime name.

        [JA] local LLM runtime name を返します。
        """
        return str(self.managed.Runtime)

    @property
    def runtime_uri(self) -> str | None:
        """[EN] Return the optional local runtime URI.

        [JA] optional local runtime URI を返します。
        """
        value = self.managed.RuntimeUri
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


class LocalLlmProvider(ManagedObject):
    """[EN]
    Wrapper for the public LocalLlmProvider object.

    [JA]
    公開 LocalLlmProvider object の wrapper です。
    """

    @classmethod
    def create(cls) -> "LocalLlmProvider":
        """[EN] Create a default managed provider.

        [JA] default managed provider を作成します。
        """
        return cls(
            call_static(
                "AIKernel.Providers.LocalLlm.LocalLlmPythonBridge",
                "LocalLlmProvider",
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


class LocalLlmInvoker(ManagedObject):
    """[EN]
    Wrapper for the local LLM capability module invoker.

    [JA]
    Local LLM capability module invoker の wrapper です。
    """

    @classmethod
    def create(cls) -> "LocalLlmInvoker":
        """[EN] Create a managed invoker.

        [JA] managed invoker を作成します。
        """
        return cls(
            call_static(
                "AIKernel.Providers.LocalLlm.LocalLlmPythonBridge",
                "LocalLlmProvider",
                "CreateInvoker",
            )
        )


def local_llm_manifest_path() -> Path:
    """[EN] Return the bundled provider manifest path.

    [JA] 同梱 provider manifest path を返します。
    """
    return Path(__file__).resolve().parent / "native" / "local-llm.provider.json"
