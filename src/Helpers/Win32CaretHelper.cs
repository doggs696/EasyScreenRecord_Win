using System;
using System.Runtime.InteropServices;
using System.Drawing;

namespace EasyScreenRecord.Helpers
{
    public static class Win32CaretHelper
    {
        [DllImport("user32.dll")]
        private static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO lpgui);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        
        [DllImport("user32.dll")]
        private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct GUITHREADINFO
        {
            public int cbSize;
            public int flags;
            public IntPtr hwndActive;
            public IntPtr hwndFocus;
            public IntPtr hwndCapture;
            public IntPtr hwndMenuOwner;
            public IntPtr hwndMoveSize;
            public IntPtr hwndCaret;
            public RECT rcCaret;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        public static Point? GetCaretPosition()
        {
            try 
            {
                IntPtr hwnd = GetForegroundWindow();
                if (hwnd == IntPtr.Zero) return null;

                uint threadId = GetWindowThreadProcessId(hwnd, out _);
                
                GUITHREADINFO guiInfo = new GUITHREADINFO();
                guiInfo.cbSize = Marshal.SizeOf(guiInfo);

                if (GetGUIThreadInfo(threadId, ref guiInfo))
                {
                    // Check if caret is provided (GUI_CARETBLINKING = 0x1 ?)
                    // If rcCaret has size
                    if (guiInfo.rcCaret.right - guiInfo.rcCaret.left > 0 || guiInfo.rcCaret.bottom - guiInfo.rcCaret.top > 0)
                    {
                        var pt = new POINT { x = guiInfo.rcCaret.left, y = guiInfo.rcCaret.top };
                        
                        if (guiInfo.hwndCaret != IntPtr.Zero)
                        {
                            ClientToScreen(guiInfo.hwndCaret, ref pt);
                            return new Point(pt.x, pt.y);
                        }
                        else if (guiInfo.hwndFocus != IntPtr.Zero)
                        {
                             // Fallback: sometimes caret is relative to focus window
                             ClientToScreen(guiInfo.hwndFocus, ref pt);
                             return new Point(pt.x, pt.y);
                        }
                    }
                }
            } 
            catch { }
            return null;
        }
    }
}
