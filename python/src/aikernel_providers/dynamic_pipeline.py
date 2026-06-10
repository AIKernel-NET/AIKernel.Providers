from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path

from .chat_openai import CapabilityContract
from .managed import ManagedObject, call_static, create_managed, to_python_dict


@dataclass(frozen=True)
class DynamicPipelineCompilerCapability:
    """[EN]
    Wrapper for the official dynamic pipeline compiler provider capability contract.

    [JA]
    公式 Dynamic Pipeline Compiler Provider capability contract の wrapper です。
    """

    provider_id: str
    dsl_schema_version: str = "0.1"

    def to_contract(self) -> CapabilityContract:
        """[EN] Create the managed capability descriptor.

        [JA] managed capability descriptor を作成します。
        """
        return CapabilityContract(
            call_static(
                "AIKernel.Providers.DynamicPipelineCompiler.DynamicPipelineCompilerPythonBridge",
                "DynamicPipelineCompilerProvider",
                "ToContract",
                self.provider_id,
                self.dsl_schema_version,
            )
        )


class DynamicPipelineCompilerSettings(ManagedObject):
    """[EN]
    Wrapper for DynamicPipelineCompilerProvider public settings.

    [JA]
    DynamicPipelineCompilerProvider の public settings wrapper です。
    """

    @classmethod
    def create(cls) -> "DynamicPipelineCompilerSettings":
        """[EN] Create default managed settings.

        [JA] default managed settings を作成します。
        """
        return cls(
            create_managed(
                "AIKernel.Providers.DynamicPipelineCompiler.DynamicPipelineCompilerSettings",
                "DynamicPipelineCompilerProvider",
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

    @property
    def dsl_schema_version(self) -> str:
        """[EN] Return the DSL schema version.

        [JA] DSL schema version を返します。
        """
        return str(self.managed.DslSchemaVersion)

    @property
    def dsl_schema_uri(self) -> str | None:
        """[EN] Return the optional DSL schema URI.

        [JA] optional DSL schema URI を返します。
        """
        value = self.managed.DslSchemaUri
        return None if value is None else str(value)

    def to_metadata(self) -> dict[str, str]:
        """[EN] Return deterministic provider metadata.

        [JA] 決定論的 provider metadata を返します。
        """
        return to_python_dict(self.managed.ToMetadata())


class DynamicPipelineCompilerProvider(ManagedObject):
    """[EN]
    Wrapper for the public DynamicPipelineCompilerProvider object.

    [JA]
    公開 DynamicPipelineCompilerProvider object の wrapper です。
    """

    @classmethod
    def create(cls) -> "DynamicPipelineCompilerProvider":
        """[EN] Create a default managed provider.

        [JA] default managed provider を作成します。
        """
        return cls(
            call_static(
                "AIKernel.Providers.DynamicPipelineCompiler.DynamicPipelineCompilerPythonBridge",
                "DynamicPipelineCompilerProvider",
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


class DynamicPipelineCompilerInvoker(ManagedObject):
    """[EN]
    Wrapper for the dynamic pipeline compiler capability module invoker.

    [JA]
    Dynamic Pipeline Compiler capability module invoker の wrapper です。
    """

    @classmethod
    def create(cls) -> "DynamicPipelineCompilerInvoker":
        """[EN] Create a managed invoker.

        [JA] managed invoker を作成します。
        """
        return cls(
            call_static(
                "AIKernel.Providers.DynamicPipelineCompiler.DynamicPipelineCompilerPythonBridge",
                "DynamicPipelineCompilerProvider",
                "CreateInvoker",
            )
        )


def dynamic_pipeline_manifest_path() -> Path:
    """[EN] Return the bundled provider manifest path.

    [JA] 同梱 provider manifest path を返します。
    """
    return Path(__file__).resolve().parent / "native" / "dynamic-pipeline.provider.json"
