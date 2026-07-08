using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using HoldSpace.Services;

namespace HoldSpace.Views
{
    public partial class GeneralSettingsView : System.Windows.Controls.UserControl
    {
        private SettingsService? _settingsService;

        public GeneralSettingsView()
        {
            InitializeComponent();
        }

        public void Initialize(SettingsService settingsService)
        {
            _settingsService = settingsService;
            LoadSettings();
            ShortcutList?.Initialize(settingsService);
        }

        private void LoadSettings()
        {
            if (_settingsService != null)
            {
                // Sync settings with the actual registry state
                bool isRegistryEnabled = StartupService.IsStartupEnabled();
                _settingsService.CurrentSettings.StartWithWindows = isRegistryEnabled;
                _settingsService.SaveSettings();

                CheckStartup.IsChecked = isRegistryEnabled;
                CheckMinimize.IsChecked = _settingsService.CurrentSettings.MinimizeToTray;
                CheckStartMinimized.IsChecked = _settingsService.CurrentSettings.StartMinimizedToTray;
            }
        }

        private void CheckStartup_Checked(object sender, RoutedEventArgs e)
        {
            UpdateStartup(true);
        }

        private void CheckStartup_Unchecked(object sender, RoutedEventArgs e)
        {
            UpdateStartup(false);
        }

        private void UpdateStartup(bool enable)
        {
            if (_settingsService != null)
            {
                _settingsService.CurrentSettings.StartWithWindows = enable;
                _settingsService.SaveSettings();
                StartupService.SetStartWithWindows(enable);
            }
        }

        private void CheckMinimize_Checked(object sender, RoutedEventArgs e)
        {
            UpdateMinimize(true);
        }

        private void CheckMinimize_Unchecked(object sender, RoutedEventArgs e)
        {
            UpdateMinimize(false);
        }

        private void UpdateMinimize(bool minimize)
        {
            if (_settingsService != null)
            {
                _settingsService.CurrentSettings.MinimizeToTray = minimize;
                _settingsService.SaveSettings();
            }
        }

        private void CheckStartMinimized_Checked(object sender, RoutedEventArgs e)
        {
            UpdateStartMinimized(true);
        }

        private void CheckStartMinimized_Unchecked(object sender, RoutedEventArgs e)
        {
            UpdateStartMinimized(false);
        }

        private void UpdateStartMinimized(bool startMinimized)
        {
            if (_settingsService != null)
            {
                _settingsService.CurrentSettings.StartMinimizedToTray = startMinimized;
                _settingsService.SaveSettings();
            }
        }

        private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            BackupService.OpenDataFolder();
        }

        private void BtnExportBackup_Click(object sender, RoutedEventArgs e)
        {
            var win = Window.GetWindow(this);
            if (win != null && _settingsService != null)
            {
                BackupService.ExportBackup(win, _settingsService);
            }
        }

        private void BtnImportBackup_Click(object sender, RoutedEventArgs e)
        {
            var win = Window.GetWindow(this);
            if (win != null && _settingsService != null)
            {
                bool success = BackupService.ImportBackup(win, _settingsService);
                if (success)
                {
                    // Reload all active views
                    var mw = win as MainWindow;
                    mw?.OnProfileChanged();
                    LoadSettings();
                }
            }
        }

