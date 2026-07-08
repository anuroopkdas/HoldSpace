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
                TxtDelayQuip.Text = GetDelayQuip(settings.HoldDelayMs);

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
            if (TxtDelayQuip != null)
            {
                TxtDelayQuip.Text = GetDelayQuip(delay);
            }
            
            _settingsService.CurrentSettings.HoldDelayMs = delay;
            _settingsService.SaveSettings();

            RestartAppHook();
        }

        private string GetDelayQuip(int ms)
        {
            if (ms <= 50) return "50 ms: Near instant. The wingbeat speed of a honeybee. Trigger-happy!";
            if (ms <= 100) return "100 ms: Blink-and-you'll-miss-it. The exact duration of a human eye blink.";
            if (ms <= 150) return "150 ms: Typical human auditory response time. Extremely snappy.";
            if (ms <= 200) return "200 ms: Average human visual reaction speed. Comfortable & responsive.";
            if (ms <= 250) return "250 ms: The time of a standard mouse click. A balanced, reliable sweet spot.";
            if (ms <= 300) return "300 ms: Time for a fastball baseball pitch to travel 30 feet. Very reactive.";
            if (ms <= 350) return "350 ms: Standard double-click timeout boundary. Feels deliberate.";
            if (ms <= 400) return "400 ms: The duration of an average eye fixation when reading text.";
            if (ms <= 450) return "450 ms: The time a hummingbird takes to flap its wings 25 times.";
            if (ms <= 500) return "500 ms: Half a second. Deliberate and highly intentional. Zero typing conflicts.";
            if (ms <= 550) return "550 ms: Average duration of a spoken syllable in conversational English.";
            if (ms <= 600) return "600 ms: The interval of a calm, resting heart beat at 100 beats per minute.";
            if (ms <= 650) return "650 ms: Duration of a single step when walking at a moderate pace.";
            if (ms <= 700) return "700 ms: The time it takes a cheetah to run 20 meters at full velocity.";
            if (ms <= 750) return "750 ms: Three-quarters of a second. Great for heavy typists who key-mash.";
            if (ms <= 800) return "800 ms: Time for a professional Formula 1 crew to change a tire. Precise.";
            if (ms <= 850) return "850 ms: Time for a sound wave to travel 300 meters through the air.";
            if (ms <= 900) return "900 ms: Almost a full second. Intentionally slow, highly safeguarded.";
            if (ms <= 950) return "950 ms: Average reaction time of a very relaxed sloth. Patience is a virtue!";
            return "1000 ms: One full second. The ultimate shield against accidental triggers.";
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
