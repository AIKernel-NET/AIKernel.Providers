"""[EN]
Managed assembly discovery and pythonnet loading for AIKernel.Providers.

[JA]
AIKernel.Providers の managed assembly 探索と pythonnet 読み込みを提供します。
"""

from __future__ import annotations

import os
from dataclasses import dataclass
from pathlib import Path


_PROVIDER_PACKAGE_VERSION = "0.1.3"
_CONTRACT_PACKAGE_VERSION = "0.1.3"
_CORE_PACKAGE_VERSION = "0.1.3"
_MICROSOFT_AI_ABSTRACTIONS_VERSION = "10.6.0"
_MICROSOFT_EXTENSIONS_VERSION = "10.0.8"
_ASSEMBLIES = (
    "AIKernel.Abstractions.dll",
    "AIKernel.Common.dll",
    "AIKernel.Core.dll",
    "AIKernel.Dtos.dll",
    "AIKernel.Enums.dll",
    "AIKernel.Providers.Standard.dll",
    "ChatOpenAIProvider.dll",
    "ChatHistoryProvider.dll",
    "CudaComputeProvider.dll",
    "DynamicPipelineCompilerProvider.dll",
    "LocalLlmProvider.dll",
    "AIKernel.Providers.MicrosoftAI.dll",
    "Microsoft.Extensions.AI.Abstractions.dll",
    "Microsoft.Extensions.Configuration.Abstractions.dll",
    "Microsoft.Extensions.DependencyInjection.Abstractions.dll",
    "Microsoft.Extensions.Hosting.Abstractions.dll",
    "Microsoft.Extensions.Logging.Abstractions.dll",
    "Microsoft.Extensions.Logging.dll",
    "Microsoft.Extensions.Options.dll",
    "Microsoft.Extensions.Options.ConfigurationExtensions.dll",
)
_ASSEMBLY_PACKAGES = {
    "AIKernel.Abstractions.dll": ("AIKernel.Abstractions", _CONTRACT_PACKAGE_VERSION),
    "AIKernel.Common.dll": ("AIKernel.Common", _CORE_PACKAGE_VERSION),
    "AIKernel.Core.dll": ("AIKernel.Core", _CORE_PACKAGE_VERSION),
    "AIKernel.Dtos.dll": ("AIKernel.Dtos", _CONTRACT_PACKAGE_VERSION),
    "AIKernel.Enums.dll": ("AIKernel.Enums", _CONTRACT_PACKAGE_VERSION),
    "AIKernel.Providers.Standard.dll": ("AIKernel.Providers.Standard", _PROVIDER_PACKAGE_VERSION),
    "ChatOpenAIProvider.dll": ("AIKernel.Providers.ChatOpenAI", _PROVIDER_PACKAGE_VERSION),
    "ChatHistoryProvider.dll": ("AIKernel.Providers.ChatHistory", _PROVIDER_PACKAGE_VERSION),
    "CudaComputeProvider.dll": ("AIKernel.Providers.CudaCompute", _PROVIDER_PACKAGE_VERSION),
    "DynamicPipelineCompilerProvider.dll": ("AIKernel.Providers.DynamicPipelineCompiler", _PROVIDER_PACKAGE_VERSION),
    "LocalLlmProvider.dll": ("AIKernel.Providers.LocalLlm", _PROVIDER_PACKAGE_VERSION),
    "AIKernel.Providers.MicrosoftAI.dll": ("AIKernel.Providers.MicrosoftAI", _PROVIDER_PACKAGE_VERSION),
    "Microsoft.Extensions.AI.Abstractions.dll": ("Microsoft.Extensions.AI.Abstractions", _MICROSOFT_AI_ABSTRACTIONS_VERSION),
    "Microsoft.Extensions.Configuration.Abstractions.dll": ("Microsoft.Extensions.Configuration.Abstractions", _MICROSOFT_EXTENSIONS_VERSION),
    "Microsoft.Extensions.DependencyInjection.Abstractions.dll": ("Microsoft.Extensions.DependencyInjection.Abstractions", _MICROSOFT_EXTENSIONS_VERSION),
    "Microsoft.Extensions.Hosting.Abstractions.dll": ("Microsoft.Extensions.Hosting.Abstractions", _MICROSOFT_EXTENSIONS_VERSION),
    "Microsoft.Extensions.Logging.Abstractions.dll": ("Microsoft.Extensions.Logging.Abstractions", _MICROSOFT_EXTENSIONS_VERSION),
    "Microsoft.Extensions.Logging.dll": ("Microsoft.Extensions.Logging", _MICROSOFT_EXTENSIONS_VERSION),
    "Microsoft.Extensions.Options.dll": ("Microsoft.Extensions.Options", _MICROSOFT_EXTENSIONS_VERSION),
    "Microsoft.Extensions.Options.ConfigurationExtensions.dll": (
        "Microsoft.Extensions.Options.ConfigurationExtensions",
        _MICROSOFT_EXTENSIONS_VERSION,
    ),
}


