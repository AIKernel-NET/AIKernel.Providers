using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using AIKernel.Core.Memory;
using Microsoft.Win32.SafeHandles;

namespace AIKernel.Providers.Flux;

internal sealed unsafe partial class FluxCudaDevice : SafeHandleZeroOrMinusOneIsInvalid, IVirtualMemoryDevice
{
    private FluxCudaDevice(nint device) : base(ownsHandle: true) => SetHandle(device);

    internal static FluxCudaDevice Open(int ordinal)
    {
        Check(OpenDevice(ordinal, out var device), "opening the device");
        return new FluxCudaDevice(device);
    }

    public IVirtualMemoryDeviceBlock Upload(ReadOnlySpan<byte> source)
    {
        var page = new Page(this);
        try
        {
            fixed (byte* bytes = source)
            {
                Check(UploadPage(this, bytes, (nuint)source.Length, out var address), "uploading a page");
                page.Initialize(address);
            }
            return page;
        }
        catch
        {
            page.Dispose();
            throw;
        }
    }

    internal void CopyTo(IVirtualMemoryDeviceBlock source, int offset, nint target, int bytes)
    {
        if (source is not Page page || page.Owner != this)
            throw new ArgumentException("The page belongs to another device.", nameof(source));
        Check(CopyPage(page, (nuint)offset, target, (nuint)bytes), "copying between device allocations");
    }

    private static void Check(int status, string operation)
    {
        if (status == 2) throw new OutOfMemoryException($"CUDA ran out of memory while {operation}.");
        if (status != 0) throw new InvalidOperationException($"CUDA error {status} while {operation}.");
    }

    protected override bool ReleaseHandle() => CloseDevice(handle) == 0;

    private sealed class Page : SafeHandleZeroOrMinusOneIsInvalid, IVirtualMemoryDeviceBlock
    {
        internal FluxCudaDevice Owner { get; }

        internal Page(FluxCudaDevice owner) : base(ownsHandle: true) => Owner = owner;

        internal void Initialize(nint address)
        {
            bool retained = false;
            Owner.DangerousAddRef(ref retained);
            SetHandle(address);
        }

        public void Download(Span<byte> destination)
        {
            fixed (byte* bytes = destination)
                Check(DownloadPage(this, bytes, (nuint)destination.Length), "downloading a page");
        }

        void IDisposable.Dispose()
        {
            // The arena serializes operations on a page, including eviction.
            if (!IsClosed && !IsInvalid)
            {
                Check(FreePage(handle), "freeing a page");
                SetHandleAsInvalid();
                Owner.DangerousRelease();
            }
            Dispose();
        }

        protected override bool ReleaseHandle()
        {
            try { return FreePage(handle) == 0; }
            finally { Owner.DangerousRelease(); }
        }
    }

    [LibraryImport(FluxNative.Library, EntryPoint = "ak_flux_cuda_device_open")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int OpenDevice(int ordinal, out nint device);

    [LibraryImport(FluxNative.Library, EntryPoint = "ak_flux_cuda_device_close")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int CloseDevice(nint device);

    [LibraryImport(FluxNative.Library, EntryPoint = "ak_flux_cuda_page_upload")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int UploadPage(FluxCudaDevice device, byte* source, nuint size, out nint page);

    [LibraryImport(FluxNative.Library, EntryPoint = "ak_flux_cuda_page_download")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int DownloadPage(Page page, byte* destination, nuint size);

    [LibraryImport(FluxNative.Library, EntryPoint = "ak_flux_cuda_page_free")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int FreePage(nint page);

    [LibraryImport(FluxNative.Library, EntryPoint = "ak_flux_cuda_page_copy")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int CopyPage(Page page, nuint offset, nint destination, nuint size);
}
