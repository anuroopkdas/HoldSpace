using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using HoldSpace.Models;

namespace HoldSpace.Services
{
    public class SettingsService
    {
        private readonly string _appDataFolder;
        private readonly string _settingsPath;
        private readonly string _layoutPath;
        private readonly JsonSerializerOptions _jsonOptions;

        public AppSettings CurrentSettings { get; set; }

        // Multi-profile storage
        public ProfilesLayout ProfilesLayout { get; private set; }

        // Convenience accessor – returns active profile's items as a CanvasLayout
        public CanvasLayout CurrentLayout => new CanvasLayout
        {
            Name = ActiveProfile?.Name ?? "Default",
            Items = ActiveProfile?.Items ?? new List<CanvasItem>()
        };

        public ShortcutProfile? ActiveProfile =>
            ProfilesLayout.Profiles.FirstOrDefault(p => p.Id == ProfilesLayout.ActiveProfileId)
            ?? ProfilesLayout.Profiles.FirstOrDefault();

        public bool SafeModeActive { get; set; }
        public string SafeModeReason { get; set; } = "";

        public SettingsService()
        {
            _appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HoldSpace");
            _settingsPath = Path.Combine(_appDataFolder, "settings.json");
            _layoutPath = Path.Combine(_appDataFolder, "layout.json");

            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true
            };

            CurrentSettings = new AppSettings();
            ProfilesLayout = new ProfilesLayout();
        }

        public void Initialize()
        {
            try
            {
                if (!Directory.Exists(_appDataFolder))
                    Directory.CreateDirectory(_appDataFolder);

                LoadSettings();
                LoadLayout();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SettingsService init failed: {ex.Message}");
                LoggerService.Error("SettingsService initialization critical failure.", ex);
                CurrentSettings = new AppSettings();
                ProfilesLayout = BuildDefaultProfilesLayout(null);
                SafeModeActive = true;
                SafeModeReason = "Critical initialization failure: " + ex.Message;
            }
        }

        public void LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    string json = File.ReadAllText(_settingsPath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions);
                    if (settings != null)
                    {
                        CurrentSettings = settings;
                        LoggerService.Info("Settings loaded successfully.");
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Load settings failed: {ex.Message}");
                LoggerService.Error("Failed to load settings file. Corrupt settings backed up.", ex);
                SafeModeActive = true;
                SafeModeReason = "Settings file was corrupted.";
                
                try
                {
                    if (File.Exists(_settingsPath))
                    {
                        string corruptPath = Path.Combine(_appDataFolder, "settings_corrupt_backup.json");
                        if (File.Exists(corruptPath)) File.Delete(corruptPath);
                        File.Move(_settingsPath, corruptPath);
                    }
                }
                catch {}
            }

