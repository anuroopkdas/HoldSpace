using System;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using HoldSpace.Models;
using Application = System.Windows.Application;

namespace HoldSpace.Services
{
    public class TrayService : IDisposable
    {
        private NotifyIcon? _notifyIcon;
        private Window? _mainWindow;
        private Action? _onToggleOverlayTest;
        private SettingsService? _settingsService;
        private Action? _onProfileChanged;

        private ToolStripMenuItem? _modesMenuItem;

        public void Initialize(Window mainWindow, Action onToggleOverlayTest,
                               SettingsService settingsService, Action onProfileChanged)
        {
            _mainWindow = mainWindow;
            _onToggleOverlayTest = onToggleOverlayTest;
            _settingsService = settingsService;
            _onProfileChanged = onProfileChanged;

            _notifyIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Text = "HoldSpace",
                Visible = true
            };

            var contextMenu = new ContextMenuStrip();

            var openItem = new ToolStripMenuItem("Open HoldSpace");
            openItem.Click += (s, e) => ShowMainWindow();
            contextMenu.Items.Add(openItem);

            var toggleTestItem = new ToolStripMenuItem("Toggle Overlay Test");
            toggleTestItem.Click += (s, e) => _onToggleOverlayTest?.Invoke();
            contextMenu.Items.Add(toggleTestItem);

            contextMenu.Items.Add(new ToolStripSeparator());

            // Modes submenu
            _modesMenuItem = new ToolStripMenuItem("Modes");
            contextMenu.Items.Add(_modesMenuItem);
            RefreshModesMenu();

            contextMenu.Items.Add(new ToolStripSeparator());

            var exitItem = new ToolStripMenuItem("Exit");
            exitItem.Click += (s, e) => ExitApplication();
            contextMenu.Items.Add(exitItem);

            _notifyIcon.ContextMenuStrip = contextMenu;
            _notifyIcon.DoubleClick += (s, e) => ShowMainWindow();
        }

        public void RefreshModesMenu()
        {
            if (_modesMenuItem == null || _settingsService == null) return;

            _modesMenuItem.DropDownItems.Clear();

            foreach (var profile in _settingsService.ProfilesLayout.Profiles)
            {
                bool isActive = profile.Id == _settingsService.ProfilesLayout.ActiveProfileId;
                var item = new ToolStripMenuItem(profile.Name)
                {
                    Checked = isActive,
                    CheckOnClick = false,
                    Font = isActive
                        ? new System.Drawing.Font(System.Drawing.SystemFonts.MenuFont, System.Drawing.FontStyle.Bold)
                        : System.Drawing.SystemFonts.MenuFont
                };

                string capturedId = profile.Id;
                item.Click += (s, e) =>
                {
                    _settingsService.SetActiveProfile(capturedId);
                    RefreshModesMenu();
                    // Notify the main window to update all views
                    Application.Current.Dispatcher.BeginInvoke(_onProfileChanged ?? (() => { }));
                };

                _modesMenuItem.DropDownItems.Add(item);
            }
        }

        public void ShowMainWindow()
        {
            if (_mainWindow != null)
            {
                _mainWindow.Show();
                if (_mainWindow.WindowState == WindowState.Minimized)
                    _mainWindow.WindowState = WindowState.Normal;
                _mainWindow.Activate();
            }
        }

        public void ShowNotification(string title, string message, ToolTipIcon icon = ToolTipIcon.Info)
        {
            _notifyIcon?.ShowBalloonTip(3000, title, message, icon);
        }

        private void ExitApplication()
        {
            Dispose();
            Application.Current.Shutdown();
        }

        public void Dispose()
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }
        }
    }
}
