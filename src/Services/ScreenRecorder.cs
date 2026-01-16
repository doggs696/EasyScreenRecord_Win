using System;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Threading.Tasks;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Media.Core;
using Windows.Media.Transcoding;
using Windows.Media.MediaProperties;
using Windows.Media;
using EasyScreenRecord.Helpers;
using System.Diagnostics;

namespace EasyScreenRecord.Services
{
    public class ScreenRecorder : IScreenRecorder, IDisposable
    {
        private GraphicsCaptureItem? _item;
        private Direct3D11CaptureFramePool? _framePool;
        private GraphicsCaptureSession? _session;
        private IDirect3DDevice? _device;
        private bool _isRecording;

        public bool IsRecording => _isRecording;

        private MediaStreamSource? _mediaStreamSource;
        private MediaTranscoder? _transcoder;
        private bool _isRecordingToFile;
        
        private Direct3D11CaptureFrame? _latestFrame;
        private object _frameLock = new object();
        private long _startTime;

        private SmartZoomEngine? _zoomEngine;
        private Direct3D11ZoomRenderer? _zoomRenderer;
        private MouseHookHelper? _mouseHook;
        
        private Task? _transcodeAction;
        private bool _isStopping;

        public ScreenRecorder()
        {
            _device = Direct3D11Helper.CreateDevice();
        }

        public async Task StartRecordingToFileAsync(object captureTarget, Windows.Storage.StorageFile file, System.Windows.Rect? region = null)
        {
            Log("Public StartRecordingToFileAsync called");
            if (_isRecording) return;
            
            var item = captureTarget as GraphicsCaptureItem;
            if (item == null) throw new ArgumentException("Capture target must be a GraphicsCaptureItem");
            
            await StartRecordingToFileAsync(item, file, region);
        }

        private System.Drawing.Size _frameSize;
        private System.Drawing.Size _inputSize;

