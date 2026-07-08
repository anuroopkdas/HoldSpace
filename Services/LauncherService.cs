using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using HoldSpace.Models;

namespace HoldSpace.Services
{
    public class LauncherService
    {
        public void Launch(ShortcutAction action)
        {
            if (action == null || string.IsNullOrWhiteSpace(action.Type))
            {
                ShowError("Invalid shortcut action.", "Action object or type was null or empty.");
                return;
            }

            string target = action.Target.Trim();
            string expandedTarget = Environment.ExpandEnvironmentVariables(target);

            LoggerService.Info($"Shortcut launch attempted. Type: {action.Type}, Target: {expandedTarget}");

            try
            {
                switch (action.Type.ToLowerInvariant())
                {
                    case "app":
                        LaunchApp(expandedTarget, action.Arguments);
                        break;
                    case "folder":
                        LaunchFolder(expandedTarget);
                        break;
                    case "file":
                        LaunchFile(expandedTarget);
                        break;
                    case "website":
                        LaunchWebsite(expandedTarget);
                        break;
                    case "systemaction":
                        LaunchSystemAction(expandedTarget);
                        break;
                    default:
                        ShowError("Unsupported shortcut type.", $"Type '{action.Type}' is not supported by HoldSpace.");
                        break;
                }
            }
            catch (Exception ex)
            {
                ShowError("Couldn't launch this shortcut. The target may have moved or is unavailable.", ex.Message + "\n\nStack Trace:\n" + ex.StackTrace);
            }
        }

        private void LaunchApp(string target, string arguments)
        {
            bool isSystemCmd = IsSystemCommand(target);
            if (!isSystemCmd && !File.Exists(target))
            {
                ShowError("Couldn't launch this shortcut. The target may have moved or is unavailable.", $"Executable not found at path: '{target}'");
                return;
            }

            var psi = new ProcessStartInfo
            {
                FileName = target,
                Arguments = arguments,
                UseShellExecute = true
            };
            Process.Start(psi);
            LoggerService.Info($"Successfully launched app: {target}");
        }

        private void LaunchFolder(string target)
        {
            if (!Directory.Exists(target))
            {
                ShowError("Couldn't launch this shortcut. The target may have moved or is unavailable.", $"Directory not found at path: '{target}'");
                return;
            }

            var psi = new ProcessStartInfo
            {
                FileName = target,
                UseShellExecute = true
            };
            Process.Start(psi);
            LoggerService.Info($"Successfully launched folder: {target}");
        }

        private void LaunchFile(string target)
        {
            if (!File.Exists(target))
            {
                ShowError("Couldn't launch this shortcut. The target may have moved or is unavailable.", $"File not found at path: '{target}'");
                return;
            }

            var psi = new ProcessStartInfo
            {
                FileName = target,
                UseShellExecute = true
            };
            Process.Start(psi);
            LoggerService.Info($"Successfully launched file: {target}");
        }

        private void LaunchWebsite(string target)
        {
            string url = target;
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                url = "https://" + url;
            }

            var psi = new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            };
            Process.Start(psi);
            LoggerService.Info($"Successfully launched website: {url}");
        }

        private void LaunchSystemAction(string target)
        {
            switch (target.ToLowerInvariant())
            {
                case "settings":
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "ms-settings:",
                        UseShellExecute = true
                    });
                    LoggerService.Info("Successfully launched system action: settings");
                    break;
                case "taskmanager":
                case "taskmgr":
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "taskmgr.exe",
                        UseShellExecute = true
                    });
                    LoggerService.Info("Successfully launched system action: taskmanager");
                    break;
                case "terminal":
                case "cmd":
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        UseShellExecute = true
                    });
                    LoggerService.Info("Successfully launched system action: terminal");
                    break;
                case "lock":
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "rundll32.exe",
                        Arguments = "user32.dll,LockWorkStation",
                        UseShellExecute = true
                    });
                    LoggerService.Info("Successfully launched system action: lock");
                    break;
                case "desktop":
                    try
                    {
                        Type? shellType = Type.GetTypeFromProgID("Shell.Application");
                        if (shellType != null)
                        {
                            object? shellObject = Activator.CreateInstance(shellType);
                            if (shellObject != null)
                            {
                                shellType.InvokeMember("ToggleDesktop", System.Reflection.BindingFlags.InvokeMethod, null, shellObject, null);
                            }
                        }
                        LoggerService.Info("Successfully launched system action: desktop");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to toggle desktop: {ex.Message}");
                        ShowError("Failed to perform desktop system action.", ex.Message);
                    }
                    break;
                case "sleep":
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "rundll32.exe",
                        Arguments = "powrprof.dll,SetSuspendState 0,1,0",
                        UseShellExecute = true,
                        CreateNoWindow = true
                    });
                    LoggerService.Info("Successfully launched system action: sleep");
                    break;
                case "hibernate":
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "rundll32.exe",
                        Arguments = "powrprof.dll,SetSuspendState 1,1,0",
                        UseShellExecute = true,
                        CreateNoWindow = true
                    });
                    LoggerService.Info("Successfully launched system action: hibernate");
                    break;
                case "shutdown":
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "shutdown.exe",
                        Arguments = "/s /t 0",
                        UseShellExecute = true,
                        CreateNoWindow = true
                    });
                    LoggerService.Info("Successfully launched system action: shutdown");
                    break;
                case "restart":
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "shutdown.exe",
                        Arguments = "/r /t 0",
                        UseShellExecute = true,
                        CreateNoWindow = true
                    });
                    LoggerService.Info("Successfully launched system action: restart");
                    break;
                case "signout":
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "shutdown.exe",
                        Arguments = "/l",
                        UseShellExecute = true,
                        CreateNoWindow = true
                    });
                    LoggerService.Info("Successfully launched system action: signout");
                    break;
                default:
                    ShowError("Unknown system action.", $"System action '{target}' is not recognized.");
                    break;
            }
        }

        private bool IsSystemCommand(string fileName)
        {
            string ext = Path.GetExtension(fileName).ToLowerInvariant();
            if (string.IsNullOrEmpty(ext) || ext == ".exe")
            {
                string name = Path.GetFileNameWithoutExtension(fileName).ToLowerInvariant();
                if (name == "cmd" || name == "powershell" || name == "notepad" || name == "explorer" || name == "calc" || name == "taskmgr")
                {
                    return true;
                }
            }
            return false;
        }

        private void ShowError(string friendlyMessage, string technicalDetails)
        {
            LoggerService.Error($"Shortcut launch failed. Friendly: {friendlyMessage} | Technical details: {technicalDetails}");

            var tray = App.TrayInstance;
            if (tray != null)
            {
                tray.ShowNotification("HoldSpace - Launch Error", friendlyMessage, System.Windows.Forms.ToolTipIcon.Warning);
            }
            else
            {
                System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    var result = System.Windows.MessageBox.Show(
                        friendlyMessage + "\n\nWould you like to see technical details?",
                        "HoldSpace - Launch Error",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Error);

                    if (result == MessageBoxResult.Yes)
                    {
                        System.Windows.MessageBox.Show(technicalDetails, "Technical Details", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }));
            }
        }
    }
}
