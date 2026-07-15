using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HoldSpace.Models;

namespace HoldSpace.Services
{
    public static class IconService
    {
        private static readonly ConcurrentDictionary<string, ImageSource> _iconCache = new ConcurrentDictionary<string, ImageSource>();

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        private const uint SHGFI_ICON = 0x000000100;
        private const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;
        private const uint SHGFI_LARGEICON = 0x000000000;

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        private static string GetCacheDirectory(string subFolder)
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string baseDir = Path.Combine(appData, "HoldSpace", "IconCache", subFolder);
            if (!Directory.Exists(baseDir))
            {
                Directory.CreateDirectory(baseDir);
            }
            return baseDir;
        }

        public static ImageSource? GetIconForShortcut(CanvasItem item)
        {
            if (item?.Action == null) return null;

            string key = item.Id;
            if (_iconCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            // Attempt to resolve icon
            ImageSource? resolved = ResolveIcon(item);
            if (resolved != null)
            {
                _iconCache[key] = resolved;
            }
            return resolved;
        }

        public static async Task<ImageSource?> GetIconForShortcutAsync(CanvasItem item)
        {
            if (item?.Action == null) return null;

            string key = item.Id;
            if (_iconCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            ImageSource? resolved = null;

            if (!string.IsNullOrEmpty(item.IconPath))
            {
                resolved = ResolveIcon(item);
            }
            else if (item.Action.Type.Equals("website", StringComparison.OrdinalIgnoreCase))
            {
                resolved = await ResolveWebsiteIconAsync(item.Action.Target);
            }
            else
            {
                resolved = ResolveIcon(item);
            }

            if (resolved != null)
            {
                _iconCache[key] = resolved;
            }
            return resolved;
        }

        public static void ClearCache()
        {
            _iconCache.Clear();
        }

        public static void EvictFromCache(string key)
        {
            _iconCache.TryRemove(key, out _);
        }

        private static string GetMd5Hash(string input)
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                byte[] inputBytes = System.Text.Encoding.UTF8.GetBytes(input);
                byte[] hashBytes = md5.ComputeHash(inputBytes);
                return Convert.ToHexString(hashBytes);
            }
        }

        private static string GetExecutablePathOnly(string target)
        {
            if (string.IsNullOrEmpty(target)) return string.Empty;
            target = target.Trim();

            if (target.StartsWith("\""))
            {
                int nextQuote = target.IndexOf("\"", 1);
                if (nextQuote > 1)
                {
                    return target.Substring(1, nextQuote - 1).Trim();
                }
            }

            int exeIndex = target.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            if (exeIndex > 0)
            {
                return target.Substring(0, exeIndex + 4).Trim(' ', '"');
            }

            int spaceIndex = target.IndexOf(' ');
            if (spaceIndex > 0)
            {
                return target.Substring(0, spaceIndex).Trim(' ', '"');
            }

            return target.Trim(' ', '"');
        }

