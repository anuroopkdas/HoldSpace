using System;
using System.IO;
using Microsoft.Win32;

namespace HoldSpace.Services
{
    public static class StartupService
    {
        private const string RegistryKeyName = "HoldSpace";
        private const string RunRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public static void SetStartWithWindows(bool enable)
        {
            try
            {
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunRegistryPath, true))
                {
                    if (key != null)
                    {
                        if (enable)
                        {
                            string exePath = Environment.ProcessPath ?? string.Empty;
                            if (string.IsNullOrEmpty(exePath))
                            {
                                exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
                            }

                            if (!string.IsNullOrEmpty(exePath))
                            {
                                // Handle dotnet assembly resolution (.dll extension when running dev build)
                                if (Path.GetExtension(exePath).Equals(".dll", StringComparison.OrdinalIgnoreCase))
                                {
                                    string exeCandidate = Path.ChangeExtension(exePath, ".exe");
                                    if (File.Exists(exeCandidate))
                                    {
                                        exePath = exeCandidate;
                                    }
                                }
                                
                                key.SetValue(RegistryKeyName, $"\"{exePath}\"");
                                System.Diagnostics.Debug.WriteLine($"Startup registry key set to: {exePath}");
                            }
                        }
                        else
                        {
                            key.DeleteValue(RegistryKeyName, false);
                            System.Diagnostics.Debug.WriteLine("Startup registry key deleted.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to set start-with-windows registry: {ex.Message}");
            }
        }

        public static bool IsStartupEnabled()
        {
            try
            {
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunRegistryPath, false))
                {
                    if (key != null)
                    {
                        return key.GetValue(RegistryKeyName) != null;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to read startup registry: {ex.Message}");
            }
            return false;
        }
    }
}