        private void BtnResetOverlay_Click(object sender, RoutedEventArgs e)
        {
            if (_settingsService == null) return;

            var result = System.Windows.MessageBox.Show(
                "Are you sure you want to reset overlay appearance settings to defaults?",
                "Reset Overlay Settings",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                var s = _settingsService.CurrentSettings;
                s.Theme = "Dark";
                s.OverlayOpacity = 0.8;
                s.BackgroundDim = true;
                s.AnimationDurationMs = 150;
                s.HoverDelayMs = 80;
                _settingsService.SaveSettings();

                var mw = Window.GetWindow(this) as MainWindow;
                mw?.OnProfileChanged();

                System.Windows.MessageBox.Show("Overlay appearance settings have been reset to defaults.", "HoldSpace Settings", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnResetCurrentMode_Click(object sender, RoutedEventArgs e)
        {
            if (_settingsService == null) return;

            var active = _settingsService.ActiveProfile;
            if (active == null) return;

            var result = System.Windows.MessageBox.Show(
                $"Are you sure you want to reset all shortcuts in the current Mode '{active.Name}' to default templates? This cannot be undone.",
                "Reset Current Mode Layout",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                active.Items = new System.Collections.Generic.List<HoldSpace.Models.CanvasItem>
                {
                    new HoldSpace.Models.CanvasItem { Id = Guid.NewGuid().ToString(), Title = "Google", X = 35, Y = 40, Action = new HoldSpace.Models.ShortcutAction { Type = "website", Target = "https://www.google.com" } },
                    new HoldSpace.Models.CanvasItem { Id = Guid.NewGuid().ToString(), Title = "GitHub", X = 65, Y = 40, Action = new HoldSpace.Models.ShortcutAction { Type = "website", Target = "https://github.com" } },
                    new HoldSpace.Models.CanvasItem { Id = Guid.NewGuid().ToString(), Title = "Command Prompt", X = 50, Y = 65, Action = new HoldSpace.Models.ShortcutAction { Type = "app", Target = "cmd.exe" } },
                };
                _settingsService.SaveLayout();
                
                var mw = Window.GetWindow(this) as MainWindow;
                mw?.OnProfileChanged();

                System.Windows.MessageBox.Show($"Shortcuts in '{active.Name}' have been reset to defaults.", "HoldSpace Settings", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnResetAll_Click(object sender, RoutedEventArgs e)
        {
            if (_settingsService == null) return;

            var result = System.Windows.MessageBox.Show(
                "Are you sure you want to perform a complete system reset? This will delete all your custom Modes, layout items, and preferences. Your logs will be preserved.",
                "Reset All Settings & Layouts",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                _settingsService.ResetLayoutToDefault();
                _settingsService.CurrentSettings = new HoldSpace.Models.AppSettings();
                _settingsService.SaveSettings();

                // Re-initialize to trigger safe defaults or onboarding
                _settingsService.CurrentSettings.HasCompletedOnboarding = false;
                _settingsService.SaveSettings();

                var mw = Window.GetWindow(this) as MainWindow;
                if (mw != null)
                {
                    mw.Hide();
                    var onboardingWindow = new OnboardingWindow(_settingsService, mw);
                    onboardingWindow.Show();
                }

                System.Windows.MessageBox.Show("Complete system reset completed.", "HoldSpace Settings", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnCopyFeedback_Click(object sender, RoutedEventArgs e)
        {
            DiagnosticsService.CopyFeedbackTemplate();
        }

        private void BtnOpenLogs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string folder = LoggerService.GetLogsDirectory();
                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }
                
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = folder,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to open logs folder: {ex.Message}", "HoldSpace Settings", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnExportDiag_Click(object sender, RoutedEventArgs e)
        {
            var win = Window.GetWindow(this);
            if (win != null && _settingsService != null)
            {
                DiagnosticsService.ExportDiagnostics(win, _settingsService);
            }
        }

        private void BtnRestartOnboarding_Click(object sender, RoutedEventArgs e)
        {
            if (_settingsService == null) return;

            var result = System.Windows.MessageBox.Show(
                "Are you sure you want to restart the onboarding setup process? This will reset your trigger key settings.",
                "Restart Onboarding Flow",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _settingsService.CurrentSettings.HasCompletedOnboarding = false;
                _settingsService.SaveSettings();

                var mainWindow = Window.GetWindow(this) as MainWindow;
                if (mainWindow != null)
                {
                    mainWindow.Hide();
                    var onboardingWindow = new OnboardingWindow(_settingsService, mainWindow);
                    onboardingWindow.Show();
                }
            }
        }

        public void RefreshShortcutList()
        {
            if (_settingsService != null)
            {
                ShortcutList?.Initialize(_settingsService);
            }
        }

        private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateResponsiveLayout(e.NewSize.Width);
        }

        private void UpdateResponsiveLayout(double width)
        {
            if (SettingsGrid == null || CardBehavior == null || CardBackup == null || CardReset == null || CardAbout == null) return;

            SettingsGrid.ColumnDefinitions.Clear();
            SettingsGrid.RowDefinitions.Clear();

            if (width > 800)
            {
                // Wide window: 2 columns
                SettingsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                SettingsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                SettingsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                SettingsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                // Left Column
                Grid.SetColumn(CardBehavior, 0);
                Grid.SetRow(CardBehavior, 0);
                
                Grid.SetColumn(CardAbout, 0);
                Grid.SetRow(CardAbout, 1);

                // Right Column
                Grid.SetColumn(CardBackup, 1);
                Grid.SetRow(CardBackup, 0);

                Grid.SetColumn(CardReset, 1);
                Grid.SetRow(CardReset, 1);
            }
            else
            {
                // Narrow/Medium window: 1 column
                SettingsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                SettingsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                SettingsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                SettingsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                SettingsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                Grid.SetColumn(CardBehavior, 0);
                Grid.SetRow(CardBehavior, 0);

                Grid.SetColumn(CardBackup, 0);
                Grid.SetRow(CardBackup, 1);

                Grid.SetColumn(CardReset, 0);
                Grid.SetRow(CardReset, 2);

                Grid.SetColumn(CardAbout, 0);
                Grid.SetRow(CardAbout, 3);
            }
        }
    }
}
