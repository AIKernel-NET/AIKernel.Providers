# CUDA Driver defaults for 0.1.4 preparation

The default CUDA descriptor now names Kernel's standalone native Driver ABI:

| Field | Default |
|---|---|
| PackageId | AIKernel.Kernel |
| ModuleId | aikernel_cuda_driver |
| EntryPoint | ak_create |
| AbiVersion | 1 |
| NativeModuleRef | aikernel-cuda://cuda13.0/modules/aikernel_cuda_driver |

The managed settings, capability fallback, managed/Python provider manifests,
and Python API catalog agree. `BackendName=cuda13.0` and `DeviceProfile=cuda13`
continue to identify the CUDA backend, not a LibTorch package version.
Python wrappers still load the managed settings and therefore see these defaults.
Kernel's NuGet package carries the native library for C#; Python's standalone
`aikernel-cuda-driver` wheel is a separate option, not a required C# dependency.

These descriptors do not bind or execute a GPU backend. Unbound execution
continues to fail closed; native diagnostics are not promoted to authoritative
readiness. `ak_create` uses Kernel's typed C ABI, not the old JSON dispatch ABI.
The existing operation catalog is retained for compatibility and is not proof
that the standalone library implements every catalog operation.

For a deliberately selected legacy backend, specify all of `PackageId`,
`ModuleId`, `EntryPoint`, `AbiVersion`, and `NativeModuleRef` explicitly.
The old descriptor test now supplies those legacy settings and retains every
assertion; new tests cover the new defaults and both manifests. The Python
default-setting assertion changes to the newly requested default and adds
checks for module, package, ABI, and reference.

No public interface, enum, provider operation, or NET metadata key is removed.
Versions remain at the current repository value until release integration.
This branch is reviewed with Core's `review/CUDA-NUGET` and Tools'
`review/CUDA-VERIFY`; dev integration and publication remain human actions.
