using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace AIKernel.Providers.Flux;

internal static unsafe partial class FluxNative
{
#if AK_FLUX_CUDA
    internal const string Library = "aikernel_flux_cuda";
#else
    internal const string Library = "aikernel_flux";
#endif
    internal const int Canceled = 3;

    [StructLayout(LayoutKind.Sequential)]
    internal struct Request
    {
        internal uint StructureSize;
        internal uint Width;
        internal uint Height;
        internal uint CpuThreads;
        internal long Seed;
        internal byte* DiffusionPath;
        internal byte* EncoderPath;
        internal byte* VaePath;
        internal byte* Prompt;
        internal byte* Reference;
        internal nuint ReferenceSize;
    }

#if AK_FLUX_CUDA
    [LibraryImport(Library, EntryPoint = "ak_flux_cuda_abi_version")]
#else
    [LibraryImport(Library, EntryPoint = "ak_flux_abi_version")]
#endif
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial uint Version();

    [LibraryImport(Library, EntryPoint = "ak_flux_job_create")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial FluxJob Create();

    [LibraryImport(Library, EntryPoint = "ak_flux_job_cancel")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial void Cancel(FluxJob job);

    [LibraryImport(Library, EntryPoint = "ak_flux_job_free")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial void Free(nint job);

#if AK_FLUX_CUDA
    [LibraryImport(Library, EntryPoint = "ak_flux_cuda_edit")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int Edit(FluxJob job, Request* request,
        delegate* unmanaged[Cdecl]<nint, byte*, ulong, void*, nuint, int> reader,
        delegate* unmanaged[Cdecl]<nint, byte*, ulong, void*, nuint, int> deviceReader, nint state, FluxCudaDevice device,
        byte* output, nuint outputSize, byte* error, nuint errorSize);
#else
    [LibraryImport(Library, EntryPoint = "ak_flux_edit")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int Edit(FluxJob job, Request* request,
        delegate* unmanaged[Cdecl]<nint, byte*, ulong, void*, nuint, int> reader, nint state,
        byte* output, nuint outputSize, byte* error, nuint errorSize);
#endif

    internal sealed class FluxJob() : SafeHandleZeroOrMinusOneIsInvalid(ownsHandle: true)
    {
        protected override bool ReleaseHandle()
        {
            Free(handle);
            return true;
        }
    }
}
