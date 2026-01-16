using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows;
using EasyScreenRecord; // For MainWindow

namespace EasyScreenRecord.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        public MainViewModel()
        {
        }

        [RelayCommand]
        private void ShowMainWindow()
        {
            var window = Application.Current.MainWindow;
            if (window != null)
            {
                window.Show();
                window.WindowState = WindowState.Normal;
                window.Activate();
            }
        }

        [RelayCommand]
        private void ShowSettings()
        {
            var settingsWindow = new Views.SettingsWindow();
            settingsWindow.ShowDialog();
        }

        [RelayCommand]
        private void ExitApp()
        {
            if (Application.Current.MainWindow is MainWindow mainWindow)
            {
                mainWindow.ForceExit();
            }
            else
            {
                Application.Current.Shutdown();
            }
        }

        // Start fullscreen recording - shows monitor selection dialog
        [RelayCommand]
        private void StartFullscreenRecording()
        {
            if (Application.Current.MainWindow is MainWindow mainWindow)
            {
                if (mainWindow.IsRecording)
                {
                    mainWindow.StopRecording();
                    return;
                }
                
                // Show monitor selection dialog
                var monitorDialog = new Views.MonitorSelectionWindow();
                monitorDialog.ShowDialog();
                
                if (monitorDialog.IsConfirmed && monitorDialog.SelectedMonitor != null)
                {
                    mainWindow.StartFullscreenRecording(monitorDialog.SelectedMonitor.Handle);
                }
            }
        }
        
        // Start region recording - shows overlay for region selection
        [RelayCommand]
        private void StartRegionRecording()
        {
            if (Application.Current.MainWindow is MainWindow mainWindow)
            {
                if (mainWindow.IsRecording)
                {
                    mainWindow.StopRecording();
                    return;
                }
                
                // Show Overlay for region selection
                var overlay = new Views.OverlayWindow();
                overlay.ShowDialog();
                    
                if (overlay.IsConfirmed)
                {
                    var region = overlay.SelectedRegion;
                    mainWindow.StartRecording(region);
                }
            }
        }
    }
}
