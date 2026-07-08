using System;
using System.Windows;
using System.Windows.Controls;
using HoldSpace.Services;

namespace HoldSpace.Views
{
    public partial class AppearanceSettingsView : System.Windows.Controls.UserControl
    {
        private SettingsService? _settingsService;

        public AppearanceSettingsView()
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
                CheckDim.IsChecked = settings.BackgroundDim;
                SliderOpacity.Value = settings.OverlayOpacity * 100;
                TxtOpacityValue.Text = Math.Round(settings.OverlayOpacity * 100).ToString();
                SliderAnim.Value = settings.AnimationDurationMs;
                TxtAnimValue.Text = settings.AnimationDurationMs.ToString();
            }
        }

        private void CheckDim_Checked(object sender, RoutedEventArgs e)
        {
            UpdateDim(true);
        }

        private void CheckDim_Unchecked(object sender, RoutedEventArgs e)
        {
            UpdateDim(false);
        }

        private void UpdateDim(bool dim)
        {
            if (_settingsService != null)
            {
                _settingsService.CurrentSettings.BackgroundDim = dim;
                _settingsService.SaveSettings();
            }
        }

        private void SliderOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtOpacityValue == null || _settingsService == null) return;

            double pct = e.NewValue;
            TxtOpacityValue.Text = Math.Round(pct).ToString();

            _settingsService.CurrentSettings.OverlayOpacity = pct / 100.0;
            _settingsService.SaveSettings();
        }

        private void SliderAnim_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtAnimValue == null || _settingsService == null) return;

            int ms = (int)Math.Round(e.NewValue);
            TxtAnimValue.Text = ms.ToString();

            _settingsService.CurrentSettings.AnimationDurationMs = ms;
            _settingsService.SaveSettings();
        }
    }
}
