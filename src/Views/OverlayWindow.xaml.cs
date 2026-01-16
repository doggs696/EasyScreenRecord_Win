using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace EasyScreenRecord.Views
{
    public partial class OverlayWindow : Window
    {
        private Point _startPoint;
        private bool _isDragging;
        public Rect SelectedRegion { get; private set; } = Rect.Empty;
        public bool IsConfirmed { get; private set; } = false;

        public OverlayWindow()
        {
            InitializeComponent();
            
            // Explicitly set Normal to avoid maximization quirks
            this.WindowState = WindowState.Normal;

            // Cover all screens
            this.Left = SystemParameters.VirtualScreenLeft;
            this.Top = SystemParameters.VirtualScreenTop;
            this.Width = SystemParameters.VirtualScreenWidth;
            this.Height = SystemParameters.VirtualScreenHeight;

            // Update Geometry size to match
             ((RectangleGeometry)((CombinedGeometry)DimmingLayer.Data).Geometry1).Rect = new Rect(0, 0, this.Width, this.Height);
            
            // Ensure keyboard focus when window loads
            this.Loaded += (s, e) => 
            {
                this.Activate();
                this.Focus();
                Keyboard.Focus(this);
            };
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                _startPoint = e.GetPosition(this);
                _isDragging = true;
                SelectionRectGeometry.Rect = new Rect(_startPoint, _startPoint);
                UpdateBorder(_startPoint, _startPoint);
            }
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                var currentPoint = e.GetPosition(this);
                var rect = new Rect(_startPoint, currentPoint);
                SelectionRectGeometry.Rect = rect;
                UpdateBorder(_startPoint, currentPoint);
            }
        }

        private void Window_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                SelectedRegion = SelectionRectGeometry.Rect;
                // Auto confirm on release? Or wait for Enter? Let's wait for Enter or Double Click.
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                this.Close();
            }
            else if (e.Key == Key.Enter)
            {
                if (SelectedRegion != Rect.Empty && SelectedRegion.Width > 0 && SelectedRegion.Height > 0)
                {
                    IsConfirmed = true;
                    this.Close();
                }
            }
        }

        private void UpdateBorder(Point p1, Point p2)
        {
            var x = Math.Min(p1.X, p2.X);
            var y = Math.Min(p1.Y, p2.Y);
            var w = Math.Abs(p1.X - p2.X);
            var h = Math.Abs(p1.Y - p2.Y);

            SelectionBorder.Margin = new Thickness(x, y, 0, 0);
            SelectionBorder.Width = w;
            SelectionBorder.Height = h;
            SelectionBorder.Visibility = Visibility.Visible;
        }
    }
}
