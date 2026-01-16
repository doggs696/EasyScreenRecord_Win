using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyScreenRecord.Models;
using EasyScreenRecord.Services;
using System.Windows;

namespace EasyScreenRecord.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly ISettingsService _settingsService;

        [ObservableProperty]
        private bool _isAutoZoom;
        
        [ObservableProperty]
        private bool _isManualZoom;

        [ObservableProperty]
        private double _zoomFactor;

        [ObservableProperty]
        private double _zoomSpeed;

        [ObservableProperty]
        private bool _showCursor;

        public SettingsViewModel()
        {
            // Direct access via App or DI. Using App static for simplicity in this port.
            _settingsService = App.SettingsService;
            LoadFromService();
        }

        private void LoadFromService()
        {
            var s = _settingsService.CurrentSettings;
            IsAutoZoom = s.ZoomMode == ZoomMode.Auto;
            IsManualZoom = s.ZoomMode == ZoomMode.Manual;
            ZoomFactor = s.ZoomFactor;
            ZoomSpeed = s.ZoomSpeed;
            ShowCursor = s.ShowCursor;
        }

        [RelayCommand]
        private void Save()
        {
            var s = _settingsService.CurrentSettings;
            s.ZoomMode = IsAutoZoom ? ZoomMode.Auto : ZoomMode.Manual;
            s.ZoomFactor = ZoomFactor;
            s.ZoomSpeed = ZoomSpeed;
            s.ShowCursor = ShowCursor;
            
            _settingsService.Save();

            // Close window
            foreach (Window window in Application.Current.Windows)
            {
                if (window.DataContext == this)
                {
                    window.DialogResult = true;
                    window.Close();
                    break;
                }
            }
        }

        [RelayCommand]
        private void Cancel()
        {
            foreach (Window window in Application.Current.Windows)
            {
                if (window.DataContext == this)
                {
                    window.DialogResult = false;
                    window.Close();
                    break;
                }
            }
        }
    }
}
