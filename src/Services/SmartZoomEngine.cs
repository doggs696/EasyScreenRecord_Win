using System;
using System.Drawing;
using System.Numerics;
using System.Diagnostics;

namespace EasyScreenRecord.Services
{
    public class SmartZoomEngine
    {
        // Config options
        public float MaxZoomLevel { get; set; } = 2.0f;
        public float MinZoomLevel { get; set; } = 1.0f;
        public float ZoomSpeed { get; set; } = 3.0f; // Speed of zoom transition
        public float PanSpeed { get; set; } = 5.0f;  // Speed of panning correction
        public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromSeconds(3);

        // State
        private Vector2 _currentCenter; // Center of the viewport in source coordinates
        private float _currentZoom = 1.0f;
        private DateTime _lastInputTime;
        private Vector2 _screenSize;
        public Rectangle? BaseRegion => _baseRegion; // Expose for logging
        private Rectangle? _baseRegion;
        
        public void SetBaseRegion(Rectangle region)
        {
            _baseRegion = region;
            // Reset center to middle of new region
            _currentCenter = new Vector2(region.Left + region.Width / 2f, region.Top + region.Height / 2f);
        }

        // Target State
        private Vector2? _targetFocus; // Where we want to look (e.g. caret)
        private bool _isTracking;

        public SmartZoomEngine(int width, int height)
        {
            _screenSize = new Vector2(width, height);
            _currentCenter = _screenSize / 2;
            _lastInputTime = DateTime.Now;
        }

        public void ReportInput(Point? focusPoint, bool isTyping)
        {
            if (focusPoint.HasValue)
            {
                // Check bounds if base region is set
                if (_baseRegion.HasValue)
                {
                    if (!_baseRegion.Value.Contains(focusPoint.Value))
                    {
                        // Ignore input outside region
                        return;
                    }
                }

                _targetFocus = new Vector2(focusPoint.Value.X, focusPoint.Value.Y);
                
                // Only start tracking (zooming) if this is a typing event
                if (isTyping)
                {
                    _isTracking = true;
                    _lastInputTime = DateTime.Now;
                }
                else if (_isTracking)
                {
                    // If already tracking, mouse movement extends the session
                    // Optional: decide if mouse alone should keep it alive. 
                    // For now, let's say "interactive" mouse use keeps it alive.
                    _lastInputTime = DateTime.Now;
                }
                // If not tracking and not typing, do nothing (stay in full screen)
            }
        }

        private int _viewLogCount = 0;
        public Rectangle GetViewport(double deltaTime)
        {
            // Update State machine
            var now = DateTime.Now;
            bool isIdle = (now - _lastInputTime) > IdleTimeout;
            
            // Define active bounds
            float boundLeft = 0;
            float boundTop = 0;
            float boundWidth = _screenSize.X;
            float boundHeight = _screenSize.Y;

            if (_baseRegion.HasValue)
            {
                boundLeft = _baseRegion.Value.Left;
                boundTop = _baseRegion.Value.Top;
                boundWidth = _baseRegion.Value.Width;
                boundHeight = _baseRegion.Value.Height;
            }

            Vector2 defaultCenter = new Vector2(boundLeft + boundWidth / 2f, boundTop + boundHeight / 2f);

            if (_viewLogCount++ % 120 == 0) 
            {
            }
            
            float targetZoom = _currentZoom;
            Vector2 targetCenter = _currentCenter;

            if (isIdle)
            {
                // Zoom out to full region (Minimum zoom)
                targetZoom = MinZoomLevel;
                targetCenter = defaultCenter;
            }
            else if (_isTracking && _targetFocus.HasValue)
            {
                // Zoom in to target
                targetZoom = MaxZoomLevel;
                targetCenter = _targetFocus.Value;
            }
            else
            {
                // Maintain current or drift back?
                targetZoom = MinZoomLevel;
                targetCenter = defaultCenter;
            }

            // Viewport Size IN GLOBAL COORDS (how much world we see)
            // If Zoom=1.0, we see the whole BoundWidth/Height.
            // If Zoom=2.0, we see half.
            Vector2 viewportSize = new Vector2(boundWidth / targetZoom, boundHeight / targetZoom);
            
            targetCenter = ClampCenter(targetCenter, viewportSize, boundLeft, boundTop, boundWidth, boundHeight);

            // Interpolation (Smooth damp)
            float t = (float)deltaTime * ZoomSpeed; 
            if (t > 1.0f) t = 1.0f;
            
            _currentZoom = PolyLerp(_currentZoom, targetZoom, t);
            
            // Pan smoothing might need to be faster or slower
            float panT = (float)deltaTime * PanSpeed;
            if (panT > 1.0f) panT = 1.0f;
            
            _currentCenter = Vector2.Lerp(_currentCenter, targetCenter, panT);

            // Final Viewport Calculation
            float currentW = boundWidth / _currentZoom; // Size in global units
            float currentH = boundHeight / _currentZoom;
            
            // Re-clamp current center just in case
            _currentCenter = ClampCenter(_currentCenter, new Vector2(currentW, currentH), boundLeft, boundTop, boundWidth, boundHeight);
            
            int left = (int)(_currentCenter.X - currentW / 2);
            int top = (int)(_currentCenter.Y - currentH / 2);
            
            return new Rectangle(left, top, (int)currentW, (int)currentH);
        }

        private Vector2 ClampCenter(Vector2 center, Vector2 viewSize, float boundLeft, float boundTop, float boundW, float boundH)
        {
            float minX = boundLeft + viewSize.X / 2;
            float maxX = (boundLeft + boundW) - viewSize.X / 2;
            
            // Handle case where zoom forces view to be larger than bounds (shouldn't happen if minZoom=1)
            if (minX > maxX) minX = maxX = boundLeft + boundW / 2;

            float minY = boundTop + viewSize.Y / 2;
            float maxY = (boundTop + boundH) - viewSize.Y / 2;

            if (minY > maxY) minY = maxY = boundTop + boundH / 2;

            return new Vector2(
                Math.Clamp(center.X, minX, maxX),
                Math.Clamp(center.Y, minY, maxY)
            );
        }

        private float PolyLerp(float a, float b, float t)
        {
            // Simple linear for now, can be eased
            return a + (b - a) * t;
        }
    }
}
