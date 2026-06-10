from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path

from .chat_openai import CapabilityContract
from .managed import ManagedObject, call_static, create_managed, to_python_dict


@dataclass(frozen=True)
class CudaComputeCapability:
    """[EN]
    Wrapper for the official CUDA compute provider capability contract.

    [JA]
    公式 CUDA compute Provider capability contract の wrapper です。
    """

    provider_id: str
    device_profile: str = "cuda13"

    def to_contract(self) -> CapabilityContract:
        """[EN] Create the managed capability descriptor.

        [JA] managed capability descriptor を作成します。
        """
        return CapabilityContract(
            call_static(
                "AIKernel.Providers.CudaCompute.CudaComputePythonBridge",
                "CudaComputeProvider",
                "ToContract",
                self.provider_id,
                self.device_profile,
            )
        )


class CudaComputeSettings(ManagedObject):
    """[EN]
    Wrapper for CudaComputeProvider public settings.

    [JA]
    CudaComputeProvider の public settings wrapper です。
    """

    @classmethod
    def create(cls) -> "CudaComputeSettings":
        """[EN] Create default managed settings.

        [JA] default managed settings を作成します。
        """
        return cls(create_managed("AIKernel.Providers.CudaCompute.CudaComputeSettings", "CudaComputeProvider"))

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
    def device_profile(self) -> str:
        """[EN] Return the CUDA device profile.

        [JA] CUDA device profile を返します。
        """
        return str(self.managed.DeviceProfile)

    @property
    def entry_point(self) -> str:
        """[EN] Return the native entry point name.

        [JA] native entry point name を返します。
        """
        return str(self.managed.EntryPoint)

    @property
    def loader_json(self) -> str | None:
        """[EN] Return optional loader JSON.

        [JA] optional loader JSON を返します。
        """
        value = self.managed.LoaderJson
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


class CudaComputeProvider(ManagedObject):
    """[EN]
    Wrapper for the public CudaComputeProvider object.

    [JA]
    公開 CudaComputeProvider object の wrapper です。
    """

    @classmethod
    def create(cls) -> "CudaComputeProvider":
        """[EN] Create a default managed provider.

        [JA] default managed provider を作成します。
        """
        return cls(
            call_static(
                "AIKernel.Providers.CudaCompute.CudaComputePythonBridge",
                "CudaComputeProvider",
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


class CudaComputeInvoker(ManagedObject):
    """[EN]
    Wrapper for the CUDA compute capability module invoker.

    [JA]
    CUDA compute capability module invoker の wrapper です。
    """

    @classmethod
    def create(cls) -> "CudaComputeInvoker":
        """[EN] Create a managed invoker.

        [JA] managed invoker を作成します。
        """
        return cls(
            call_static(
                "AIKernel.Providers.CudaCompute.CudaComputePythonBridge",
                "CudaComputeProvider",
                "CreateInvoker",
            )
        )


def cuda_manifest_path() -> Path:
    """[EN] Return the bundled provider manifest path.

    [JA] 同梱 provider manifest path を返します。
    """
    return Path(__file__).resolve().parent / "native" / "cuda.provider.json"
