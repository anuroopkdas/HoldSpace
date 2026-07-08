using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HoldSpace.Models;

namespace HoldSpace.Services
{
    /// <summary>
    /// Provides named starter template definitions for new Modes.
    /// Safe: skips exe shortcuts if the file is not found on disk.
    /// </summary>
    public static class ModeTemplateService
    {
        public static readonly string[] TemplateNames =
            { "Empty", "Work", "Design", "Gaming", "Study", "Personal" };

        /// <summary>Returns preview label strings for a template (may include "(not installed)" notes).</summary>
        public static List<string> GetPreview(string templateName)
        {
            var items = BuildItems(templateName);
            return items.Count == 0
                ? new List<string> { "(no shortcuts – blank canvas)" }
                : items.Select(i => i.Title).ToList();
        }

        /// <summary>Builds a ready-to-use list of CanvasItems for the chosen template.</summary>
        public static List<CanvasItem> BuildItems(string templateName)
        {
            return templateName switch
            {
                "Work"     => BuildWork(),
                "Design"   => BuildDesign(),
                "Gaming"   => BuildGaming(),
                "Study"    => BuildStudy(),
                "Personal" => BuildPersonal(),
                _          => new List<CanvasItem>()   // Empty
            };
        }

        // ── Grid layout helpers ────────────────────────────────────────────

        private static List<CanvasItem> Grid(IEnumerable<(string title, string type, string target)> entries)
        {
            var result = new List<CanvasItem>();
            double[] cols = { 20, 40, 60, 80 };
            double[] rows = { 30, 55, 80 };
            int idx = 0;

            foreach (var (title, type, target) in entries)
            {
                // Skip exe shortcuts that don't exist on disk
                if (type == "app" && !target.Contains("://") && !File.Exists(target))
                {
                    System.Diagnostics.Debug.WriteLine($"[Template] Skipping missing app: {target}");
                    continue;
                }

                result.Add(new CanvasItem
                {
                    Id = Guid.NewGuid().ToString(),
                    Title = title,
                    X = cols[idx % cols.Length],
                    Y = rows[idx / cols.Length % rows.Length],
                    Action = new ShortcutAction { Type = type, Target = target }
                });
                idx++;
            }
            return result;
        }

        // ── Common paths ───────────────────────────────────────────────────

        private static string BrowserExe()
        {
            // Try common browsers in priority order
            string[] candidates =
            {
                @"C:\Program Files\Google\Chrome\Application\chrome.exe",
                @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
                @"C:\Program Files\Mozilla Firefox\firefox.exe",
                @"C:\Program Files (x86)\Mozilla Firefox\firefox.exe",
            };
            return candidates.FirstOrDefault(File.Exists) ?? "";
        }

        private static string BrowserTitle()
        {
            if (File.Exists(@"C:\Program Files\Google\Chrome\Application\chrome.exe")) return "Chrome";
            if (File.Exists(@"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe")) return "Chrome";
            if (File.Exists(@"C:\Program Files\Mozilla Firefox\firefox.exe")) return "Firefox";
            return "Browser";
        }

        private static (string, string, string) BrowserEntry() =>
            (BrowserTitle(), "app", BrowserExe());

        private static (string, string, string) Web(string title, string url) =>
            (title, "website", url);

        private static (string, string, string) App(string title, string path) =>
            (title, "app", path);

        // ── Templates ─────────────────────────────────────────────────────

        private static List<CanvasItem> BuildWork() => Grid(new[]
        {
            App("File Explorer", @"C:\Windows\explorer.exe"),
            ("Downloads", "folder", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")),
            ("Documents", "folder", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)),
            BrowserEntry(),
            App("Terminal", @"C:\Windows\System32\cmd.exe"),
        });

        private static List<CanvasItem> BuildDesign() => Grid(new[]
        {
            BrowserEntry(),
            ("Downloads", "folder", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")),
            ("Pictures",  "folder", Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)),
            Web("Figma",  "https://www.figma.com"),
            Web("Canva",  "https://www.canva.com"),
        });

        private static List<CanvasItem> BuildGaming() => Grid(new[]
        {
            App("Steam",   @"C:\Program Files (x86)\Steam\steam.exe"),
            App("Discord", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Discord\Update.exe")),
            Web("YouTube", "https://www.youtube.com"),
            ("Downloads", "folder", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")),
        });

        private static List<CanvasItem> BuildStudy() => Grid(new[]
        {
            BrowserEntry(),
            ("Documents", "folder", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)),
            Web("Google",   "https://www.google.com"),
            Web("YouTube",  "https://www.youtube.com"),
            Web("ChatGPT",  "https://chat.openai.com"),
        });

        private static List<CanvasItem> BuildPersonal() => Grid(new[]
        {
            BrowserEntry(),
            ("Downloads", "folder", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")),
            Web("YouTube", "https://www.youtube.com"),
            Web("ChatGPT", "https://chat.openai.com"),
        });
    }
}
