using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Windows.Graphics.Capture;
using EasyScreenRecord.Models;

namespace EasyScreenRecord.Helpers
{
    public static class MonitorHelper
    {
        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);
        
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

        [DllImport("api-ms-win-core-winrt-l1-1-0.dll")]
        private static extern int RoGetActivationFactory(IntPtr activatableClassId, [In] ref Guid iid, out IntPtr factory);

        [DllImport("api-ms-win-core-winrt-string-l1-1-0.dll")]
        private static extern int WindowsCreateString(IntPtr sourceString, uint length, out IntPtr hstring);

        [DllImport("api-ms-win-core-winrt-string-l1-1-0.dll")]
        private static extern int WindowsDeleteString(IntPtr hstring);

        private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

        private const uint MONITOR_DEFAULTTOPRIMARY = 1;
        private const int CCHDEVICENAME = 32;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MONITORINFOEX
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCHDEVICENAME)]
            public string szDevice;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        /// <summary>
        /// Get all connected monitors
        /// </summary>
        public static List<MonitorInfo> GetAllMonitors()
        {
            _monitorList = new List<MonitorInfo>();
            _monitorIndex = 1;
            
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, MonitorEnumCallback, IntPtr.Zero);
            
            return _monitorList;
        }
        
        private static List<MonitorInfo> _monitorList = new List<MonitorInfo>();
        private static int _monitorIndex = 1;
        
        private static bool MonitorEnumCallback(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData)
        {
            var info = new MONITORINFOEX();
            info.cbSize = Marshal.SizeOf(typeof(MONITORINFOEX));

            if (GetMonitorInfo(hMonitor, ref info))
            {
                _monitorList.Add(new MonitorInfo
                {
                    Name = $"ディスプレイ {_monitorIndex}",
                    DeviceName = info.szDevice,
                    Handle = hMonitor,
                    X = info.rcMonitor.left,
                    Y = info.rcMonitor.top,
                    Width = info.rcMonitor.right - info.rcMonitor.left,
                    Height = info.rcMonitor.bottom - info.rcMonitor.top,
                    IsPrimary = (info.dwFlags & 1) != 0 // MONITORINFOF_PRIMARY = 1
                });
                _monitorIndex++;
            }
            return true; // Continue enumeration
        }

        /// <summary>
        /// Create a GraphicsCaptureItem for a specific monitor
        /// </summary>
        public static GraphicsCaptureItem CreateItemForMonitor(IntPtr hMonitor)
        {
            string className = "Windows.Graphics.Capture.GraphicsCaptureItem";
            IntPtr pClassName = Marshal.StringToHGlobalUni(className);
            
            try 
            {
                IntPtr hClassName;
                int hr = WindowsCreateString(pClassName, (uint)className.Length, out hClassName);
                if (hr != 0) throw new COMException("Failed to create HSTRING", hr);

                try 
                {
                    var iid = new Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356"); // IGraphicsCaptureItemInterop
                    IntPtr pFactory;
                    hr = RoGetActivationFactory(hClassName, ref iid, out pFactory);
                    if (hr != 0) throw new COMException("Failed to get factory", hr);

                    var interop = (IGraphicsCaptureItemInterop)Marshal.GetObjectForIUnknown(pFactory);
                    Marshal.Release(pFactory);

                    var itemIid = new Guid("79C3F95B-31F7-4EC2-A464-632EF5D30760"); // IGraphicsCaptureItem
                    IntPtr pItem;
                    interop.CreateForMonitor(hMonitor, ref itemIid, out pItem);
                    
                    var item = GraphicsCaptureItem.FromAbi(pItem);
                    Marshal.Release(pItem);

                    return item;
                }
                finally
                {
                    WindowsDeleteString(hClassName);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(pClassName);
            }
        }

        public static GraphicsCaptureItem CreateItemForPrimaryMonitor()
        {
            var hmon = MonitorFromWindow(IntPtr.Zero, MONITOR_DEFAULTTOPRIMARY);
            return CreateItemForMonitor(hmon);
        }

        [ComImport]
        [Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IGraphicsCaptureItemInterop
        {
             void CreateForWindow(IntPtr window, [In] ref Guid iid, out IntPtr result);
             void CreateForMonitor(IntPtr monitor, [In] ref Guid iid, out IntPtr result);
        }
    }
}

