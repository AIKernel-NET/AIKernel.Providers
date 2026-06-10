from __future__ import annotations

from dataclasses import dataclass

from .managed import ManagedObject, create_managed, managed_type


_ASSEMBLY = "AIKernel.Providers.Standard"


@dataclass(frozen=True)
class StandardDriverContract:
    """[EN] Python descriptor for a public AIKernel.Providers.Standard driver.

    [JA] 公開 AIKernel.Providers.Standard driver の Python descriptor です。
    """

    provider_id: str
    name: str
    managed_type: str


def _create(type_name: str, *args):
    if not args:
        return create_managed(type_name, _ASSEMBLY)

    from System import Activator, Array, Object  # type: ignore[import-not-found]

    items = Array[Object](len(args))
    for index, value in enumerate(args):
        items[index] = value
    return Activator.CreateInstance(managed_type(type_name, _ASSEMBLY), items)


class CpuComputeProvider(ManagedObject):
    """[EN] Wrapper for the standard CPU compute provider.

    [JA] 標準 CPU compute Provider の wrapper です。
    """

    @classmethod
    def create(cls) -> "CpuComputeProvider":
        """[EN] Create a managed CPU compute provider.

        [JA] managed CPU compute Provider を作成します。
        """
        return cls(_create("AIKernel.Providers.Standard.Compute.CpuComputeProvider"))


class MemoryFileSystemProvider(ManagedObject):
    """[EN] Wrapper for the in-memory file system provider.

    [JA] in-memory file system Provider の wrapper です。
    """

    @classmethod
    def create(cls) -> "MemoryFileSystemProvider":
        """[EN] Create a managed memory file system provider.

        [JA] managed memory file system Provider を作成します。
        """
        return cls(_create("AIKernel.Providers.Standard.FileSystem.MemoryFileSystemProvider"))


class PhysicalFileSystemProvider(ManagedObject):
    """[EN] Wrapper for the physical file system provider.

    [JA] physical file system Provider の wrapper です。
    """

    @classmethod
    def create(cls) -> "PhysicalFileSystemProvider":
        """[EN] Create a managed physical file system provider.

        [JA] managed physical file system Provider を作成します。
        """
        return cls(_create("AIKernel.Providers.Standard.FileSystem.PhysicalFileSystemProvider"))


class ZipFileSystemProvider(ManagedObject):
    """[EN] Wrapper for the ZIP file system provider.

    [JA] ZIP file system Provider の wrapper です。
    """

    @classmethod
    def create(cls, zip_path: str) -> "ZipFileSystemProvider":
        """[EN] Create a managed ZIP file system provider.

        [JA] managed ZIP file system Provider を作成します。
        """
        return cls(_create("AIKernel.Providers.Standard.FileSystem.ZipFileSystemProvider", zip_path))


class ConsoleLoggingProvider(ManagedObject):
    """[EN] Wrapper for the console logging provider.

    [JA] console logging Provider の wrapper です。
    """

    @classmethod
    def create(cls) -> "ConsoleLoggingProvider":
        """[EN] Create a managed console logging provider.

        [JA] managed console logging Provider を作成します。
        """
        return cls(_create("AIKernel.Providers.Standard.Logging.ConsoleLoggingProvider"))


class FileLoggingProvider(ManagedObject):
    """[EN] Wrapper for the file logging provider.

    [JA] file logging Provider の wrapper です。
    """

    @classmethod
    def create(cls, path: str) -> "FileLoggingProvider":
        """[EN] Create a managed file logging provider.

        [JA] managed file logging Provider を作成します。
        """
        return cls(_create("AIKernel.Providers.Standard.Logging.FileLoggingProvider", path))


class NetworkProvider(ManagedObject):
    """[EN] Wrapper for the standard network provider.

    [JA] standard network Provider の wrapper です。
    """

    @classmethod
    def create(cls) -> "NetworkProvider":
        """[EN] Create a managed network provider.

        [JA] managed network Provider を作成します。
        """
        return cls(_create("AIKernel.Providers.Standard.Network.NetworkProvider"))


class HttpNetworkProvider(ManagedObject):
    """[EN] Wrapper for the HTTP network provider.

    [JA] HTTP network Provider の wrapper です。
    """

    @classmethod
    def create(cls) -> "HttpNetworkProvider":
        """[EN] Create a managed HTTP network provider.

        [JA] managed HTTP network Provider を作成します。
        """
        return cls(_create("AIKernel.Providers.Standard.Network.HttpNetworkProvider"))


class WebSocketNetworkProvider(ManagedObject):
    """[EN] Wrapper for the WebSocket network provider.

    [JA] WebSocket network Provider の wrapper です。
    """

    @classmethod
    def create(cls) -> "WebSocketNetworkProvider":
        """[EN] Create a managed WebSocket network provider.

        [JA] managed WebSocket network Provider を作成します。
        """
        return cls(_create("AIKernel.Providers.Standard.Network.WebSocketNetworkProvider"))


