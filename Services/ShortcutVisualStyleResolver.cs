using System;
using System.IO;
using System.Windows.Media;
using HoldSpace.Models;

namespace HoldSpace.Services
{
    public class ShortcutVisualStyle
    {
        public string CategoryName { get; set; } = "App";
        public System.Windows.Media.Brush AccentBrush { get; set; } = System.Windows.Media.Brushes.Transparent;
        public System.Windows.Media.Brush BorderBrush { get; set; } = System.Windows.Media.Brushes.Transparent;
        public System.Windows.Media.Brush BackgroundBrush { get; set; } = System.Windows.Media.Brushes.Transparent;
        public System.Windows.Media.Brush IconWellBrush { get; set; } = System.Windows.Media.Brushes.Transparent;
        public System.Windows.Media.Brush BadgeTextBrush { get; set; } = System.Windows.Media.Brushes.Transparent;
        public System.Windows.Media.Brush HoverBackgroundBrush { get; set; } = System.Windows.Media.Brushes.Transparent;
        public System.Windows.Media.Brush HoverBorderBrush { get; set; } = System.Windows.Media.Brushes.Transparent;
        public bool IsDangerous { get; set; }
        public bool IsMissing { get; set; }
    }

    public static class ShortcutVisualStyleResolver
    {
        private static bool IsSystemCommand(string fileName)
        {
            string ext = Path.GetExtension(fileName).ToLowerInvariant();
            if (string.IsNullOrEmpty(ext) || ext == ".exe")
            {
                string name = Path.GetFileNameWithoutExtension(fileName).ToLowerInvariant();
                return name == "cmd" || name == "powershell" || name == "notepad" || name == "explorer" || name == "calc" || name == "taskmgr";
            }
            return false;
        }

        private static System.Windows.Media.Brush GetBrush(string hex)
        {
            try
            {
                var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
                var brush = new SolidColorBrush(color);
                brush.Freeze();
                return brush;
            }
            catch
            {
                return System.Windows.Media.Brushes.Transparent;
            }
        }

        public static ShortcutVisualStyle Resolve(CanvasItem item)
        {
            var style = new ShortcutVisualStyle();

            if (item == null || item.Action == null)
                return style;

            string type = (item.Action.Type ?? "").ToLowerInvariant();
            string target = (item.Action.Target ?? "").Trim();
            string expandedTarget = Environment.ExpandEnvironmentVariables(target);

            // 1. Check if missing/broken
            bool isMissing = false;
            if (type == "app")
            {
                if (!IsSystemCommand(expandedTarget) && !File.Exists(expandedTarget))
                    isMissing = true;
            }
            else if (type == "folder")
            {
                if (!Directory.Exists(expandedTarget))
                    isMissing = true;
            }
            else if (type == "file")
            {
                if (!File.Exists(expandedTarget))
                    isMissing = true;
            }

            // Uniform dark backgrounds/borders by default to prevent a "flashy" grid
            var defaultBg = GetBrush("#131315");
            var defaultBorder = GetBrush("#222225");
            var defaultIconWell = GetBrush("#1A1A1C");
            var defaultHoverBg = GetBrush("#1C1C1E");

            if (isMissing)
            {
                style.CategoryName = "Missing";
                style.AccentBrush = GetBrush("#FF453A"); // Red
                style.BorderBrush = GetBrush("#3D1F1F");
                style.BackgroundBrush = GetBrush("#181212");
                style.IconWellBrush = GetBrush("#261717");
                style.BadgeTextBrush = GetBrush("#FF453A");
                style.HoverBackgroundBrush = GetBrush("#241818");
                style.HoverBorderBrush = GetBrush("#FF453A");
                style.IsMissing = true;
                return style;
            }

            // 2. Check if dangerous/power action
            bool isPower = false;
            if (type == "systemaction" || type == "system_action")
            {
                string tLower = target.ToLowerInvariant();
                if (tLower == "shutdown" || tLower == "restart" || tLower == "reboot" || tLower == "signout" || tLower == "sleep" || tLower == "hibernate")
                {
                    isPower = true;
                }
            }

            if (isPower)
            {
                style.CategoryName = "Power";
                style.AccentBrush = GetBrush("#FF9500"); // Orange
                style.BorderBrush = GetBrush("#3D2A1F");
                style.BackgroundBrush = GetBrush("#181412");
                style.IconWellBrush = GetBrush("#261D17");
                style.BadgeTextBrush = GetBrush("#FF9500");
                style.HoverBackgroundBrush = GetBrush("#241E18");
                style.HoverBorderBrush = GetBrush("#FF9500");
                style.IsDangerous = true;
                return style;
            }

            // 3. Normal categories
            style.BackgroundBrush = defaultBg;
            style.BorderBrush = defaultBorder;
            style.IconWellBrush = defaultIconWell;
            style.HoverBackgroundBrush = defaultHoverBg;

            switch (type)
            {
                case "folder":
                    style.CategoryName = "Folder";
                    style.AccentBrush = GetBrush("#FFD60A"); // Yellow/Amber
                    style.BadgeTextBrush = GetBrush("#FFD60A");
                    style.HoverBorderBrush = GetBrush("#FFD60A");
                    break;

                case "website":
                    style.CategoryName = "Web";
                    style.AccentBrush = GetBrush("#30D158"); // Green
                    style.BadgeTextBrush = GetBrush("#30D158");
                    style.HoverBorderBrush = GetBrush("#30D158");
                    break;

                case "file":
                    style.CategoryName = "File";
                    style.AccentBrush = GetBrush("#BF5AF2"); // Purple
                    style.BadgeTextBrush = GetBrush("#BF5AF2");
                    style.HoverBorderBrush = GetBrush("#BF5AF2");
                    break;

                case "systemaction":
                case "system_action":
                    style.CategoryName = "System";
                    style.AccentBrush = GetBrush("#64D2FF"); // Cyan/Light Blue
                    style.BadgeTextBrush = GetBrush("#64D2FF");
                    style.HoverBorderBrush = GetBrush("#64D2FF");
                    break;

                case "app":
                default:
                    style.CategoryName = "App";
                    style.AccentBrush = GetBrush("#0A84FF"); // Blue
                    style.BadgeTextBrush = GetBrush("#0A84FF");
                    style.HoverBorderBrush = GetBrush("#0A84FF");
                    break;
            }

            return style;
        }
    }
}
