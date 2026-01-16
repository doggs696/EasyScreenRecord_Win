using System;
using System.Windows;

namespace EasyScreenRecord.Views
{
    public partial class RecordingFrameWindow : Window
    {
        private Rect _region;

        public RecordingFrameWindow(Rect region)
        {
            InitializeComponent();
            _region = region;
            
            // Make the window cover the entire virtual screen  
            this.Left = SystemParameters.VirtualScreenLeft;
            this.Top = SystemParameters.VirtualScreenTop;
            this.Width = SystemParameters.VirtualScreenWidth;
            this.Height = SystemParameters.VirtualScreenHeight;
            
            // Set the fullscreen geometry to cover the virtual screen
            FullScreenGeometry.Rect = new Rect(0, 0, this.Width, this.Height);
            
            // Set the cutout geometry (relative to the window)
            // The region is in virtual screen coordinates, so we need to offset it
            double offsetX = region.Left - SystemParameters.VirtualScreenLeft;
            double offsetY = region.Top - SystemParameters.VirtualScreenTop;
            CutoutGeometry.Rect = new Rect(offsetX, offsetY, region.Width, region.Height);
            
            // Show the green border around the recording region (inner frame)
            int borderThickness = 4;
            FrameBorder.Margin = new Thickness(
                offsetX - borderThickness, 
                offsetY - borderThickness, 
                0, 0);
            FrameBorder.Width = region.Width + (borderThickness * 2);
            FrameBorder.Height = region.Height + (borderThickness * 2);
            FrameBorder.Visibility = Visibility.Visible;
        }
        
        private void Window_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // When clicking on the dimmed area, activate the MainWindow
            var mainWindow = Application.Current.MainWindow;
            if (mainWindow != null)
            {
                mainWindow.Activate();
            }
        }
    }
}
