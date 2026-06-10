from __future__ import annotations

from aikernel_providers.native import load_provider_runtime


class ManagedObject:
    """[EN]
    Base wrapper for public C# Provider objects.

    [JA]
    公開 C# Provider object の基底 wrapper です。
    """

    def __init__(self, managed):
        self._managed = managed

    @property
    def managed(self):
        """[EN] Return the underlying C# object.

        [JA] 背後の C# object を返します。
        """
        return self._managed

    def to_managed(self):
        """[EN] Return the underlying C# object for contract calls.

        [JA] 契約呼び出し用に背後の C# object を返します。
        """
        return self._managed


def managed_type(type_name: str, assembly_name: str):
    """[EN] Resolve a C# type by assembly-qualified name.

    [JA] assembly-qualified name で C# type を解決します。
    """
    load_provider_runtime()
    from System import Type  # type: ignore[import-not-found]

    resolved = Type.GetType(f"{type_name}, {assembly_name}")
    if resolved is None:
        raise RuntimeError(f"Unable to resolve managed type: {type_name}, {assembly_name}")
    return resolved


def create_managed(type_name: str, assembly_name: str):
    """[EN] Create a managed object with its public default constructor.

    [JA] public default constructor で managed object を作成します。
    """
    load_provider_runtime()
    from System import Activator  # type: ignore[import-not-found]

    return Activator.CreateInstance(managed_type(type_name, assembly_name))


def to_python_dict(managed_dictionary) -> dict[str, str]:
    """[EN] Convert a managed string dictionary to a Python dictionary.

    [JA] managed string dictionary を Python dictionary へ変換します。
    """
    return {str(item.Key): str(item.Value) for item in managed_dictionary}


def call_static(type_name: str, assembly_name: str, method_name: str, *args):
    """[EN] Invoke a public static C# method through reflection.

    [JA] 公開 static C# method を reflection 経由で呼び出します。
    """
    method = managed_type(type_name, assembly_name).GetMethod(method_name)
    if method is None:
        raise RuntimeError(f"Unable to resolve managed method: {type_name}.{method_name}")
    return method.Invoke(None, _object_array(args))


def _object_array(values):
    load_provider_runtime()
    from System import Array, Object  # type: ignore[import-not-found]

    items = Array[Object](len(values))
    for index, value in enumerate(values):
        items[index] = value
    return items
