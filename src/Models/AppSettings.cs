using System;

namespace EasyScreenRecord.Models
{
    public enum ZoomMode
    {
        Auto,   // Auto-zoom based on typing detection
        Manual  // Manual zoom with Ctrl+scroll
    }
    
    public class AppSettings
    {
        public ZoomMode ZoomMode { get; set; } = ZoomMode.Auto;
        public double ZoomFactor { get; set; } = 2.0;
        public double ZoomSpeed { get; set; } = 3.0;
        public bool ShowCursor { get; set; } = true;
        public int Bitrate { get; set; } = 10_000_000; // 10 Mbps
    }
}

