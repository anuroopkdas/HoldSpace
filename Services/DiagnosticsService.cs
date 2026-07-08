using System;
using System.IO;
using System.IO.Compression;
using System.Windows;
using Microsoft.Win32;
using System.Text;
using MessageBox = System.Windows.MessageBox;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using Clipboard = System.Windows.Clipboard;

namespace HoldSpace.Services
{
    public static class DiagnosticsService
    {
        public static void ExportDiagnostics(Window owner, SettingsService settingsService)
        {
            try
            {
                var userConfirms = MessageBox.Show(
                    "Would you like to include your custom shortcut titles/names in the diagnostics report? Personal shortcut targets (like URLs or file paths) will be omitted automatically.",
                    "Include Shortcut Names?",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                bool includeTitles = userConfirms == MessageBoxResult.Yes;

                SaveFileDialog sfd = new SaveFileDialog
                {
                    Filter = "HoldSpace Diagnostics (*.zip)|*.zip",
                    FileName = $"holdspace_diagnostics_{DateTime.Now:yyyyMMdd_HHmmss}.zip"
                };

                if (sfd.ShowDialog(owner) == true)
                {
                    string tempDir = Path.Combine(Path.GetTempPath(), "HoldSpaceDiagTemp_" + Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(tempDir);

                    var sysInfo = new StringBuilder();
                    sysInfo.AppendLine($"HoldSpace Version: 0.1.0-beta");
                    sysInfo.AppendLine($"OS Version: {Environment.OSVersion}");
                    sysInfo.AppendLine($"64-Bit OS: {Environment.Is64BitOperatingSystem}");
                    sysInfo.AppendLine($"Runtime Version: {Environment.Version}");
                    sysInfo.AppendLine($"Machine Name Hash: {Environment.MachineName.GetHashCode()}");
                    sysInfo.AppendLine($"Local Time: {DateTime.Now}");
                    sysInfo.AppendLine($"Safe Mode Active: {App.IsSafeMode}");
                    sysInfo.AppendLine($"Safe Mode Reason: {App.SafeModeReason}");
                    File.WriteAllText(Path.Combine(tempDir, "system_info.txt"), sysInfo.ToString());

                    var settingsSummary = new StringBuilder();
                    var settings = settingsService.CurrentSettings;
                    settingsSummary.AppendLine("--- Settings Summary ---");
                    settingsSummary.AppendLine($"Theme: {settings.Theme}");
                    settingsSummary.AppendLine($"HoldDelayMs: {settings.HoldDelayMs}");
                    settingsSummary.AppendLine($"Trigger Key: {settings.Trigger?.KeyName} (VK: {settings.Trigger?.VirtualKeyCode})");
                    settingsSummary.AppendLine($"MinimizeToTray: {settings.MinimizeToTray}");
                    settingsSummary.AppendLine($"StartMinimizedToTray: {settings.StartMinimizedToTray}");
                    settingsSummary.AppendLine($"OverlayOpacity: {settings.OverlayOpacity}");
                    settingsSummary.AppendLine($"BackgroundDim: {settings.BackgroundDim}");
                    settingsSummary.AppendLine($"AnimationDurationMs: {settings.AnimationDurationMs}");
                    settingsSummary.AppendLine($"HoverDelayMs: {settings.HoverDelayMs}");
                    settingsSummary.AppendLine($"StartWithWindows: {settings.StartWithWindows}");
                    settingsSummary.AppendLine($"HasCompletedOnboarding: {settings.HasCompletedOnboarding}");
                    
                    settingsSummary.AppendLine();
                    settingsSummary.AppendLine("--- Profiles Summary ---");
                    settingsSummary.AppendLine($"Active Profile ID: {settingsService.ProfilesLayout.ActiveProfileId}");
                    settingsSummary.AppendLine($"Total Profiles: {settingsService.ProfilesLayout.Profiles.Count}");
                    foreach (var p in settingsService.ProfilesLayout.Profiles)
                    {
                        settingsSummary.AppendLine($"Profile: '{p.Name}' (ID: {p.Id}) | Items: {p.Items?.Count ?? 0}");
                        if (p.Items != null)
                        {
                            int idx = 1;
                            foreach (var item in p.Items)
                            {
                                string name = includeTitles ? item.Title : $"Shortcut {idx}";
                                string type = item.Action?.Type ?? "unknown";
                                settingsSummary.AppendLine($"  - {name} [Type: {type}] (Target: [REDACTED])");
                                idx++;
                            }
                        }
                    }
                    File.WriteAllText(Path.Combine(tempDir, "sanitized_settings.txt"), settingsSummary.ToString());

                    string logsDir = LoggerService.GetLogsDirectory();
                    if (Directory.Exists(logsDir))
                    {
                        string targetLogsDir = Path.Combine(tempDir, "Logs");
                        Directory.CreateDirectory(targetLogsDir);
                        foreach (var file in Directory.GetFiles(logsDir))
                        {
                            try
                            {
                                File.Copy(file, Path.Combine(targetLogsDir, Path.GetFileName(file)));
                            }
                            catch {}
                        }
                    }

                    if (File.Exists(sfd.FileName))
                        File.Delete(sfd.FileName);

                    ZipFile.CreateFromDirectory(tempDir, sfd.FileName);
                    Directory.Delete(tempDir, true);

                    LoggerService.Info($"Diagnostics exported successfully to: {sfd.FileName}");
                    MessageBox.Show("Diagnostics report exported successfully! Please share this zip file with the development team.", "HoldSpace - Diagnostics", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                LoggerService.Error("Failed to export diagnostics.", ex);
                MessageBox.Show($"Failed to export diagnostics: {ex.Message}", "HoldSpace - Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public static void CopyFeedbackTemplate()
        {
            try
            {
                var template = new StringBuilder();
                template.AppendLine("HoldSpace Feedback");
                template.AppendLine("==================");
                template.AppendLine();
                template.AppendLine("What happened:");
                template.AppendLine("[Describe what you did and what issue/error occurred]");
                template.AppendLine();
                template.AppendLine("What did you expect to happen:");
                template.AppendLine("[Describe what you expected HoldSpace to do]");
                template.AppendLine();
                template.AppendLine("Steps to reproduce:");
                template.AppendLine("1. ");
                template.AppendLine("2. ");
                template.AppendLine("3. ");
                template.AppendLine();
                template.AppendLine("App version: 0.1.0-beta");
                template.AppendLine($"Windows version: {Environment.OSVersion}");
                template.AppendLine($"Safe Mode Active: {App.IsSafeMode}");

                Clipboard.SetText(template.ToString());
                LoggerService.Info("Feedback template copied to clipboard.");
                MessageBox.Show("A structured feedback template has been copied to your clipboard. You can paste it into your bug report or email!", "Feedback Template Copied", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                LoggerService.Error("Failed to copy feedback template.", ex);
            }
        }
    }
}
