using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HoldSpace.Models;
using HoldSpace.Services;

namespace HoldSpace.Views
{
    public partial class TriggerSettingsView : System.Windows.Controls.UserControl
    {
        private SettingsService? _settingsService;
        private bool _isRecording;

        public TriggerSettingsView()
        {
            InitializeComponent();
        }

        public void Initialize(SettingsService settingsService)
        {
            _settingsService = settingsService;
            LoadSettings();
        }

        private void LoadSettings()
        {
            if (_settingsService != null)
            {
                var settings = _settingsService.CurrentSettings;
                BtnRecord.Content = settings.Trigger.KeyName;
                SliderDelay.Value = settings.HoldDelayMs;
                TxtDelayValue.Text = settings.HoldDelayMs.ToString();

                // Show warning initially if Windows key was saved
                string key = settings.Trigger.KeyName.ToLowerInvariant();
                if (key.Contains("win") || key.Contains("lwin") || key.Contains("rwin"))
                {
                    WinKeyWarning.Visibility = Visibility.Visible;
                }
                else
                {
                    WinKeyWarning.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void BtnRecord_Click(object sender, RoutedEventArgs e)
        {
            if (_isRecording) return;

            var window = Window.GetWindow(this);
            if (window != null)
            {
                _isRecording = true;
                App.IsRecordingTrigger = true;
                BtnRecord.Content = "[ Press a key... ]";
                RecordBanner.Visibility = Visibility.Visible;
                
                // Temporarily focus the window to capture keypress
                window.Focus();
                window.PreviewKeyDown += Window_PreviewKeyDown;
            }
        }

        private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (!_isRecording || _settingsService == null) return;

            // Get Key (handling system keys like Alt/Win)
            Key key = e.Key == Key.System ? e.SystemKey : e.Key;
            
            // Allow cancelling recording with Escape if desired, but since Esc is a key itself,
            // we can let them bind it, or cancel if it was pressed. Let's bind it if they want.
            int vk = KeyInterop.VirtualKeyFromKey(key);

            // Special mapping for Windows keys
            if (key == Key.LWin || key == Key.RWin)
            {
                WinKeyWarning.Visibility = Visibility.Visible;
            }
            else
            {
                WinKeyWarning.Visibility = Visibility.Collapsed;
            }

            // Save key settings
            _settingsService.CurrentSettings.Trigger.KeyName = key.ToString();
            _settingsService.CurrentSettings.Trigger.VirtualKeyCode = vk;
            _settingsService.SaveSettings();

            BtnRecord.Content = key.ToString();

            // Notify application to restart the hook with the new key settings
            if (System.Windows.Application.Current is App myApp)
            {
                RestartAppHook();
            }

            // Cleanup recording state
            var window = sender as Window;
            if (window != null)
            {
                window.PreviewKeyDown -= Window_PreviewKeyDown;
            }

            _isRecording = false;
            App.IsRecordingTrigger = false;
            RecordBanner.Visibility = Visibility.Collapsed;
            e.Handled = true;
        }

        private void SliderDelay_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtDelayValue == null || _settingsService == null) return;

            int delay = (int)Math.Round(e.NewValue);
            TxtDelayValue.Text = delay.ToString();
            
            _settingsService.CurrentSettings.HoldDelayMs = delay;
            _settingsService.SaveSettings();

            RestartAppHook();
        }

        private void RestartAppHook()
        {
            try
            {
                // Access app hook restarting via reflection or direct call
                var restartMethod = System.Windows.Application.Current.GetType().GetMethod("RestartHook");
                restartMethod?.Invoke(System.Windows.Application.Current, null);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to notify app of hook change: {ex.Message}");
            }
        }
    }
}
