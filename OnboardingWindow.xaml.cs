using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using HoldSpace.Models;
using HoldSpace.Services;

namespace HoldSpace
{
    public partial class OnboardingWindow : Window
    {
        private readonly SettingsService _settingsService;
        private readonly MainWindow _mainWindow;
        private int _currentScreenIndex = 0;
        private bool _isRecordingKey = false;
        private bool _hasTestSucceeded = false;

        public OnboardingWindow(SettingsService settingsService, MainWindow mainWindow)
        {
            InitializeComponent();
            _settingsService = settingsService;
            _mainWindow = mainWindow;
            
            // Set initial trigger ComboBox selection
            ComboTriggerSource.SelectedIndex = 0; // Caps Lock
            
            // Subscribe to onboarding demo selection event
            OverlayService.DemoItemLaunched += OnOnboardingDemoItemLaunched;
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        private void OnOnboardingDemoItemLaunched(CanvasItem item)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _hasTestSucceeded = true;
                TxtTestStatus.Text = "Success!";
                TxtTestSuccess.Text = $"Perfect. You hovered and launched: '{item.Title}'";
                TestSuccessBanner.Visibility = Visibility.Visible;
                BtnContinue.IsEnabled = true; // Unlock navigation
            }));
        }

        private void ComboTriggerSource_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_settingsService == null || CustomKeyPanel == null) return;

            int index = ComboTriggerSource.SelectedIndex;
            if (index == 6) // Wait, let's look at the items in the ComboBox:
            {
                // index 0: Caps Lock
                // index 1: Left Alt
                // index 2: Right Alt
                // index 3: Mouse Button 4
                // index 4: Mouse Button 5
                // index 5: Custom keyboard key
            }

            if (index == 5) // Custom keyboard key
            {
                CustomKeyPanel.Visibility = Visibility.Visible;
                BtnRecordOnboarding.Content = "Record Key";
            }
            else
            {
                CustomKeyPanel.Visibility = Visibility.Collapsed;

                string keyName = "Capital";
                int vk = 0x14; // default Caps Lock

                switch (index)
                {
                    case 0:
                        keyName = "Capital";
                        vk = 0x14;
                        break;
                    case 1:
                        keyName = "LMenu";
                        vk = 0xA4;
                        break;
                    case 2:
                        keyName = "RMenu";
                        vk = 0xA5;
                        break;
                    case 3:
                        keyName = "XButton1";
                        vk = 0x05;
                        break;
                    case 4:
                        keyName = "XButton2";
                        vk = 0x06;
                        break;
                }

                _settingsService.CurrentSettings.Trigger.KeyName = keyName;
                _settingsService.CurrentSettings.Trigger.VirtualKeyCode = vk;
                _settingsService.SaveSettings();

                RestartAppHook();
            }

            UpdateWinWarningVisibility();
        }

        private void BtnRecordOnboarding_Click(object sender, RoutedEventArgs e)
        {
            if (_isRecordingKey) return;

            _isRecordingKey = true;
            App.IsRecordingTrigger = true;
            BtnRecordOnboarding.Content = "[ Press a key... ]";
            
            Focus();
            PreviewKeyDown += OnboardingWindow_PreviewKeyDown;
        }

        private void OnboardingWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (!_isRecordingKey) return;

            Key key = e.Key == Key.System ? e.SystemKey : e.Key;
            int vk = KeyInterop.VirtualKeyFromKey(key);

            _settingsService.CurrentSettings.Trigger.KeyName = key.ToString();
            _settingsService.CurrentSettings.Trigger.VirtualKeyCode = vk;
            _settingsService.SaveSettings();

            BtnRecordOnboarding.Content = key.ToString();
            RestartAppHook();
            UpdateWinWarningVisibility();

            // Cleanup recording state
            PreviewKeyDown -= OnboardingWindow_PreviewKeyDown;
            _isRecordingKey = false;
            App.IsRecordingTrigger = false;
            e.Handled = true;
        }

        private void UpdateWinWarningVisibility()
        {
            if (_settingsService != null && WinKeyWarningOnboarding != null)
            {
                string key = _settingsService.CurrentSettings.Trigger.KeyName.ToLowerInvariant();
                if (key.Contains("win") || key.Contains("lwin") || key.Contains("rwin"))
                {
                    WinKeyWarningOnboarding.Visibility = Visibility.Visible;
                }
                else
                {
                    WinKeyWarningOnboarding.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void RestartAppHook()
        {
            try
            {
                var restartMethod = System.Windows.Application.Current.GetType().GetMethod("RestartHook");
                restartMethod?.Invoke(System.Windows.Application.Current, null);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to notify app of hook change: {ex.Message}");
            }
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            NavigateToScreen(_currentScreenIndex - 1);
        }

        private void BtnContinue_Click(object sender, RoutedEventArgs e)
        {
            NavigateToScreen(_currentScreenIndex + 1);
        }

        private void NavigateToScreen(int newIndex)
        {
            // Transition Slide logic and hook management
            if (_currentScreenIndex == 2)
            {
                // Unloading Screen 2 (Test Gesture): restore normal overlay mode
                App.IsOnboardingTestActive = false;
            }

            _currentScreenIndex = newIndex;
            TabOnboarding.SelectedIndex = _currentScreenIndex;

            // Manage button visibilities and states based on screen
            BtnBack.Visibility = _currentScreenIndex > 0 ? Visibility.Visible : Visibility.Collapsed;
            BtnSkipWelcome.Visibility = _currentScreenIndex == 0 ? Visibility.Visible : Visibility.Collapsed;

            // Slide specific loading checks
            if (_currentScreenIndex == 2)
            {
                // Slide 2: Test Gesture. Hook test mode
                App.IsOnboardingTestActive = true;
                
                // Do not allow continue until successful
                BtnContinue.IsEnabled = _hasTestSucceeded;
            }
            else
            {
                BtnContinue.IsEnabled = true;
            }

            if (_currentScreenIndex == 3)
            {
                BtnContinue.Content = "Create Canvas";
            }
            else
            {
                BtnContinue.Content = "Continue";
            }

            if (_currentScreenIndex == 4)
            {
                // Slide 4: Finish. Load final stats
                CreateStarterShortcuts();
                
                TxtSummaryTrigger.Text = _settingsService.CurrentSettings.Trigger.KeyName;
                TxtSummaryShortcuts.Text = $"{_settingsService.CurrentLayout.Items.Count} shortcuts added";

                BtnContinue.Visibility = Visibility.Collapsed;
                BtnFinish.Visibility = Visibility.Visible;
            }
            else
            {
                BtnContinue.Visibility = Visibility.Visible;
                BtnFinish.Visibility = Visibility.Collapsed;
            }

            // Sync visual progress dots
            UpdateProgressDots();
        }

        private void UpdateProgressDots()
        {
            var activeBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(10, 132, 255));
            var inactiveBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(44, 44, 46));

            Dot0.Fill = _currentScreenIndex == 0 ? activeBrush : inactiveBrush;
            Dot1.Fill = _currentScreenIndex == 1 ? activeBrush : inactiveBrush;
            Dot2.Fill = _currentScreenIndex == 2 ? activeBrush : inactiveBrush;
            Dot3.Fill = _currentScreenIndex == 3 ? activeBrush : inactiveBrush;
            Dot4.Fill = _currentScreenIndex == 4 ? activeBrush : inactiveBrush;
        }

        private void ShortcutCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border)
            {
                var chk = FindVisualChild<System.Windows.Controls.CheckBox>(border);
                if (chk != null)
                {
                    chk.IsChecked = !chk.IsChecked;
                }
            }
        }

        private void CreateStarterShortcuts()
        {
            if (_settingsService == null) return;

            // Clear previous to prevent duplicate canvas items
            _settingsService.CurrentLayout.Items.Clear();

            var itemsToAdd = new List<CanvasItem>();

            // 1. Core Windows
            if (ChkDownloads.IsChecked == true)
            {
                string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                itemsToAdd.Add(new CanvasItem
                {
                    Title = "Downloads",
                    Action = new ShortcutAction { Type = "folder", Target = path }
                });
            }
            if (ChkDocuments.IsChecked == true)
            {
                string path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                itemsToAdd.Add(new CanvasItem
                {
                    Title = "Documents",
                    Action = new ShortcutAction { Type = "folder", Target = path }
                });
            }
            if (ChkDesktop.IsChecked == true)
            {
                string path = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                itemsToAdd.Add(new CanvasItem
                {
                    Title = "Desktop",
                    Action = new ShortcutAction { Type = "folder", Target = path }
                });
            }

            // 2. System Utilities
            if (ChkTaskManager.IsChecked == true)
            {
                itemsToAdd.Add(new CanvasItem
                {
                    Title = "Task Manager",
                    Action = new ShortcutAction { Type = "app", Target = "taskmgr.exe" }
                });
            }
            if (ChkTerminal.IsChecked == true)
            {
                itemsToAdd.Add(new CanvasItem
                {
                    Title = "Terminal",
                    Action = new ShortcutAction { Type = "app", Target = "cmd.exe" }
                });
            }
            if (ChkPowerShell.IsChecked == true)
            {
                itemsToAdd.Add(new CanvasItem
                {
                    Title = "PowerShell",
                    Action = new ShortcutAction { Type = "app", Target = "powershell.exe" }
                });
            }
            if (ChkSnippingTool.IsChecked == true)
            {
                itemsToAdd.Add(new CanvasItem
                {
                    Title = "Snipping",
                    Action = new ShortcutAction { Type = "app", Target = "snippingtool.exe" }
                });
            }
            if (ChkCalculator.IsChecked == true)
            {
                itemsToAdd.Add(new CanvasItem
                {
                    Title = "Calculator",
                    Action = new ShortcutAction { Type = "app", Target = "calc.exe" }
                });
            }
            if (ChkSettings.IsChecked == true)
            {
                itemsToAdd.Add(new CanvasItem
                {
                    Title = "Settings",
                    Action = new ShortcutAction { Type = "website", Target = "ms-settings:" }
                });
            }
            if (ChkControlPanel.IsChecked == true)
            {
                itemsToAdd.Add(new CanvasItem
                {
                    Title = "Control Panel",
                    Action = new ShortcutAction { Type = "app", Target = "control.exe" }
                });
            }

            // 3. Weblinks
            if (ChkGoogle.IsChecked == true)
            {
                itemsToAdd.Add(new CanvasItem
                {
                    Title = "Google",
                    Action = new ShortcutAction { Type = "website", Target = "https://www.google.com" }
                });
            }
            if (ChkChatGPT.IsChecked == true)
            {
                itemsToAdd.Add(new CanvasItem
                {
                    Title = "ChatGPT",
                    Action = new ShortcutAction { Type = "website", Target = "https://chatgpt.com" }
                });
            }
            if (ChkGitHub.IsChecked == true)
            {
                itemsToAdd.Add(new CanvasItem
                {
                    Title = "GitHub",
                    Action = new ShortcutAction { Type = "website", Target = "https://github.com" }
                });
            }
            if (ChkStackOverflow.IsChecked == true)
            {
                itemsToAdd.Add(new CanvasItem
                {
                    Title = "StackOverflow",
                    Action = new ShortcutAction { Type = "website", Target = "https://stackoverflow.com" }
                });
            }
            if (ChkNotion.IsChecked == true)
            {
                itemsToAdd.Add(new CanvasItem
                {
                    Title = "Notion",
                    Action = new ShortcutAction { Type = "website", Target = "https://notion.so" }
                });
            }
            if (ChkYouTube.IsChecked == true)
            {
                itemsToAdd.Add(new CanvasItem
                {
                    Title = "YouTube",
                    Action = new ShortcutAction { Type = "website", Target = "https://youtube.com" }
                });
            }
            if (ChkGoogleDrive.IsChecked == true)
            {
                itemsToAdd.Add(new CanvasItem
                {
                    Title = "Google Drive",
                    Action = new ShortcutAction { Type = "website", Target = "https://drive.google.com" }
                });
            }
            if (ChkGmail.IsChecked == true)
            {
                itemsToAdd.Add(new CanvasItem
                {
                    Title = "Gmail",
                    Action = new ShortcutAction { Type = "website", Target = "https://mail.google.com" }
                });
            }
            if (ChkSlack.IsChecked == true)
            {
                itemsToAdd.Add(new CanvasItem
                {
                    Title = "Slack",
                    Action = new ShortcutAction { Type = "website", Target = "https://slack.com" }
                });
            }
            if (ChkFigma.IsChecked == true)
            {
                itemsToAdd.Add(new CanvasItem
                {
                    Title = "Figma",
                    Action = new ShortcutAction { Type = "website", Target = "https://figma.com" }
                });
            }

            // Distribute items in concentric circles on the canvas (no overlapping)
            for (int i = 0; i < itemsToAdd.Count; i++)
            {
                var item = itemsToAdd[i];
                item.Id = Guid.NewGuid().ToString();

                double radius = 18.0;
                double centerX = 50.0;
                double centerY = 50.0;
                double angle;

                if (itemsToAdd.Count <= 6)
                {
                    angle = (2 * Math.PI * i) / itemsToAdd.Count;
                }
                else if (itemsToAdd.Count <= 14)
                {
                    if (i < 5)
                    {
                        radius = 16.0;
                        angle = (2 * Math.PI * i) / 5;
                    }
                    else
                    {
                        radius = 32.0;
                        angle = (2 * Math.PI * (i - 5)) / (itemsToAdd.Count - 5);
                    }
                }
                else
                {
                    // concentric layers (inner: 4, middle: 8, outer: remaining)
                    if (i < 4)
                    {
                        radius = 14.0;
                        angle = (2 * Math.PI * i) / 4;
                    }
                    else if (i < 12)
                    {
                        radius = 26.0;
                        angle = (2 * Math.PI * (i - 4)) / 8;
                    }
                    else
                    {
                        radius = 38.0;
                        angle = (2 * Math.PI * (i - 12)) / (itemsToAdd.Count - 12);
                    }
                }

                item.X = Math.Round(centerX + radius * Math.Cos(angle), 1);
                item.Y = Math.Round(centerY + radius * Math.Sin(angle), 1);

                _settingsService.CurrentLayout.Items.Add(item);
            }

            _settingsService.SaveLayout();
        }

        private void BtnSkipWelcome_Click(object sender, RoutedEventArgs e)
        {
            CompleteOnboarding();
        }

        private void BtnFinish_Click(object sender, RoutedEventArgs e)
        {
            CompleteOnboarding();
        }

        private void CompleteOnboarding()
        {
            // Unsubscribe test gesture listener
            OverlayService.DemoItemLaunched -= OnOnboardingDemoItemLaunched;
            App.IsOnboardingTestActive = false;

            // Update configuration status
            _settingsService.CurrentSettings.HasCompletedOnboarding = true;
            _settingsService.SaveSettings();

            // Refresh the dashboard sub-views to load the newly selected starter items
            _mainWindow.CanvasEditor?.Initialize(_settingsService);
            _mainWindow.GeneralSettings?.RefreshShortcutList();
            _mainWindow.OverviewViewControl?.RefreshMetrics();

            // Open primary dashboard window
            _mainWindow.Show();
            _mainWindow.Activate();
            
            // Close onboarding window
            Close();
        }

        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T childType)
                {
                    return childType;
                }
                var childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                {
                    return childOfChild;
                }
            }
            return null;
        }

        protected override void OnClosed(EventArgs e)
        {
            OverlayService.DemoItemLaunched -= OnOnboardingDemoItemLaunched;
            App.IsOnboardingTestActive = false;
            base.OnClosed(e);
        }
    }
}
