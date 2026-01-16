using System;
using System.Runtime.InteropServices;
using Windows.Graphics.DirectX.Direct3D11;

namespace EasyScreenRecord.Helpers
{
    public static class Direct3D11Helper
    {
        [DllImport("d3d11.dll", EntryPoint = "D3D11CreateDevice", SetLastError = true)]
        private static extern int D3D11CreateDevice(
            IntPtr adapter,
            int driverType,
            IntPtr software,
            uint flags,
            uint[] featureLevels,
            uint featureLevelsCount,
            uint sdkVersion,
            out IntPtr device,
            out uint featureLevel,
            out IntPtr deviceContext);

        [DllImport("d3d11.dll", EntryPoint = "CreateDirect3D11DeviceFromDXGIDevice", SetLastError = true)]
        private static extern int CreateDirect3D11DeviceFromDXGIDevice(
            IntPtr dxgiDevice,
            out IntPtr graphicsDevice);

        [DllImport("d3d11.dll", EntryPoint = "CreateDirect3D11SurfaceFromDXGISurface", SetLastError = true)]
        private static extern int CreateDirect3D11SurfaceFromDXGISurface(
            IntPtr dxgiSurface,
            out IntPtr graphicsSurface);
            
        // We might need to use the WinRT interop interface instead if CreateDirect3D11DeviceFromDXGIDevice is not exported directly in d3d11.dll (it's actually in d3d11.dll on modern Windows but often accessed via IInspectable)
        // Actually, CreateDirect3D11DeviceFromDXGIDevice is a flat C API function exported by d3d11.dll since Windows 8.

        public static IDirect3DDevice CreateDevice()
        {
            return CreateDevice(false);
        }

        public static IDirect3DDevice CreateDevice(bool useWarp)
        {
            var driverType = useWarp ? 2 : 1; // D3D_DRIVER_TYPE_WARP = 2, D3D_DRIVER_TYPE_HARDWARE = 1
            uint flags = 0x20; // D3D11_CREATE_DEVICE_BGRA_SUPPORT

            // D3D11_CREATE_DEVICE_DEBUG = 0x2
            #if DEBUG
            // flags |= 0x2; 
            #endif

            var featureLevels = new uint[]
            {
                0xb100, // D3D_FEATURE_LEVEL_11_1
                0xb000, // D3D_FEATURE_LEVEL_11_0
                0xa100, // D3D_FEATURE_LEVEL_10_1
                0xa000, // D3D_FEATURE_LEVEL_10_0
                0x9300, // D3D_FEATURE_LEVEL_9_3
                0x9200, // D3D_FEATURE_LEVEL_9_2
                0x9100  // D3D_FEATURE_LEVEL_9_1
            };

            IntPtr pDevice;
            IntPtr pContext;
            uint featureLevel;

            int hr = D3D11CreateDevice(
                IntPtr.Zero,
                driverType,
                IntPtr.Zero,
                flags,
                featureLevels,
                (uint)featureLevels.Length,
                7, // D3D11_SDK_VERSION
                out pDevice,
                out featureLevel,
                out pContext);

            if (hr != 0)
            {
                throw new Exception($"D3D11CreateDevice failed with code {hr}");
            }

            // Convert to IDirect3DDevice
            // We need to query idxgiDevice from pDevice
            
            IntPtr pDxgiDevice = IntPtr.Zero;
            Guid IID_IDXGIDevice = new Guid("54ec77fa-1377-44e6-8c32-88fd5f44c84c");
            
            hr = Marshal.QueryInterface(pDevice, ref IID_IDXGIDevice, out pDxgiDevice);
            if (hr != 0)
            {
                 Marshal.Release(pDevice);
                 if (pContext != IntPtr.Zero) Marshal.Release(pContext);
                 throw new Exception("Failed to query IDXGIDevice");
            }

            IntPtr pInspectable = IntPtr.Zero;
            hr = CreateDirect3D11DeviceFromDXGIDevice(pDxgiDevice, out pInspectable);
            
            Marshal.Release(pDxgiDevice);
            Marshal.Release(pDevice);
            if (pContext != IntPtr.Zero) Marshal.Release(pContext);

            if (hr != 0)
            {
                throw new Exception($"CreateDirect3D11DeviceFromDXGIDevice failed with code {hr}");
            }
            
            // var device = Marshal.GetObjectForIUnknown(pInspectable) as IDirect3DDevice;
            var device = WinRT.MarshalInterface<IDirect3DDevice>.FromAbi(pInspectable); // Use CsWinRT marshaler
            Marshal.Release(pInspectable);
            
            return device;
        }

        public static IDirect3DSurface CreateSurfaceFromNative(IntPtr nativeSurface)
        {
            IntPtr pInspectable = IntPtr.Zero;
            int hr = CreateDirect3D11SurfaceFromDXGISurface(nativeSurface, out pInspectable);
            if (hr != 0) throw new Exception("Failed to create IDirect3DSurface from native pointer");
            
            var surface = WinRT.MarshalInterface<IDirect3DSurface>.FromAbi(pInspectable);
            Marshal.Release(pInspectable);
            return surface;
        }
    }

    [ComImport]
    [Guid("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IDirect3DDxgiInterfaceAccess
    {
        IntPtr GetInterface([In] ref Guid iid);
    }
}
