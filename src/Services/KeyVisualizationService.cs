using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Threading;
using EasyScreenRecord.Helpers;
using EasyScreenRecord.Views;

namespace EasyScreenRecord.Services
{
    public class KeyVisualizationService : IDisposable
    {
        private KeyOSDWindow? _osdWindow;
        private DispatcherTimer _hideTimer;
        private bool _isListening;
        
        // Track modifier keys manually as Hook doesn't give state for other keys
        private bool _isCtrlDown;
        private bool _isAltDown;
        private bool _isShiftDown;
        private bool _isWinDown;

        public KeyVisualizationService()
        {
            _hideTimer = new DispatcherTimer();
            _hideTimer.Interval = TimeSpan.FromSeconds(2.0);
            _hideTimer.Tick += (s, e) => HideOSD();
        }

        public void Start()
        {
            if (_isListening) return;
            KeyboardHook.KeyEvent += OnKeyEvent;
            KeyboardHook.Start();
            _isListening = true;
        }

        public void Stop()
        {
            if (!_isListening) return;
            KeyboardHook.KeyEvent -= OnKeyEvent;
            KeyboardHook.Stop();
            HideOSD();
            _isListening = false;
        }

        private void OnKeyEvent(int vkCode, bool isDown, bool isSystem)
        {
            // Update Modifiers
            // VK_SHIFT 0x10, VK_CONTROL 0x11, VK_MENU 0x12 (Alt), VK_LWIN 0x5B, VK_RWIN 0x5C
            if (vkCode == 0x10 || vkCode == 0xA0 || vkCode == 0xA1) _isShiftDown = isDown;
            else if (vkCode == 0x11 || vkCode == 0xA2 || vkCode == 0xA3) _isCtrlDown = isDown;
            else if (vkCode == 0x12 || vkCode == 0xA4 || vkCode == 0xA5) _isAltDown = isDown;
            else if (vkCode == 0x5B || vkCode == 0x5C) _isWinDown = isDown;

            if (!isDown) return; // Only process on key down

            // Ignore pure modifier presses
            if (IsModifier(vkCode)) return;

            // Filter logic: Show if Modifier is down OR Special Key
            bool hasModifier = _isCtrlDown || _isAltDown || _isShiftDown || _isWinDown;
            bool isSpecial = IsSpecialKey(vkCode);

            if (hasModifier || isSpecial)
            {
                string text = BuildKeyString(vkCode);
                ShowOSD(text);
            }
        }

        private bool IsModifier(int vkCode)
        {
            return (vkCode >= 0x10 && vkCode <= 0x12) || (vkCode >= 0xA0 && vkCode <= 0xA5) || vkCode == 0x5B || vkCode == 0x5C;
        }

        private bool IsSpecialKey(int vkCode)
        {
            // Enter(0x0D), Esc(0x1B), Backspace(0x08), Tab(0x09), Delete(0x2E)
            // Function Keys F1-F12 (0x70 - 0x7B)
            // PageUp(0x21), PageDown(0x22), End(0x23), Home(0x24), Left(0x25), Up, Right, Down(0x28)
            // PrintScreen(0x2C), Insert(0x2D)
            return vkCode == 0x0D || vkCode == 0x1B || vkCode == 0x08 || vkCode == 0x09 || vkCode == 0x2E ||
                   (vkCode >= 0x70 && vkCode <= 0x7B) ||
                   (vkCode >= 0x21 && vkCode <= 0x28) ||
                   vkCode == 0x2C || vkCode == 0x2D;
        }

        private string BuildKeyString(int vkCode)
        {
            List<string> parts = new List<string>();
            if (_isCtrlDown) parts.Add("Ctrl");
            if (_isShiftDown) parts.Add("Shift");
            if (_isAltDown) parts.Add("Alt");
            if (_isWinDown) parts.Add("Win");

            string keyName = ((System.Windows.Forms.Keys)vkCode).ToString();
            
            // Cleanup names
            if (vkCode >= 0x30 && vkCode <= 0x39) keyName = ((char)('0' + (vkCode - 0x30))).ToString(); // 0-9
            else if (vkCode >= 0x41 && vkCode <= 0x5A) keyName = ((char)('A' + (vkCode - 0x41))).ToString(); // A-Z
            
            // Special mappings
            if (vkCode == 0x0D) keyName = "Enter";
            else if (vkCode == 0x1B) keyName = "Esc";
            else if (vkCode == 0x08) keyName = "Backspace";
            else if (vkCode == 0x09) keyName = "Tab";
            else if (vkCode == 0x20) keyName = "Space";

            parts.Add(keyName);
            return string.Join(" + ", parts);
        }

        private void ShowOSD(string text)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_osdWindow == null)
                {
                    _osdWindow = new KeyOSDWindow();
                    // Center Bottom
                    _osdWindow.Left = (SystemParameters.PrimaryScreenWidth - _osdWindow.Width) / 2;
                    _osdWindow.Top = SystemParameters.PrimaryScreenHeight - _osdWindow.Height - 100;
                }

                _osdWindow.SetText(text);
                _osdWindow.Show();
                
                // Restart Timer
                _hideTimer.Stop();
                _hideTimer.Start();
            });
        }

        private void HideOSD()
        {
            _hideTimer.Stop();
            _osdWindow?.Hide();
        }

        public void Dispose()
        {
            Stop();
            _osdWindow?.Close();
            _osdWindow = null;
        }
    }
}
