using System;

namespace EasyScreenRecord.Models
{
    public class AppSettings
    {
        public double ZoomFactor { get; set; } = 2.0;
        public double ZoomSpeed { get; set; } = 3.0;
        public bool ShowCursor { get; set; } = true;
        public int Bitrate { get; set; } = 10_000_000; // 10 Mbps
    }
}
