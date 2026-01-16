using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;

namespace EasyScreenRecord.Helpers
{
    /// <summary>
    /// Global mouse hook for detecting Ctrl+scroll and middle-click
    /// </summary>
    public class MouseHookHelper : IDisposable
    {
        public event Action<float, Point>? OnCtrlScroll;
        public event Action? OnMiddleClick;
        
        private IntPtr _hookId = IntPtr.Zero;
        private LowLevelMouseProc? _proc;
        
        private const int WH_MOUSE_LL = 14;
        private const int WM_MOUSEWHEEL = 0x020A;
        private const int WM_MBUTTONDOWN = 0x0207;
        
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);
        
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);
        
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
        
        [DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);
        
        private const int VK_CONTROL = 0x11;
        
        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
        
        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }
        
        public void Start()
        {
            if (_hookId != IntPtr.Zero) return;
            
            _proc = HookCallback;
            using (var curProcess = Process.GetCurrentProcess())
            using (var curModule = curProcess.MainModule!)
            {
                _hookId = SetWindowsHookEx(WH_MOUSE_LL, _proc, GetModuleHandle(curModule.ModuleName!), 0);
            }
        }
        
        public void Stop()
        {
            if (_hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }
        }
        
        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                var mousePoint = new Point(hookStruct.pt.x, hookStruct.pt.y);
                
                if ((int)wParam == WM_MOUSEWHEEL)
                {
                    // Check if Ctrl is pressed
                    bool ctrlPressed = (GetKeyState(VK_CONTROL) & 0x8000) != 0;
                    
                    if (ctrlPressed)
                    {
                        // Get scroll delta from high word of mouseData
                        short delta = (short)((hookStruct.mouseData >> 16) & 0xFFFF);
                        Debug.WriteLine($"MouseHook: Ctrl+Scroll detected, delta={delta}");
                        OnCtrlScroll?.Invoke(delta, mousePoint);
                    }
                }
                else if ((int)wParam == WM_MBUTTONDOWN)
                {
                    Debug.WriteLine("MouseHook: Middle-click detected");
                    OnMiddleClick?.Invoke();
                }
            }
            
            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }
        
        public void Dispose()
        {
            Stop();
        }
    }
}