        private async Task StartRecordingToFileAsync(GraphicsCaptureItem item, Windows.Storage.StorageFile file, System.Windows.Rect? region)
        {
            Log($"Internal StartRecordingToFileAsync called (Build: {DateTime.Now:HH:mm:ss}) - Frame Props Fix");
            if (_isRecording) return;
            
            // Allow accessing Size on UI thread before offloading
            var rawW = item.Size.Width;
            var rawH = item.Size.Height;
            _inputSize = new System.Drawing.Size(rawW, rawH);

            int targetW = rawW;
            int targetH = rawH;

            // If region is set, use that as output size
            if (region.HasValue)
            {
                targetW = (int)region.Value.Width;
                targetH = (int)region.Value.Height;
                Log($"Region Selected: {region.Value}");
            }

            // Ensure even dimensions for encoding compatibility
            var w = (targetW % 2 == 0) ? targetW : targetW - 1;
            var h = (targetH % 2 == 0) ? targetH : targetH - 1;
            
            _frameSize = new System.Drawing.Size(w, h); // Use int
            Log($"Output Size: {_frameSize.Width}x{_frameSize.Height}");
            
            // Part 1: Initialize D3D Resources (UI Thread - STA for safety with FramePool/Session)
            _item = item;
            _device = Direct3D11Helper.CreateDevice(); 
            _isStopping = false;

            // Initialize Zoom Engine
            _zoomEngine = new SmartZoomEngine(rawW, rawH); // Engine tracks GLOBAL Item Size
            
            // Apply Settings
            var settings = App.SettingsService.CurrentSettings;
            
            // Configure Zoom Mode
            _zoomEngine.IsManualMode = settings.ZoomMode == Models.ZoomMode.Manual;
            Log($"Zoom Mode: {(settings.ZoomMode == Models.ZoomMode.Manual ? "Manual (Ctrl+Scroll)" : "Auto (Typing Detection)")}");
            
            // Setup mouse hook for manual zoom if needed
            if (_zoomEngine.IsManualMode)
            {
                _mouseHook = new MouseHookHelper();
                _mouseHook.OnCtrlScroll += (delta, point) =>
                {
                    _zoomEngine?.ManualZoom(delta, new System.Drawing.Point(point.X, point.Y));
                };
                _mouseHook.OnMiddleClick += () =>
                {
                    _zoomEngine?.ResetZoom();
                };
                _mouseHook.Start();
                Log("Mouse hook started for manual zoom control");
            }
            
            if (region.HasValue)
            {
                // If Region is selected, user expects specific static crop. Disable Smart Zoom.
                _zoomEngine.MaxZoomLevel = 1.0f; 
                _zoomEngine.ZoomSpeed = (float)settings.ZoomSpeed; // Keep speed just in case needed for initial framing (though start is immediate)
                Log("Region mode: Smart Zoom Disabled (Fixed Crop).");
            }
            else
            {
                // Full Screen mode: Enable Smart Zoom based on settings
                _zoomEngine.MaxZoomLevel = (float)settings.ZoomFactor;
                _zoomEngine.ZoomSpeed = (float)settings.ZoomSpeed;
                Log($"Full Screen mode: Configured Zoom Factor={_zoomEngine.MaxZoomLevel}, Speed={_zoomEngine.ZoomSpeed}");
            }

            if (region.HasValue)
            {
                // Convert WPF Rect (DIPs) to Physical Pixels (Int)
                // We need to know the DPI scaling factor. 
                // Since we don't have easy access to VisualTree here, we can infer scale based on rawW vs VirtualScreen.
                // Or better, assume standard scaling if we can't get it.
                // Actually, the best way is to use the ratio of item.Size (Physical) vs SystemParameters (Virtual).
                // But item.Size is just the specific monitor size.
                
                // Let's assume the region passed from OverlayWindow is in the same coordinate space as the CaptureItem's logical space?
                // No, OverlayWindow is across ALL screens. CaptureItem is usually one monitor.
                
                // CRITICAL FIX: Direct3D11ZoomRenderer expects PIXELS.
                // 'region' is in DIPs relative to the Virtual Screen top-left.
                // We need to map this 'region' to the 'item's' coordinate space.
                
                // For simplicity in this "Primary Monitor" scenario:
                // If item is Primary Monitor, its (0,0) is usually (0,0) in Virtual Screen too.
                // We just need to scale by DPI.
                
                double dpiScaleX = 1.0;
                double dpiScaleY = 1.0;
                
                // Attempt to get DPI from a dummy source or assume standard 100% if failing, 
                // but since rawW is likely multiplied, we can check:
                var primaryScreenWidth = System.Windows.SystemParameters.PrimaryScreenWidth;
                var primaryScreenHeight = System.Windows.SystemParameters.PrimaryScreenHeight;
                
                if (primaryScreenWidth > 0) dpiScaleX = rawW / primaryScreenWidth;
                if (primaryScreenHeight > 0) dpiScaleY = rawH / primaryScreenHeight;
                
                // If the item is NOT primary, this is tricky. 
                // But MonitorHelper.CreateItemForPrimaryMonitor() is used.
                // So item is Primary.
                
                int rX = (int)(region.Value.X * dpiScaleX);
                int rY = (int)(region.Value.Y * dpiScaleY);
                int rW = (int)(region.Value.Width * dpiScaleX);
                int rH = (int)(region.Value.Height * dpiScaleY);

                Log($"Region Fix: DIPs={region.Value} -> Physical=({rX},{rY},{rW},{rH}) Scale=({dpiScaleX:F2},{dpiScaleY:F2})");
                
                var r = new Rectangle(rX, rY, rW, rH);
                _zoomEngine.SetBaseRegion(r);
            }

            _zoomRenderer = new Direct3D11ZoomRenderer(_device);

            // Part 2: Create Session & FramePool (UI Thread STA)
            Log("Creating FramePool (Sync) and Session on UI thread...");
            
            // 1. Setup FramePool using Create (Sync) which uses the DispatcherQueue
            _framePool = Direct3D11CaptureFramePool.Create(
                _device,
                Windows.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized,
                2,
                item.Size); 

            _framePool.FrameArrived += OnFrameArrived;

            _session = _framePool.CreateCaptureSession(item);
            _session.IsCursorCaptureEnabled = settings.ShowCursor; // Apply Cursor Setting
            // Note: IsBorderRequired not available on Windows 10 < build 20348, skipping to avoid runtime error
            _session.StartCapture();
            _isRecording = true;
            _isRecordingToFile = true;

            // Part 3: Start Encoding (Background MTA or Async)
            _ = Task.Run(async () => 
            {
                try {
                    // 2. Setup MediaStreamSource (Output Resolution - matches Source)
                    var videoProperties = VideoEncodingProperties.CreateUncompressed(MediaEncodingSubtypes.Bgra8, (uint)_frameSize.Width, (uint)_frameSize.Height);
                    _mediaStreamSource = new MediaStreamSource(new VideoStreamDescriptor(videoProperties));
                    _mediaStreamSource.BufferTime = TimeSpan.FromSeconds(0);
                    _mediaStreamSource.Starting += OnMediaStreamSourceStarting;
                    _mediaStreamSource.SampleRequested += OnMediaStreamSourceSampleRequested;

                    // 3. Setup Transcoder
                    _transcoder = new MediaTranscoder();
                    _transcoder.HardwareAccelerationEnabled = true;

                    // Create encoding profile
                    var profile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.HD1080p);
                    profile.Video.Width = (uint)_frameSize.Width;
                    profile.Video.Height = (uint)_frameSize.Height;
                    profile.Video.Bitrate = (uint)settings.Bitrate; // Apply Bitrate Setting

                    Log("Preparing Transcoder...");
                    
                    using (var stream = await file.OpenAsync(Windows.Storage.FileAccessMode.ReadWrite))
                    {
                        var prepareOp = await _transcoder.PrepareMediaStreamSourceTranscodeAsync(_mediaStreamSource, stream, profile);
                        
                        if (prepareOp.CanTranscode)
                        {
                            _transcodeAction = prepareOp.TranscodeAsync().AsTask();
                            Log("ScreenRecorder: Transcoding started");
                            await _transcodeAction;
                            Log("ScreenRecorder: Transcoding finished normally");
                        }
                        else
                        {
                            Log($"Unable to prepare transcoder: {prepareOp.FailureReason}");
                            throw new Exception($"Unable to prepare transcoder: {prepareOp.FailureReason}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log($"Recording Error: {ex}");
                    try { await StopRecordingAsync(); } catch {} 
                }
            });
            
            // Start Input Tracking Loop
            _ = InputTrackingLoop();
        }
        
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        private async Task InputTrackingLoop()
        {
            Log("InputTrackingLoop started");
            Point? lastCaretPos = null;
            Point lastMousePos = new Point(0, 0);

            while (_isRecording && !_isStopping)
            {
                 try 
                 {
                     // 1. Get current inputs
                     var caret = UIAutomationHelper.GetCaretPosition();
                     if (!caret.HasValue)
                     {
                         caret = Win32CaretHelper.GetCaretPosition();
                     }
                     
                     GetCursorPos(out POINT p);
                     Point mousePoint = new Point(p.X, p.Y);

                     // 2. Calculate Deltas
                     bool caretMoved = false;
                     if (caret.HasValue)
                     {
                         if (lastCaretPos == null) caretMoved = true;
                         else
                         {
                             double dx = caret.Value.X - lastCaretPos.Value.X;
                             double dy = caret.Value.Y - lastCaretPos.Value.Y;
                             if (dx * dx + dy * dy > 5) caretMoved = true; // Threshold 2px
                         }
                         lastCaretPos = caret;
                     }
                     else
                     {
                         lastCaretPos = null;
                     }

                     bool mouseMoved = false;
                     {
                         double dx = mousePoint.X - lastMousePos.X;
                         double dy = mousePoint.Y - lastMousePos.Y;
                         if (dx * dx + dy * dy > 50) mouseMoved = true; // Threshold ~7px (ignore micro jitters)
                         lastMousePos = mousePoint;
                     }

                     // 3. Decision Logic
                     // If Mouse Moved significantly, it takes precedence for FOCUS (looking around)
                     // But it does NOT trigger "Typing" mode.
                     if (mouseMoved && _zoomEngine != null)
                     {
                         _zoomEngine.ReportInput(mousePoint, false);
                         // Log($"Mouse Moved: {mousePoint}");
                     }
                     else if (caretMoved && caret.HasValue && _zoomEngine != null)
                     {
                         // Caret moved -> User is typing/navigating text
                         // TRIGGER Zoom (set _isTracking = true)
                         _zoomEngine.ReportInput(caret.Value, true);
                         Log($"Caret Moved: {caret}");
                     }
                 }
                 catch(Exception ex) { 
                    Log($"InputTracking Error: {ex.Message}");
                 }
                 
                 await Task.Delay(33); // ~30fps sampling
            }
        }

        private void OnMediaStreamSourceStarting(MediaStreamSource sender, MediaStreamSourceStartingEventArgs args)
        {
            Log("MediaStreamSource Starting");
            args.Request.SetActualStartPosition(TimeSpan.Zero);
            _startTime = Stopwatch.GetTimestamp();
        }

        private async void OnMediaStreamSourceSampleRequested(MediaStreamSource sender, MediaStreamSourceSampleRequestedEventArgs args)
        {
            Log("SampleRequested called");
            
            var request = args.Request;
            var deferral = request.GetDeferral();

            try
            {
                if (_isStopping)
                {
                    request.Sample = null;
                    deferral.Complete();
                    return;
                }

                if (_latestFrame == null)
                {
                    Log("Waiting for first frame...");
                    int waitLimit = 0;
                    while (_latestFrame == null && !_isStopping && waitLimit < 100)
                    {
                         await Task.Delay(10);
                         waitLimit++;
                    }
                    if (_latestFrame == null)
                    {
                         Log("TIMEOUT waiting for first frame.");
                         request.Sample = null; 
                         deferral.Complete();
                         return;
                    }
                }

                lock (_frameLock)
                {
                    if (_latestFrame != null && _zoomRenderer != null)
                    {
                        var ticks = Stopwatch.GetTimestamp() - _startTime;
                        // Use consistent time
                        var timeStamp = TimeSpan.FromTicks((long)(ticks * 10_000_000.0 / Stopwatch.Frequency));
                        
                        // Log every 60 frames approx
                        if (_frameCount % 60 == 0) Log($"SampleRequested: Providing Sample at {timeStamp}");

                        var deltaTime = 0.016; 

                        // Use _frameSize NOT _item.Size
                        var viewport = _zoomEngine?.GetViewport(deltaTime) ?? new System.Drawing.Rectangle(0,0, _frameSize.Width, _frameSize.Height);

                        var outputSurface = _zoomRenderer.CreateOutputSurface(_frameSize.Width, _frameSize.Height);
                        
                        try 
                        {
                            if ((_frameCount % 60) == 0)
                            {
                                Log($"Render Frame {_frameCount}: Viewport={viewport}, Tex={_zoomRenderer.LastTextureSize}, Crop={_zoomRenderer.LastCropRect}, BaseRegion={_zoomEngine.BaseRegion}");
                            }

                            _zoomRenderer.Render(_latestFrame.Surface, outputSurface, viewport, _inputSize);
                            
                            var sample = MediaStreamSample.CreateFromDirect3D11Surface(outputSurface, timeStamp);
                            sample.Processed += (s, e) => {
                                 if (outputSurface is IDisposable d) d.Dispose();
                            };

                            request.Sample = sample;
                        }
                        catch (Exception renderEx)
                        {
                             Log($"Render Failed: {renderEx}");
                             outputSurface.Dispose();
                             throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Error creating sample: {ex.Message}");
                _mediaStreamSource?.NotifyError(MediaStreamSourceErrorStatus.Other);
            }
            finally
            {
                deferral.Complete();
            }
        }

        public async Task StopRecordingAsync()
        {
            if (!_isRecording) return;

            Log("ScreenRecorder: Stopping...");
            _isStopping = true;
            
            if (_transcodeAction != null)
            {
                try {
                     // Wait safely with timeout
                     var cleanupTask = _transcodeAction;
                     if (await Task.WhenAny(cleanupTask, Task.Delay(5000)) == cleanupTask)
                     {
                        Log("Transcode finished normally.");
                     }
                     else
                     {
                        Log("Transcode stop TIMED OUT.");
                     }
                } catch(Exception ex) {
                    Log($"Transcode finish error: {ex.Message}");
                }
            }

            _session?.Dispose();
            _framePool?.Dispose();
            _zoomRenderer?.Dispose();
            
            _session = null;
            _framePool = null;
            _item = null;
            _latestFrame?.Dispose();
            _latestFrame = null;
            _zoomRenderer = null;
            _isRecording = false;
            _isStopping = false;

            Debug.WriteLine("ScreenRecorder: Capture stopped");
        }

        private int _frameCount = 0;
        private void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
        {
            var frame = sender.TryGetNextFrame();
            lock (_frameLock)
            {
                _latestFrame?.Dispose();
                _latestFrame = frame;
            }
            if (_frameCount++ % 60 == 0) Log($"Frame Arrived. Total: {_frameCount}");
        }

        public void Log(string message)
        {
            try
            {
                var path = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop), "EasyScreenRecord_Log.txt");
                System.IO.File.AppendAllText(path, $"{DateTime.Now:HH:mm:ss.fff}: {message}\n");
            }
            catch { }
            Debug.WriteLine(message);
        }

        public void Dispose()
        {
            _mouseHook?.Dispose();
            _mouseHook = null;
            StopRecordingAsync();
            _device?.Dispose();
        }
    }
}