        private static string? ResolveFullExePath(string exePath)
        {
            if (File.Exists(exePath)) return exePath;

            var paths = Environment.GetEnvironmentVariable("PATH")?.Split(';');
            if (paths != null)
            {
                foreach (var p in paths)
                {
                    try
                    {
                        string fullPath = Path.Combine(p.Trim(), exePath);
                        if (File.Exists(fullPath)) return fullPath;
                    }
                    catch { }
                }
            }

            string[] commonPaths = {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), exePath),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), exePath),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.SystemX86), exePath)
            };
            foreach (var cp in commonPaths)
            {
                if (File.Exists(cp)) return cp;
            }

            return null;
        }

        private static ImageSource? ExtractIconFromPath(string path)
        {
            try
            {
                path = GetExecutablePathOnly(path);
                if (string.IsNullOrEmpty(path)) return null;

                if (!Path.IsPathRooted(path))
                {
                    string? resolved = ResolveFullExePath(path);
                    if (resolved != null) path = resolved;
                }

                if (!File.Exists(path) && !Directory.Exists(path))
                {
                    return null;
                }

                string cacheName = GetMd5Hash(path) + ".png";
                string cachePath = Path.Combine(GetCacheDirectory("Extracted"), cacheName);

                if (File.Exists(cachePath))
                {
                    return LoadImageFromFile(cachePath);
                }

                SHFILEINFO shinfo = new SHFILEINFO();
                IntPtr hSuccess = SHGetFileInfo(
                    path,
                    0,
                    ref shinfo,
                    (uint)Marshal.SizeOf(shinfo),
                    SHGFI_ICON | SHGFI_LARGEICON);

                if (shinfo.hIcon != IntPtr.Zero)
                {
                    using (var icon = System.Drawing.Icon.FromHandle(shinfo.hIcon))
                    {
                        using (var bitmap = icon.ToBitmap())
                        {
                            bitmap.Save(cachePath, System.Drawing.Imaging.ImageFormat.Png);
                        }
                    }
                    DestroyIcon(shinfo.hIcon);
                    return LoadImageFromFile(cachePath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to extract icon for {path}: {ex.Message}");
            }
            return null;
        }

        private static ImageSource? ResolveIcon(CanvasItem item)
        {
            if (!string.IsNullOrEmpty(item.IconPath))
            {
                try
                {
                    if (File.Exists(item.IconPath))
                    {
                        var customImg = LoadImageFromFile(item.IconPath);
                        if (customImg != null) return customImg;
                    }
                }
                catch { }
            }

            string type = item.Action.Type.ToLowerInvariant();
            string target = item.Action.Target ?? "";

            try
            {
                switch (type)
                {
                    case "app":
                        return ExtractIconFromPath(target);
                    case "folder":
                        var folderIcon = ExtractIconFromPath(target);
                        return folderIcon ?? ExtractFolderIcon();
                    case "file":
                        return ExtractIconFromPath(target);
                    case "website":
                        // Synchronous check of local cache first
                        string domain = GetDomainFromUrl(target);
                        if (!string.IsNullOrEmpty(domain))
                        {
                            string cachePath = Path.Combine(GetCacheDirectory("Websites"), domain + ".png");
                            if (File.Exists(cachePath))
                            {
                                return LoadImageFromFile(cachePath);
                            }
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to resolve icon: {ex.Message}");
            }

            return null;
        }

        private static ImageSource? ExtractFolderIcon()
        {
            try
            {
                string cachePath = Path.Combine(GetCacheDirectory("Folders"), "folder_default.png");
                if (File.Exists(cachePath))
                {
                    return LoadImageFromFile(cachePath);
                }

                SHFILEINFO shinfo = new SHFILEINFO();
                IntPtr hSuccess = SHGetFileInfo(
                    Environment.GetFolderPath(Environment.SpecialFolder.System),
                    0x00000010, // FILE_ATTRIBUTE_DIRECTORY
                    ref shinfo,
                    (uint)Marshal.SizeOf(shinfo),
                    SHGFI_ICON | SHGFI_LARGEICON | SHGFI_USEFILEATTRIBUTES);

                if (shinfo.hIcon != IntPtr.Zero)
                {
                    using (var icon = System.Drawing.Icon.FromHandle(shinfo.hIcon))
                    {
                        using (var bitmap = icon.ToBitmap())
                        {
                            bitmap.Save(cachePath, System.Drawing.Imaging.ImageFormat.Png);
                        }
                    }
                    DestroyIcon(shinfo.hIcon);
                    return LoadImageFromFile(cachePath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to extract folder icon: {ex.Message}");
            }
            return null;
        }

        private static ImageSource? ExtractFileIcon(string filePath)
        {
            try
            {
                string ext = Path.GetExtension(filePath).ToLowerInvariant();
                if (string.IsNullOrEmpty(ext)) ext = ".txt";

                string cacheName = ext.Replace(".", "_") + ".png";
                string cachePath = Path.Combine(GetCacheDirectory("Files"), cacheName);

                if (File.Exists(cachePath))
                {
                    return LoadImageFromFile(cachePath);
                }

                SHFILEINFO shinfo = new SHFILEINFO();
                IntPtr hSuccess = SHGetFileInfo(
                    ext,
                    0x00000080, // FILE_ATTRIBUTE_NORMAL
                    ref shinfo,
                    (uint)Marshal.SizeOf(shinfo),
                    SHGFI_ICON | SHGFI_LARGEICON | SHGFI_USEFILEATTRIBUTES);

                if (shinfo.hIcon != IntPtr.Zero)
                {
                    using (var icon = System.Drawing.Icon.FromHandle(shinfo.hIcon))
                    {
                        using (var bitmap = icon.ToBitmap())
                        {
                            bitmap.Save(cachePath, System.Drawing.Imaging.ImageFormat.Png);
                        }
                    }
                    DestroyIcon(shinfo.hIcon);
                    return LoadImageFromFile(cachePath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to extract file icon: {ex.Message}");
            }
            return null;
        }

        private static async Task<ImageSource?> ResolveWebsiteIconAsync(string url)
        {
            try
            {
                string domain = GetDomainFromUrl(url);
                if (string.IsNullOrEmpty(domain)) return null;

                string cacheName = domain + ".png";
                string cachePath = Path.Combine(GetCacheDirectory("Websites"), cacheName);

                if (File.Exists(cachePath))
                {
                    return LoadImageFromFile(cachePath);
                }

                // Download in background
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(5);
                    byte[] data = await client.GetByteArrayAsync($"https://www.google.com/s2/favicons?sz=64&domain={domain}");
                    if (data != null && data.Length > 0)
                    {
                        await File.WriteAllBytesAsync(cachePath, data);
                        return LoadImageFromFile(cachePath);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to download website icon: {ex.Message}");
            }
            return null;
        }

        private static string GetDomainFromUrl(string url)
        {
            try
            {
                if (!url.Contains("://"))
                {
                    url = "https://" + url;
                }
                var uri = new Uri(url);
                return uri.Host;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static ImageSource? LoadImageFromFile(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(path);
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load bitmap image from {path}: {ex.Message}");
                return null;
            }
        }
    }
}
