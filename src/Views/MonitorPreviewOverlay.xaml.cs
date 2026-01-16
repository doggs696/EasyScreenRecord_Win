using System.Windows;
using EasyScreenRecord.Models;

namespace EasyScreenRecord.Views
{
    public partial class MonitorPreviewOverlay : Window
    {
        public MonitorPreviewOverlay()
        {
            InitializeComponent();
        }

        public void ShowOnMonitor(MonitorInfo monitor)
        {
            // Position the window on the specified monitor
            this.Left = monitor.X;
            this.Top = monitor.Y;
            this.Width = monitor.Width;
            this.Height = monitor.Height;
            
            // Update the info text
            MonitorInfoText.Text = $"{monitor.Name} ({monitor.Width}x{monitor.Height})";
            
            this.Show();
        }
    }
}