            CurrentSettings = new AppSettings();
            SaveSettings();
        }

        public void SaveSettings()
        {
            try
            {
                Directory.CreateDirectory(_appDataFolder);
                File.WriteAllText(_settingsPath, JsonSerializer.Serialize(CurrentSettings, _jsonOptions));
                LoggerService.Info("Settings saved successfully.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Save settings failed: {ex.Message}");
                LoggerService.Error("Failed to save settings.", ex);
            }
        }

        public void LoadLayout()
        {
            try
            {
                if (File.Exists(_layoutPath))
                {
                    string json = File.ReadAllText(_layoutPath);
                    var doc = JsonDocument.Parse(json);

                    // Detect new multi-profile format case-insensitively
                    bool hasProfiles = false;
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        if (prop.Name.Equals("profiles", StringComparison.OrdinalIgnoreCase))
                        {
                            hasProfiles = true;
                            break;
                        }
                    }

                    if (hasProfiles)
                    {
                        var pl = JsonSerializer.Deserialize<ProfilesLayout>(json, _jsonOptions);
                        if (pl != null && pl.Profiles.Count > 0)
                        {
                            ProfilesLayout = pl;
                            LoggerService.Info("Layout profiles loaded successfully.");
                            return;
                        }
                    }

                    // Legacy: flat CanvasLayout with items array → migrate
                    var legacy = JsonSerializer.Deserialize<CanvasLayout>(json, _jsonOptions);
                    ProfilesLayout = BuildDefaultProfilesLayout(legacy);
                    SaveLayout(); // persist migrated format
                    LoggerService.Info("Legacy flat layout migrated successfully.");
                    return;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Load layout failed: {ex.Message}");
                LoggerService.Error("Failed to load layout file. Corrupt layout backed up.", ex);
                SafeModeActive = true;
                SafeModeReason = string.IsNullOrEmpty(SafeModeReason) ? "Layout file was corrupted." : SafeModeReason + " & Layout file was corrupted.";

                try
                {
                    if (File.Exists(_layoutPath))
                    {
                        string corruptPath = Path.Combine(_appDataFolder, "layout_corrupt_backup.json");
                        if (File.Exists(corruptPath)) File.Delete(corruptPath);
                        File.Move(_layoutPath, corruptPath);
                    }
                }
                catch {}
            }

            ProfilesLayout = BuildDefaultProfilesLayout(null);
            SaveLayout();
        }

        public void SaveLayout()
        {
            try
            {
                // Stamp updatedAt on active profile
                if (ActiveProfile != null)
                    ActiveProfile.UpdatedAt = DateTime.UtcNow;

                Directory.CreateDirectory(_appDataFolder);
                File.WriteAllText(_layoutPath, JsonSerializer.Serialize(ProfilesLayout, _jsonOptions));
                LoggerService.Info($"Layout saved successfully. Active Profile: {ProfilesLayout.ActiveProfileId}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Save layout failed: {ex.Message}");
                LoggerService.Error("Failed to save layout.", ex);
            }
        }

        // ── Profile CRUD ────────────────────────────────────────────────────

        public ShortcutProfile CreateProfile(string name)
        {
            var p = new ShortcutProfile { Id = Guid.NewGuid().ToString(), Name = name };
            ProfilesLayout.Profiles.Add(p);
            ProfilesLayout.ActiveProfileId = p.Id;
            SaveLayout();
            LoggerService.Info($"Created and set active new mode: {name} (ID: {p.Id})");
            return p;
        }

        public ShortcutProfile CreateProfile(string name, System.Collections.Generic.List<CanvasItem> items)
        {
            var p = new ShortcutProfile { Id = Guid.NewGuid().ToString(), Name = name, Items = items };
            ProfilesLayout.Profiles.Add(p);
            ProfilesLayout.ActiveProfileId = p.Id;
            SaveLayout();
            LoggerService.Info($"Created and set active new mode from template: {name} (ID: {p.Id})");
            return p;
        }

        public void RenameProfile(string id, string newName)
        {
            var p = ProfilesLayout.Profiles.FirstOrDefault(x => x.Id == id);
            if (p != null)
            {
                string oldName = p.Name;
                p.Name = newName;
                p.UpdatedAt = DateTime.UtcNow;
                SaveLayout();
                LoggerService.Info($"Renamed mode {id} from '{oldName}' to '{newName}'");
            }
        }

        public ShortcutProfile DuplicateProfile(string id)
        {
            var src = ProfilesLayout.Profiles.FirstOrDefault(x => x.Id == id);
            if (src == null) throw new InvalidOperationException("Profile not found.");

            // Deep-copy items with new IDs
            var newItems = src.Items.Select(item => new CanvasItem
            {
                Id = Guid.NewGuid().ToString(),
                Title = item.Title,
                IconPath = item.IconPath,
                X = item.X,
                Y = item.Y,
                Action = new ShortcutAction { Type = item.Action?.Type ?? "", Target = item.Action?.Target ?? "" }
            }).ToList();

            var copy = new ShortcutProfile
            {
                Id = Guid.NewGuid().ToString(),
                Name = src.Name + " Copy",
                Items = newItems
            };

            ProfilesLayout.Profiles.Add(copy);
            ProfilesLayout.ActiveProfileId = copy.Id;
            SaveLayout();
            LoggerService.Info($"Duplicated mode {id} ('{src.Name}') to new mode '{copy.Name}' (ID: {copy.Id})");
            return copy;
        }

        public bool DeleteProfile(string id)
        {
            if (ProfilesLayout.Profiles.Count <= 1) return false; // keep last
            var p = ProfilesLayout.Profiles.FirstOrDefault(x => x.Id == id);
            if (p == null) return false;

            ProfilesLayout.Profiles.Remove(p);
            LoggerService.Info($"Deleted mode: '{p.Name}' (ID: {id})");

            // Switch active if needed
            if (ProfilesLayout.ActiveProfileId == id)
            {
                var nextActive = ProfilesLayout.Profiles[0];
                ProfilesLayout.ActiveProfileId = nextActive.Id;
                LoggerService.Info($"Switched active mode to '{nextActive.Name}' (ID: {nextActive.Id}) because the previous active mode was deleted.");
            }

            SaveLayout();
            return true;
        }

        public void SetActiveProfile(string id)
        {
            var p = ProfilesLayout.Profiles.FirstOrDefault(x => x.Id == id);
            if (p != null)
            {
                string oldActive = ProfilesLayout.ActiveProfileId;
                ProfilesLayout.ActiveProfileId = id;
                SaveLayout();
                LoggerService.Info($"Active mode changed from '{oldActive}' to '{id}' ('{p.Name}')");
            }
        }

        // ── Legacy helpers ─────────────────────────────────────────────────

        private ProfilesLayout BuildDefaultProfilesLayout(CanvasLayout? legacy)
        {
            var defaultProfile = new ShortcutProfile
            {
                Id = "default",
                Name = "Default",
                Items = legacy?.Items ?? BuildDefaultItems()
            };

            return new ProfilesLayout
            {
                ActiveProfileId = "default",
                Profiles = new List<ShortcutProfile> { defaultProfile }
            };
        }

        private List<CanvasItem> BuildDefaultItems()
        {
            return new List<CanvasItem>
            {
                new CanvasItem { Id = Guid.NewGuid().ToString(), Title = "Google", X = 35, Y = 40, Action = new ShortcutAction { Type = "website", Target = "https://www.google.com" } },
                new CanvasItem { Id = Guid.NewGuid().ToString(), Title = "GitHub", X = 65, Y = 40, Action = new ShortcutAction { Type = "website", Target = "https://github.com" } },
                new CanvasItem { Id = Guid.NewGuid().ToString(), Title = "Command Prompt", X = 50, Y = 65, Action = new ShortcutAction { Type = "app", Target = "cmd.exe" } },
            };
        }

        public void ResetLayoutToDefault()
        {
            ProfilesLayout = BuildDefaultProfilesLayout(null);
            SaveLayout();
        }
    }
}