@dataclass(frozen=True)
class ProviderAssemblySet:
    """[EN]
    Resolved managed assemblies that define the provider boundary.

    [JA]
    Provider 境界を定義する解決済み managed assembly 群です。
    """

    root: Path
    assemblies: tuple[Path, ...]

    @property
    def is_complete(self) -> bool:
        """[EN] Return whether every expected assembly exists.

        [JA] 期待されるすべての assembly が存在するかを返します。
        """
        return all(path.exists() for path in self.assemblies)

    @property
    def missing(self) -> tuple[str, ...]:
        """[EN] Return missing assembly file names.

        [JA] 不足している assembly ファイル名を返します。
        """
        return tuple(path.name for path in self.assemblies if not path.exists())


def provider_assemblies() -> ProviderAssemblySet:
    """[EN] Resolve bundled or NuGet-provided Provider assemblies.

    [JA] 同梱または NuGet 提供の Provider assembly を解決します。
    """
    root = _native_root()
    return ProviderAssemblySet(
        root=root,
        assemblies=tuple(_resolve_assembly(name) for name in _ASSEMBLIES),
    )


def require_provider_assemblies() -> ProviderAssemblySet:
    """[EN] Resolve assemblies and fail if any required assembly is missing.

    [JA] assembly を解決し、不足がある場合は失敗します。
    """
    assemblies = provider_assemblies()
    if not assemblies.is_complete:
        missing = ", ".join(assemblies.missing)
        raise FileNotFoundError(
            "AIKernel.Providers managed assemblies are not available: "
            f"{missing}. Bundle them in aikernel_providers/native or restore "
            "the corresponding NuGet packages."
        )
    return assemblies


def load_provider_runtime() -> ProviderAssemblySet:
    """[EN] Load bundled C# assemblies through pythonnet.

    [JA] 同梱 C# assembly を pythonnet 経由で読み込みます。
    """
    assemblies = require_provider_assemblies()
    try:
        from pythonnet import load  # type: ignore[import-not-found]

        try:
            load("coreclr")
        except RuntimeError:
            pass
        import clr  # type: ignore[import-not-found]
    except ImportError as exc:
        raise RuntimeError("pythonnet is required to load AIKernel.Providers assemblies.") from exc

    for assembly in assemblies.assemblies:
        clr.AddReference(str(assembly))
    return assemblies


def _resolve_assembly(name: str) -> Path:
    for root in _assembly_roots():
        candidate = root / name
        if candidate.exists():
            return candidate

    nuget_candidate = _resolve_nuget_assembly(name)
    if nuget_candidate is not None:
        return nuget_candidate

    return _native_root() / name


def _assembly_roots() -> tuple[Path, ...]:
    roots = [_native_root()]
    roots.extend(_source_build_roots())
    override = os.environ.get("AIKERNEL_PROVIDERS_ASSEMBLY_PATH")
    if override:
        roots.extend(Path(path) for path in override.split(os.pathsep) if path)
    return tuple(roots)


def _native_root() -> Path:
    return Path(__file__).resolve().parent


def _source_build_roots() -> tuple[Path, ...]:
    repository_root = Path(__file__).resolve().parents[4]
    return tuple(
        path
        for path in (
            repository_root / "src" / "Standard" / "AIKernel.Providers.Standard" / "bin" / "Release" / "net10.0",
            repository_root / "src" / "Chat" / "ChatHistoryProvider" / "bin" / "Release" / "net10.0",
            repository_root / "src" / "Compute" / "CudaComputeProvider" / "bin" / "Release" / "net10.0",
            repository_root / "src" / "Chat" / "ChatOpenAIProvider" / "bin" / "Release" / "net10.0",
            repository_root / "src" / "Llm" / "LocalLlmProvider" / "bin" / "Release" / "net10.0",
            repository_root / "src" / "Llm" / "MicrosoftAIProvider" / "bin" / "Release" / "net10.0",
            repository_root / "src" / "Pipeline" / "DynamicPipelineCompilerProvider" / "bin" / "Release" / "net10.0",
        )
        if path.exists()
    )


def _resolve_nuget_assembly(name: str) -> Path | None:
    package, version = _ASSEMBLY_PACKAGES[name]
    if package in {
        "AIKernel.Providers.ChatOpenAI",
        "AIKernel.Providers.ChatHistory",
        "AIKernel.Providers.CudaCompute",
        "AIKernel.Providers.DynamicPipelineCompiler",
        "AIKernel.Providers.LocalLlm",
    }:
        return None

    package_root = _nuget_root() / package.lower() / version / "lib"
    for framework in ("net10.0", "net9.0", "net8.0", "netstandard2.1", "netstandard2.0"):
        candidate = package_root / framework / name
        if candidate.exists():
            return candidate
    for candidate in package_root.rglob(name):
        if candidate.exists():
            return candidate
    return None


def _nuget_root() -> Path:
    configured = os.environ.get("NUGET_PACKAGES")
    if configured:
        return Path(configured)
    return Path.home() / ".nuget" / "packages"
