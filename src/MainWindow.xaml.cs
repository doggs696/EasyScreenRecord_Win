using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using EasyScreenRecord.Helpers;
using EasyScreenRecord.Services;

namespace EasyScreenRecord
{
    public partial class MainWindow : Window
    {
        private ScreenRecorder _recorder;
        private Helpers.SystemDispatcherQueueHelper _dispatcherHelper;
        private Windows.Storage.StorageFile? _file;

        public MainWindow()
        {
            InitializeComponent();
            _dispatcherHelper = new Helpers.SystemDispatcherQueueHelper();
            _dispatcherHelper.EnsureSystemDispatcherQueue();

            _recorder = new ScreenRecorder();
            
            // Handle Deactivated to keep window on top during recording
            this.Deactivated += MainWindow_Deactivated;
        }
        
        private void MainWindow_Deactivated(object? sender, EventArgs e)
        {
            // If recording with overlay, re-assert topmost and activate
            if (_recordingFrame != null && this.Topmost)
            {
                this.Activate();
            }
        }

        public bool IsExit { get; set; } = false;

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);
            if (!IsExit)
            {
                e.Cancel = true;
                this.Hide(); // Minimize to tray
            }
        }

        public void ForceExit()
        {
            IsExit = true;
            this.Close();
        }

        public bool IsRecording => _recorder != null && _recorder.IsRecording;

        private EasyScreenRecord.Views.RecordingFrameWindow? _recordingFrame;

        public async void StartRecording(Rect? region = null)
        {
            if (IsRecording) return;

            try
            {
                var item = MonitorHelper.CreateItemForPrimaryMonitor();
                
                // Create file
                var myVideos = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
                var fileName = $"recording_{DateTime.Now:yyyyMMdd_HHmmss}.mp4";
                var filePath = Path.Combine(myVideos, fileName);
                
                var storageFolder = Windows.Storage.KnownFolders.VideosLibrary;
                _file = await storageFolder.CreateFileAsync(fileName, Windows.Storage.CreationCollisionOption.GenerateUniqueName);

                // Show Recording Frame IMMEDIATELY if Region is selected
                // This prevents the "visual gap" where overlay closes but frame hasn't appeared yet.
                if (region.HasValue)
                {
                    _recordingFrame = new EasyScreenRecord.Views.RecordingFrameWindow(region.Value);
                    _recordingFrame.Show();
                    this.Topmost = true; // Keep MainWindow above the dimming overlay
                    _recorder.Log($"RecordingFrame Debug: L={_recordingFrame.Left}, T={_recordingFrame.Top}, W={_recordingFrame.Width}, H={_recordingFrame.Height}, Handle={new System.Windows.Interop.WindowInteropHelper(_recordingFrame).Handle}");
                }

                await _recorder.StartRecordingToFileAsync(item, _file, region);
                
                StatusText.Text = $"録画中: {_file.Name}";
                FullscreenButton.IsEnabled = false;
                FullscreenButton.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xAA, 0xAA, 0xAA));
                RegionButton.IsEnabled = false;
                RegionButton.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xAA, 0xAA, 0xAA));
                StopButton.IsEnabled = true;
                StopButton.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0x55, 0x55));
                
                // Update Tray Icon
                if (MyNotifyIcon != null)
                {
                    MyNotifyIcon.ToolTipText = "EasyScreenRecord (Recording)";
                    // Ideally change icon to 'Recording' state
                }
                
                // Start OSD
                App.KeyVisualizationService.Start();
            }
            catch (Exception ex)
            {
                // Close frame if error occurs
                if (_recordingFrame != null) { _recordingFrame.Close(); _recordingFrame = null; }

                var errorMsg = $"Error: {ex.ToString()}";
                LogBox.Text = errorMsg;
                await System.IO.File.WriteAllTextAsync("error.log", errorMsg);
                MessageBox.Show("エラーが発生しました。ログボックスの内容をコピーするか、error.logを確認してください。");
            }
        }

        public async void StartFullscreenRecording(IntPtr monitorHandle)
        {
            if (IsRecording) return;

            try
            {
                var item = MonitorHelper.CreateItemForMonitor(monitorHandle);
                
                // Create file
                var myVideos = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
                var fileName = $"recording_{DateTime.Now:yyyyMMdd_HHmmss}.mp4";
                var filePath = Path.Combine(myVideos, fileName);
                
                var storageFolder = Windows.Storage.KnownFolders.VideosLibrary;
                _file = await storageFolder.CreateFileAsync(fileName, Windows.Storage.CreationCollisionOption.GenerateUniqueName);

                // No RecordingFrameWindow for fullscreen - just record the entire screen
                await _recorder.StartRecordingToFileAsync(item, _file, null);
                
                StatusText.Text = $"録画中: {_file.Name}";
                FullscreenButton.IsEnabled = false;
                FullscreenButton.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xAA, 0xAA, 0xAA));
                RegionButton.IsEnabled = false;
                RegionButton.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xAA, 0xAA, 0xAA));
                StopButton.IsEnabled = true;
                StopButton.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0x55, 0x55));
                
                // Update Tray Icon
                if (MyNotifyIcon != null)
                {
                    MyNotifyIcon.ToolTipText = "EasyScreenRecord (Recording)";
                }
                
                // Start OSD
                App.KeyVisualizationService.Start();
            }
            catch (Exception ex)
            {
                var errorMsg = $"Error: {ex.ToString()}";
                LogBox.Text = errorMsg;
                await System.IO.File.WriteAllTextAsync("error.log", errorMsg);
                MessageBox.Show("エラーが発生しました。ログボックスの内容をコピーするか、error.logを確認してください。");
            }
        }

        public async void StopRecording()
        {
            if (!IsRecording) return;

            await _recorder.StopRecordingAsync();
            
            // Close Recording Frame
            if (_recordingFrame != null)
            {
                _recordingFrame.Close();
                _recordingFrame = null;
                this.Topmost = false; // Reset MainWindow to normal z-order
            }

            StatusText.Text = "停止しました。";
            FullscreenButton.IsEnabled = true;
            FullscreenButton.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x4C, 0xAF, 0x50));
            RegionButton.IsEnabled = true;
            RegionButton.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x4C, 0xAF, 0x50));
            StopButton.IsEnabled = false;
            StopButton.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xAA, 0xAA, 0xAA));
            
            if (MyNotifyIcon != null)
            {
                MyNotifyIcon.ToolTipText = "EasyScreenRecord";
            }
            
            // Stop OSD
            App.KeyVisualizationService.Stop();
            
            if (_file != null)
            {
                Process.Start("explorer.exe", $"/select,\"{_file.Path}\"");
            }
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            StartRecording(null);
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            StopRecording();
        }
        private void TestCaretButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
               // Delay to let user click into a text box
               System.Threading.Tasks.Task.Delay(2000).Wait();
               
               // Try UIA first
               var point = UIAutomationHelper.GetCaretPosition();
               if (point.HasValue)
               {
                   MessageBox.Show($"Caret (UIA): {point.Value.X}, {point.Value.Y}");
                   return;
               }

               // Try Win32 Fallback
               point = Win32CaretHelper.GetCaretPosition();
               if (point.HasValue)
               {
                   MessageBox.Show($"Caret (Win32): {point.Value.X}, {point.Value.Y}");
                   return;
               }
               
               MessageBox.Show("Caret not found (UIA & Win32 failed).");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
            }
        }
    }
}