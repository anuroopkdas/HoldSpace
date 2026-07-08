using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using HoldSpace.Services;

namespace HoldSpace
{
    public partial class MainWindow : Window
    {
        private readonly SettingsService _settingsService;

        public MainWindow(SettingsService settingsService)
        {
            InitializeComponent();
            _settingsService = settingsService;
            OverviewViewControl.Initialize(_settingsService);
            ProfilesViewControl.Initialize(_settingsService);
            CanvasEditor.Initialize(_settingsService);
            TriggerSettings.Initialize(_settingsService);
            AppearanceSettings.Initialize(_settingsService);
            GeneralSettings.Initialize(_settingsService);
            _ = PreloadIconsAsync();
            Loaded += (_, _) => ApplyMicaBackdrop();
        }

        // ── Mica Backdrop (Windows 11) ──────────────────────────────────────

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private void ApplyMicaBackdrop()
        {
            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                // DWMWA_USE_IMMERSIVE_DARK_MODE = 20
                int dark = 1;
                DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));
                // DWMWA_SYSTEMBACKDROP_TYPE = 38  (2 = Mica, 3 = Acrylic, 4 = Tabbed Mica)
                int mica = 2;
                DwmSetWindowAttribute(hwnd, 38, ref mica, sizeof(int));
            }
            catch
            {
                // Windows 10 / older — silently ignore, custom title bar still looks clean
            }
        }

        // ── Title bar interactions ──────────────────────────────────────────

        private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                WindowState = WindowState == WindowState.Maximized
                    ? WindowState.Normal
                    : WindowState.Maximized;
                return;
            }
            DragMove();
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e) =>
            WindowState = WindowState.Minimized;

        private void BtnMaximize_Click(object sender, RoutedEventArgs e) =>
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            if (_settingsService?.CurrentSettings?.MinimizeToTray == true)
                Hide();
            else
                System.Windows.Application.Current.Shutdown();
        }

        // ── Existing methods ────────────────────────────────────────────────

        private async Task PreloadIconsAsync()
        {
            var items = _settingsService.CurrentLayout.Items;
            if (items == null) return;
            foreach (var item in items)
            {
                try { await Services.IconService.GetIconForShortcutAsync(item); item.ResolvedIcon = Services.IconService.GetIconForShortcut(item); }
                catch { }
            }
        }

        public void UpdateHookStatus(HookStatus status)
        {
            Dispatcher.Invoke(() =>
            {
                StatusText.Text = $"Status: {status}";
                OverviewViewControl?.RefreshMetrics();
            });
        }

        public void OnProfileChanged()
        {
            Dispatcher.Invoke(() =>
            {
                CanvasEditor?.Initialize(_settingsService);
                GeneralSettings?.RefreshShortcutList();
                OverviewViewControl?.RefreshMetrics();
                ProfilesViewControl?.Refresh();
                TriggerSettings?.Initialize(_settingsService);
                AppearanceSettings?.Initialize(_settingsService);
                App.TrayInstance?.RefreshModesMenu();
                _ = PreloadIconsAsync();
            });
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (_settingsService?.CurrentSettings?.MinimizeToTray == true)
            {
                e.Cancel = true;
                Hide();
            }
            else
            {
                System.Windows.Application.Current.Shutdown();
            }
        }
    }
}