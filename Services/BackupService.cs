using System;
using System.IO;
using System.IO.Compression;
using System.Windows;
using Microsoft.Win32;
using System.Diagnostics;
using MessageBox = System.Windows.MessageBox;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace HoldSpace.Services
{
    public static class BackupService
    {
        private static readonly string AppDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HoldSpace");

        public static void ExportBackup(Window owner, SettingsService settingsService)
        {
            try
            {
                // Force save current settings/layout to disk first so backup is up-to-date
                settingsService?.SaveSettings();
                settingsService?.SaveLayout();

                SaveFileDialog sfd = new SaveFileDialog
                {
                    Filter = "HoldSpace Backup (*.holdspacebackup)|*.holdspacebackup",
                    FileName = $"holdspace_backup_{DateTime.Now:yyyyMMdd_HHmmss}.holdspacebackup"
                };

                if (sfd.ShowDialog(owner) == true)
                {
                    string tempDir = Path.Combine(Path.GetTempPath(), "HoldSpaceBackupTemp_" + Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(tempDir);

                    string settingsFile = Path.Combine(AppDataFolder, "settings.json");
                    string layoutFile = Path.Combine(AppDataFolder, "layout.json");

                    if (File.Exists(settingsFile))
                        File.Copy(settingsFile, Path.Combine(tempDir, "settings.json"));
                    if (File.Exists(layoutFile))
                        File.Copy(layoutFile, Path.Combine(tempDir, "layout.json"));

                    if (File.Exists(sfd.FileName))
                        File.Delete(sfd.FileName);

                    ZipFile.CreateFromDirectory(tempDir, sfd.FileName);
                    Directory.Delete(tempDir, true);

                    LoggerService.Info($"Backup exported successfully to: {sfd.FileName}");
                    MessageBox.Show("Backup exported successfully!", "HoldSpace - Backup", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                LoggerService.Error("Failed to export backup.", ex);
                MessageBox.Show($"Failed to export backup: {ex.Message}", "HoldSpace - Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public static bool ImportBackup(Window owner, SettingsService settingsService)
        {
            try
            {
                var confirm = MessageBox.Show(
                    "Importing a backup will replace your current HoldSpace settings and layouts.\n\nAre you sure you want to continue?",
                    "HoldSpace - Confirm Import",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (confirm != MessageBoxResult.Yes)
                    return false;

                OpenFileDialog ofd = new OpenFileDialog
                {
                    Filter = "HoldSpace Backup (*.holdspacebackup)|*.holdspacebackup"
                };

                if (ofd.ShowDialog(owner) == true)
                {
                    // Create a safety backup first
                    string safetyBackupPath = Path.Combine(AppDataFolder, $"safety_backup_before_import_{DateTime.Now:yyyyMMdd_HHmmss}.holdspacebackup");
                    string tempSafetyDir = Path.Combine(Path.GetTempPath(), "HoldSpaceSafetyBackupTemp_" + Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(tempSafetyDir);

                    string settingsFile = Path.Combine(AppDataFolder, "settings.json");
                    string layoutFile = Path.Combine(AppDataFolder, "layout.json");

                    if (File.Exists(settingsFile))
                        File.Copy(settingsFile, Path.Combine(tempSafetyDir, "settings.json"));
                    if (File.Exists(layoutFile))
                        File.Copy(layoutFile, Path.Combine(tempSafetyDir, "layout.json"));

                    ZipFile.CreateFromDirectory(tempSafetyDir, safetyBackupPath);
                    Directory.Delete(tempSafetyDir, true);
                    LoggerService.Info($"Safety backup created at: {safetyBackupPath}");

                    // Extract import
                    string tempImportDir = Path.Combine(Path.GetTempPath(), "HoldSpaceImportTemp_" + Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(tempImportDir);

                    ZipFile.ExtractToDirectory(ofd.FileName, tempImportDir);

                    string importedSettings = Path.Combine(tempImportDir, "settings.json");
                    string importedLayout = Path.Combine(tempImportDir, "layout.json");

                    if (!File.Exists(importedSettings) || !File.Exists(importedLayout))
                    {
                        Directory.Delete(tempImportDir, true);
                        throw new InvalidDataException("The backup file does not contain valid HoldSpace configuration files.");
                    }

                    // Copy over
                    File.Copy(importedSettings, settingsFile, true);
                    File.Copy(importedLayout, layoutFile, true);

                    Directory.Delete(tempImportDir, true);

                    // Reload the newly imported configurations into memory
                    settingsService.LoadSettings();
                    settingsService.LoadLayout();

                    // Refresh input trigger hooks
                    (System.Windows.Application.Current as App)?.RestartHook();

                    LoggerService.Info($"Backup successfully imported from: {ofd.FileName}");
                    
                    MessageBox.Show("Backup imported successfully! The application settings and layouts have been reloaded.", "HoldSpace - Backup", MessageBoxButton.OK, MessageBoxImage.Information);
                    return true;
                }
            }
            catch (Exception ex)
            {
                LoggerService.Error("Failed to import backup.", ex);
                MessageBox.Show($"Failed to import backup: {ex.Message}", "HoldSpace - Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return false;
        }

        public static void OpenDataFolder()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = AppDataFolder,
                    UseShellExecute = true
                });
                LoggerService.Info("Opened data folder.");
            }
            catch (Exception ex)
            {
                LoggerService.Error("Failed to open data folder.", ex);
            }
        }
    }
}