class ProcessSupervisorProvider(ManagedObject):
    """[EN] Wrapper for the logical process supervisor provider.

    [JA] logical process supervisor Provider の wrapper です。
    """

    @classmethod
    def create(cls) -> "ProcessSupervisorProvider":
        """[EN] Create a managed process supervisor provider.

        [JA] managed process supervisor Provider を作成します。
        """
        return cls(_create("AIKernel.Providers.Standard.Processes.ProcessSupervisorProvider"))


class DefaultProcessSupervisorProvider(ManagedObject):
    """[EN] Wrapper for the default Core process supervisor provider.

    [JA] default Core process supervisor Provider の wrapper です。
    """

    @classmethod
    def create(cls) -> "DefaultProcessSupervisorProvider":
        """[EN] Create a managed default process supervisor provider.

        [JA] managed default process supervisor Provider を作成します。
        """
        return cls(_create("AIKernel.Providers.Standard.Processes.DefaultProcessSupervisorProvider"))


class EventBusProvider(ManagedObject):
    """[EN] Wrapper for the standard EventBus provider.

    [JA] standard EventBus Provider の wrapper です。
    """

    @classmethod
    def create(cls) -> "EventBusProvider":
        """[EN] Create a managed EventBus provider.

        [JA] managed EventBus Provider を作成します。
        """
        return cls(_create("AIKernel.Providers.Standard.EventBus.EventBusProvider"))


class SchedulerProvider(ManagedObject):
    """[EN] Wrapper for the standard scheduler provider.

    [JA] standard scheduler Provider の wrapper です。
    """

    @classmethod
    def create(cls) -> "SchedulerProvider":
        """[EN] Create a managed scheduler provider.

        [JA] managed scheduler Provider を作成します。
        """
        return cls(_create("AIKernel.Providers.Standard.Scheduler.SchedulerProvider"))


class ProfilerProvider(ManagedObject):
    """[EN] Wrapper for the standard profiler provider.

    [JA] standard profiler Provider の wrapper です。
    """

    @classmethod
    def create(cls) -> "ProfilerProvider":
        """[EN] Create a managed profiler provider.

        [JA] managed profiler Provider を作成します。
        """
        return cls(_create("AIKernel.Providers.Standard.Profiler.ProfilerProvider"))


def standard_driver_contracts() -> tuple[StandardDriverContract, ...]:
    """[EN] Return the standard OS driver providers covered by Python.

    [JA] Python が網羅する standard OS driver Provider を返します。
    """
    return (
        StandardDriverContract("providers.compute.cpu", "CPU Compute Provider", "AIKernel.Providers.Standard.Compute.CpuComputeProvider"),
        StandardDriverContract("providers.fs.memory", "Memory File System Provider", "AIKernel.Providers.Standard.FileSystem.MemoryFileSystemProvider"),
        StandardDriverContract("providers.fs.physical", "Physical File System Provider", "AIKernel.Providers.Standard.FileSystem.PhysicalFileSystemProvider"),
        StandardDriverContract("providers.fs.zip", "ZIP File System Provider", "AIKernel.Providers.Standard.FileSystem.ZipFileSystemProvider"),
        StandardDriverContract("providers.logging.console", "Console Logging Provider", "AIKernel.Providers.Standard.Logging.ConsoleLoggingProvider"),
        StandardDriverContract("providers.logging.file", "File Logging Provider", "AIKernel.Providers.Standard.Logging.FileLoggingProvider"),
        StandardDriverContract("providers.network.standard", "Network Provider", "AIKernel.Providers.Standard.Network.NetworkProvider"),
        StandardDriverContract("providers.network.http", "HTTP Network Provider", "AIKernel.Providers.Standard.Network.HttpNetworkProvider"),
        StandardDriverContract("providers.network.websocket", "WebSocket Network Provider", "AIKernel.Providers.Standard.Network.WebSocketNetworkProvider"),
        StandardDriverContract("providers.process.supervisor", "Process Supervisor Provider", "AIKernel.Providers.Standard.Processes.ProcessSupervisorProvider"),
        StandardDriverContract("providers.process.supervisor.default", "Default Process Supervisor Provider", "AIKernel.Providers.Standard.Processes.DefaultProcessSupervisorProvider"),
        StandardDriverContract("providers.eventbus.memory", "Event Bus Provider", "AIKernel.Providers.Standard.EventBus.EventBusProvider"),
        StandardDriverContract("providers.scheduler.standard", "Scheduler Provider", "AIKernel.Providers.Standard.Scheduler.SchedulerProvider"),
        StandardDriverContract("providers.profiler.standard", "Profiler Provider", "AIKernel.Providers.Standard.Profiler.ProfilerProvider"),
    )
