using System;
using System.Diagnostics;
using System.Windows;
using HoldSpace.Models;

namespace HoldSpace.Services
{
    public class OverlayService
    {
        private readonly SettingsService _settingsService;
        private readonly LauncherService _launcherService;
        private OverlayWindow? _overlayWindow;

        public OverlayService(SettingsService settingsService)
        {
            _settingsService = settingsService;
            _launcherService = new LauncherService();
        }

        private bool _isTestMode;
        private bool _hasLaunchedThisCycle;

        public static event Action<CanvasItem>? DemoItemLaunched;

        public void ShowOverlay(bool isTestMode = false)
        {
            _isTestMode = isTestMode;
            _hasLaunchedThisCycle = false;
            try
            {
                // Ensure any previous overlay is closed
                CloseOverlayInternal();

                // Fetch latest layout
                var layout = _settingsService.CurrentLayout;

                // Create new overlay window passing both layout, settings, and isTestMode flag
                _overlayWindow = new OverlayWindow(layout, _settingsService.CurrentSettings, isTestMode);

                // Position window on monitor containing the mouse cursor
                var mousePos = System.Windows.Forms.Cursor.Position;
                var activeScreen = System.Windows.Forms.Screen.FromPoint(mousePos);
                var bounds = activeScreen.Bounds;

                _overlayWindow.Left = bounds.Left;
                _overlayWindow.Top = bounds.Top;
                _overlayWindow.Width = bounds.Width;
                _overlayWindow.Height = bounds.Height;

                // Subscribe to Esc pressed event
                OverlayWindow.EscPressed += OnEscPressed;

                _overlayWindow.Show();
                _overlayWindow.Activate();
                
                Debug.WriteLine($"Overlay shown. TestMode={isTestMode}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to show overlay: {ex.Message}\n{ex.StackTrace}");
                System.Windows.MessageBox.Show($"Failed to show overlay:\n\n{ex.Message}\n\n{ex.StackTrace}", "HoldSpace Overlay Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                CloseOverlayInternal();
            }
        }

        private void OnEscPressed()
        {
            CloseOverlay(false);
        }

        public void CloseOverlay(bool executeHovered)
        {
            if (_overlayWindow == null || _hasLaunchedThisCycle) return;

            var hovered = _overlayWindow.HoveredItem;
            double elapsedMs = 0;
            if (hovered != null)
            {
                elapsedMs = (DateTime.UtcNow - _overlayWindow.HoverStartedAt).TotalMilliseconds;
            }

            int hoverDelay = _settingsService.CurrentSettings.HoverDelayMs;
            bool shouldLaunch = executeHovered && hovered != null && elapsedMs >= hoverDelay;

            _hasLaunchedThisCycle = true;

            if (shouldLaunch && hovered != null)
            {
                _overlayWindow.ShowPressedStateAndClose(() =>
                {
                    if (_isTestMode)
                    {
                        Debug.WriteLine($"TEST GESTURE SELECTED: {hovered.Title}");
                        DemoItemLaunched?.Invoke(hovered);
                    }
                    else
                    {
                        Debug.WriteLine($"LAUNCHING SELECT: {hovered.Title} (Target: {hovered.Action.Target})");
                        _launcherService.Launch(hovered.Action);
                    }
                    CloseOverlayInternal();
                });
            }
            else
            {
                Debug.WriteLine("Overlay closed without execution (Cancelled/Escaped/Empty release/Below hover delay).");
                _overlayWindow.FadeOutAndClose(() => CloseOverlayInternal());
            }
        }

        private void CloseOverlayInternal()
        {
            OverlayWindow.EscPressed -= OnEscPressed;
            if (_overlayWindow != null)
            {
                try
                {
                    _overlayWindow.Close();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error closing overlay window: {ex.Message}");
                }
                finally
                {
                    _overlayWindow = null;
                }
            }
        }
    }
}
