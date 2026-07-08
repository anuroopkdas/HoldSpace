using System;
using HoldSpace.ViewModels;

namespace HoldSpace.Models
{
    public class CanvasItem : ViewModelBase
    {
        private string _id = Guid.NewGuid().ToString();
        private string _title = "New Shortcut";
        private string _iconPath = string.Empty;
        private double _x = 50;
        private double _y = 50;
        private ShortcutAction _action = new ShortcutAction();

        private bool _isSelected;

        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        private System.Windows.Media.ImageSource? _resolvedIcon;

        [System.Text.Json.Serialization.JsonIgnore]
        public System.Windows.Media.ImageSource? ResolvedIcon
        {
            get => _resolvedIcon;
            set => SetProperty(ref _resolvedIcon, value);
        }

        [System.Text.Json.Serialization.JsonIgnore]
        public HoldSpace.Services.ShortcutVisualStyle VisualStyle => HoldSpace.Services.ShortcutVisualStyleResolver.Resolve(this);

        [System.Text.Json.Serialization.JsonIgnore]
        public string FirstLetter => !string.IsNullOrEmpty(Title) ? Title[0].ToString().ToUpper() : "?";

        [System.Text.Json.Serialization.JsonIgnore]
        public string DisplayIcon
        {
            get
            {
                if (Action == null || string.IsNullOrEmpty(Action.Type)) return "?";
                string type = Action.Type.ToLowerInvariant();
                string target = (Action.Target ?? "").ToLowerInvariant();

                switch (type)
                {
                    case "folder":
                        return "\uE838"; // Folder Glyph
                    case "file":
                        return "\uE7C3"; // Document Glyph
                    case "systemaction":
                    case "system_action":
                        if (target.Contains("settings")) return "\uE713";
                        if (target.Contains("taskmanager") || target.Contains("taskmgr")) return "\uE9F9";
                        if (target.Contains("terminal") || target.Contains("cmd")) return "\uE756";
                        if (target.Contains("lock")) return "\uE72E";
                        if (target.Contains("desktop")) return "\uE7C4";
                        return "\uE713";
                    case "website":
                        return "\uE774"; // Globe/Link
                    default:
                        return FirstLetter;
                }
            }
        }

        [System.Text.Json.Serialization.JsonIgnore]
        public string FallbackText
        {
            get
            {
                if (Action == null || string.IsNullOrEmpty(Action.Type)) return FirstLetter;
                string type = Action.Type.ToLowerInvariant();

                if (type == "folder" || type == "file" || type == "systemaction" || type == "system_action")
                {
                    return DisplayIcon;
                }

                if (type == "website" || type == "app")
                {
                    if (Title.Equals("ChatGPT", StringComparison.OrdinalIgnoreCase)) return "CG";
                    if (Title.Equals("GitHub", StringComparison.OrdinalIgnoreCase)) return "GH";
                    if (Title.Equals("YouTube", StringComparison.OrdinalIgnoreCase)) return "YT";

                    var parts = Title.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        return (parts[0][0].ToString() + parts[1][0].ToString()).ToUpper();
                    }
                }

                return FirstLetter;
            }
        }

        public void NotifyIconChanged()
        {
            OnPropertyChanged(nameof(DisplayIcon));
            OnPropertyChanged(nameof(FallbackText));
            OnPropertyChanged(nameof(VisualStyle));
        }

        public string Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        public string Title
        {
            get => _title;
            set
            {
                if (SetProperty(ref _title, value))
                {
                    OnPropertyChanged(nameof(FirstLetter));
                    OnPropertyChanged(nameof(DisplayIcon));
                    OnPropertyChanged(nameof(FallbackText));
                    RefreshIconAsync();
                }
            }
        }

        public string IconPath
        {
            get => _iconPath;
            set => SetProperty(ref _iconPath, value);
        }

        public double X
        {
            get => _x;
            set => SetProperty(ref _x, value);
        }

        public double Y
        {
            get => _y;
            set => SetProperty(ref _y, value);
        }

        public ShortcutAction Action
        {
            get => _action;
            set => SetProperty(ref _action, value);
        }

        public async void RefreshIconAsync()
        {
            try
            {
                var icon = await HoldSpace.Services.IconService.GetIconForShortcutAsync(this);
                if (icon != null)
                {
                    ResolvedIcon = icon;
                }
                else
                {
                    ResolvedIcon = null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to async refresh icon: {ex.Message}");
            }
        }
    }
}
