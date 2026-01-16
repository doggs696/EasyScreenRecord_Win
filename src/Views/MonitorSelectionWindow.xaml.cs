using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using EasyScreenRecord.Helpers;
using EasyScreenRecord.Models;

namespace EasyScreenRecord.Views
{
    public partial class MonitorSelectionWindow : Window
    {
        public MonitorInfo? SelectedMonitor { get; private set; }
        public bool IsConfirmed { get; private set; } = false;
        
        private MonitorPreviewOverlay? _previewOverlay;
        private List<MonitorInfo> _monitors = new List<MonitorInfo>();

        public MonitorSelectionWindow()
        {
            InitializeComponent();
            LoadMonitors();
            
            // Show preview when window loads
            this.Loaded += (s, e) => UpdatePreview();
            
            // Clean up overlay on close
            this.Closed += (s, e) => _previewOverlay?.Close();
        }

        private void LoadMonitors()
        {
            _monitors = MonitorHelper.GetAllMonitors();
            MonitorComboBox.ItemsSource = _monitors;
            
            // Select primary monitor by default
            for (int i = 0; i < _monitors.Count; i++)
            {
                if (_monitors[i].IsPrimary)
                {
                    MonitorComboBox.SelectedIndex = i;
                    break;
                }
            }
        }
        
        private void MonitorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdatePreview();
        }
        
        private void UpdatePreview()
        {
            var selectedMonitor = MonitorComboBox.SelectedItem as MonitorInfo;
            if (selectedMonitor == null) return;
            
            // Close existing preview if any
            _previewOverlay?.Close();
            
            // Create and show new preview
            _previewOverlay = new MonitorPreviewOverlay();
            _previewOverlay.ShowOnMonitor(selectedMonitor);
            
            // Keep this window on top of the preview
            this.Topmost = true;
            this.Activate();
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedMonitor = MonitorComboBox.SelectedItem as MonitorInfo;
            IsConfirmed = SelectedMonitor != null;
            _previewOverlay?.Close();
            this.Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            IsConfirmed = false;
            _previewOverlay?.Close();
            this.Close();
        }
    }
}
