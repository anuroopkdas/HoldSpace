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

            if (item.Action.Type.Equals("website", StringComparison.OrdinalIgnoreCase))
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

        private static ImageSource? ResolveIcon(CanvasItem item)
        {
            string type = item.Action.Type.ToLowerInvariant();
            string target = item.Action.Target ?? "";

            try
            {
                switch (type)
                {
                    case "app":
                        return ExtractExeIcon(target);
                    case "folder":
                        return ExtractFolderIcon();
                    case "file":
                        return ExtractFileIcon(target);
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

        private static ImageSource? ExtractExeIcon(string exePath)
        {
            try
            {
                // Clean quotes or target parameters if any
                exePath = exePath.Trim('\"', ' ');

                if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                {
                    return null;
                }

                string cacheName = Path.GetFileName(exePath) + ".png";
                string cachePath = Path.Combine(GetCacheDirectory("Apps"), cacheName);

                if (File.Exists(cachePath))
                {
                    return LoadImageFromFile(cachePath);
                }

                using (var icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath))
                {
                    if (icon != null)
                    {
                        using (var bitmap = icon.ToBitmap())
                        {
                            bitmap.Save(cachePath, System.Drawing.Imaging.ImageFormat.Png);
                        }
                        return LoadImageFromFile(cachePath);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to extract exe icon for {exePath}: {ex.Message}");
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
