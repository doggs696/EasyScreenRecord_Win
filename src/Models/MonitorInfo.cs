namespace EasyScreenRecord.Models
{
    /// <summary>
    /// Represents information about a display monitor
    /// </summary>
    public class MonitorInfo
    {
        /// <summary>
        /// Display name (e.g., "ディスプレイ 1", "DELL U2720Q")
        /// </summary>
        public string Name { get; set; } = "";
        
        /// <summary>
        /// Device name from Windows (e.g., "\\\\.\\DISPLAY1")
        /// </summary>
        public string DeviceName { get; set; } = "";
        
        /// <summary>
        /// Monitor handle for capture API
        /// </summary>
        public nint Handle { get; set; }
        
        /// <summary>
        /// Width in pixels
        /// </summary>
        public int Width { get; set; }
        
        /// <summary>
        /// Height in pixels
        /// </summary>
        public int Height { get; set; }
        
        /// <summary>
        /// X position of the monitor
        /// </summary>
        public int X { get; set; }
        
        /// <summary>
        /// Y position of the monitor
        /// </summary>
        public int Y { get; set; }
        
        /// <summary>
        /// Whether this is the primary monitor
        /// </summary>
        public bool IsPrimary { get; set; }
        
        /// <summary>
        /// Display string for UI
        /// </summary>
        public string DisplayText => $"{Name} ({Width}x{Height}){(IsPrimary ? " [メイン]" : "")}";
    }
}
