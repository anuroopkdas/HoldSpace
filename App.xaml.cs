using System;
using System.Windows;
using HoldSpace.Services;
using HoldSpace.Models;
using MessageBox = System.Windows.MessageBox;

namespace HoldSpace
{
    public partial class App : System.Windows.Application
    {
        private SettingsService? _settingsService;
        private TrayService? _trayService;
        private MainWindow? _mainWindow;
        private InputHookService? _inputHookService;
        private OverlayService? _overlayService;

        public static bool IsOnboardingTestActive { get; set; } = false;
        public static bool IsRecordingTrigger { get; set; } = false;
        public static TrayService? TrayInstance { get; private set; }

        public static bool IsSafeMode { get; set; } = false;
        public static string SafeModeReason { get; set; } = "";

        private System.Threading.Mutex? _instanceMutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            // Single Instance Check
            _instanceMutex = new System.Threading.Mutex(true, "HoldSpace_SingleInstance_Mutex", out bool createdNew);
            if (!createdNew)
            {
                MessageBox.Show("HoldSpace is already running in the background. Check your system tray (near the clock).", "HoldSpace", MessageBoxButton.OK, MessageBoxImage.Information);
                System.Windows.Application.Current.Shutdown();
                return;
            }

            // Global Exception Handlers for diagnostics
            AppDomain.CurrentDomain.UnhandledException += (s, ex) => LogFatalException(ex.ExceptionObject as Exception);
            DispatcherUnhandledException += (s, ex) => {
                LogFatalException(ex.Exception);
                ex.Handled = true;
            };

            base.OnStartup(e);

            // Log app startup
            LoggerService.Info("=================== HoldSpace 0.1.0-beta Started ===================");

            // Check command line arguments for --safe-mode
            bool cliSafeMode = false;
            foreach (var arg in e.Args)
            {
                if (arg.Equals("--safe-mode", StringComparison.OrdinalIgnoreCase) || arg.Equals("-safe", StringComparison.OrdinalIgnoreCase))
                {
                    cliSafeMode = true;
                }
            }

            // Initialize Settings Service
            _settingsService = new SettingsService();
            _settingsService.Initialize();

            if (_settingsService.SafeModeActive || cliSafeMode)
            {
                IsSafeMode = true;
                SafeModeReason = cliSafeMode ? "Started via command line --safe-mode flag." : _settingsService.SafeModeReason;
                LoggerService.Warn($"App started in SAFE MODE. Reason: {SafeModeReason}");
            }

            // Initialize MainWindow
            _mainWindow = new MainWindow(_settingsService);

            // Initialize Overlay Service
            _overlayService = new OverlayService(_settingsService);

            // Initialize Tray Service
            _trayService = new TrayService();
            _trayService.Initialize(_mainWindow, ToggleOverlayTest, _settingsService, OnProfileChangedFromTray);
            TrayInstance = _trayService;

            // Initialize InputHookService
            _inputHookService = new InputHookService();
            _inputHookService.StatusChanged += OnHookStatusChanged;
            _inputHookService.TriggerPressed += OnTriggerPressed;
            _inputHookService.TriggerReleased += OnTriggerReleased;

            int delay = _settingsService?.CurrentSettings?.HoldDelayMs ?? 120;
            byte vk = (byte)(_settingsService?.CurrentSettings?.Trigger?.VirtualKeyCode ?? 0x14);
            _inputHookService.StartHook(delay, vk);

            // Show MainWindow or OnboardingWindow based on settings completion
            if (_settingsService?.CurrentSettings?.HasCompletedOnboarding == true)
            {
                // App starts minimized to tray if enabled
                if (_settingsService.CurrentSettings.MinimizeToTray && _settingsService.CurrentSettings.StartMinimizedToTray)
                {
                    LoggerService.Info("Startup: App minimized to tray.");
                    _mainWindow.Hide();
                }
                else
                {
                    _mainWindow.Show();
                }
            }
            else
            {
                var onboardingWindow = new OnboardingWindow(_settingsService!, _mainWindow!);
                onboardingWindow.Show();
            }

            if (IsSafeMode)
            {
                MessageBox.Show(
                    "HoldSpace started with safe defaults because your settings could not be loaded.\n\nReason: " + SafeModeReason,
                    "HoldSpace - Safe Mode Active",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void LogFatalException(Exception? ex)
        {
            if (ex == null) return;
            string msg = $"Unhandled Exception:\n\n{ex.Message}\n\nStack Trace:\n{ex.StackTrace}";
            System.Diagnostics.Debug.WriteLine(msg);
            System.Windows.MessageBox.Show(msg, "HoldSpace Fatal Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }

        private void OnHookStatusChanged(HookStatus status)
        {
            _mainWindow?.UpdateHookStatus(status);
        }

        private void OnTriggerPressed()
        {
            if (IsSafeMode) return;
            if (IsRecordingTrigger) return;

            foreach (Window win in System.Windows.Application.Current.Windows)
            {
                if (win is AddShortcutWindow)
                {
                    return; // Abort showing overlay if Add Shortcut modal dialog is active
                }
            }

            System.Diagnostics.Debug.WriteLine("Trigger Pressed: Overlay Armed!");
            LoggerService.Info("Overlay opened via trigger key.");
            Dispatcher.BeginInvoke(new Action(() => _overlayService?.ShowOverlay(IsOnboardingTestActive)));
        }

        private void OnTriggerReleased(bool wasArmed)
        {
            System.Diagnostics.Debug.WriteLine($"Trigger Released: Was Armed = {wasArmed}");
            Dispatcher.BeginInvoke(new Action(() => _overlayService?.CloseOverlay(wasArmed)));
        }

        public void ShowOverlayTest()
        {
            Dispatcher.BeginInvoke(new Action(() => _overlayService?.ShowOverlay()));
        }

        private void ToggleOverlayTest()
        {
            ShowOverlayTest();
        }

        private void OnProfileChangedFromTray()
        {
            _mainWindow?.OnProfileChanged();
        }

        public void RestartHook()
        {
            if (_inputHookService != null && _settingsService != null)
            {
                _inputHookService.StopHook();
                int delay = _settingsService.CurrentSettings.HoldDelayMs;
                byte vk = (byte)_settingsService.CurrentSettings.Trigger.VirtualKeyCode;
                _inputHookService.StartHook(delay, vk);
                System.Diagnostics.Debug.WriteLine($"Hook restarted with delay={delay}ms, key={_settingsService.CurrentSettings.Trigger.KeyName}");
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                LoggerService.Info("Application exiting. Auto-saving settings and layouts...");
                _settingsService?.SaveLayout();
                _settingsService?.SaveSettings();
            }
            catch (Exception ex)
            {
                LoggerService.Error("Failed to auto-save settings or layout on exit.", ex);
            }
            _inputHookService?.Dispose();
            _trayService?.Dispose();

            if (_instanceMutex != null)
            {
                try
                {
                    _instanceMutex.ReleaseMutex();
                    _instanceMutex.Dispose();
                }
                catch {}
            }

            base.OnExit(e);
        }
    }
}
