using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HoldSpace.Models;

namespace HoldSpace
{
    public partial class AddShortcutWindow : Window
    {
        public CanvasItem? OutputItem { get; private set; }

        public AddShortcutWindow()
        {
            InitializeComponent();
            ListTypes.SelectedIndex = 0; // Default to App
            ValidateInputs(this, null);
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        private void ListTypes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ValidateInputs(sender, null);
        }

        private void ValidateInputs(object sender, TextChangedEventArgs? e)
        {
            if (BtnAddShortcut == null || TxtError == null) return;

            int index = ListTypes.SelectedIndex;
            bool isValid = false;
            string errorMsg = "";

            switch (index)
            {
                case 0: // App
                    string appPath = TxtAppPath.Text.Trim();
                    string appName = TxtAppName.Text.Trim();
                    if (string.IsNullOrEmpty(appPath))
                    {
                        errorMsg = "Choose an app executable path to continue.";
                    }
                    else if (string.IsNullOrEmpty(appName))
                    {
                        errorMsg = "Enter a name for the app shortcut.";
                    }
                    else if (!File.Exists(appPath) && !IsSystemCommand(appPath))
                    {
                        errorMsg = "This app file path does not exist.";
                    }
                    else
                    {
                        isValid = true;
                    }
                    break;

                case 1: // Folder
                    string folderPath = TxtFolderPath.Text.Trim();
                    string folderName = TxtFolderName.Text.Trim();
                    if (string.IsNullOrEmpty(folderPath))
                    {
                        errorMsg = "Choose a folder path to continue.";
                    }
                    else if (string.IsNullOrEmpty(folderName))
                    {
                        errorMsg = "Enter a name for the folder shortcut.";
                    }
                    else if (!Directory.Exists(folderPath))
                    {
                        errorMsg = "This folder path does not exist.";
                    }
                    else
                    {
                        isValid = true;
                    }
                    break;

                case 2: // File
                    string filePath = TxtFilePath.Text.Trim();
                    string fileName = TxtFileName.Text.Trim();
                    if (string.IsNullOrEmpty(filePath))
                    {
                        errorMsg = "Choose a file path to continue.";
                    }
                    else if (string.IsNullOrEmpty(fileName))
                    {
                        errorMsg = "Enter a name for the file shortcut.";
                    }
                    else if (!File.Exists(filePath))
                    {
                        errorMsg = "This file path does not exist.";
                    }
                    else
                    {
                        isValid = true;
                    }
                    break;

                case 3: // Website
                    string webUrl = TxtWebUrl.Text.Trim();
                    string webName = TxtWebName.Text.Trim();
                    if (string.IsNullOrEmpty(webUrl))
                    {
                        errorMsg = "Enter a valid website URL.";
                    }
                    else if (!IsValidUrl(webUrl))
                    {
                        errorMsg = "Enter a valid website URL or domain.";
                    }
                    else
                    {
                        isValid = true;
                    }
                    break;

                case 4: // System Action
                    string sysName = TxtSysName.Text.Trim();
                    if (ComboSysAction.SelectedIndex < 0)
                    {
                        errorMsg = "Select a system action.";
                    }
                    else if (string.IsNullOrEmpty(sysName))
                    {
                        errorMsg = "Enter a name for the system action shortcut.";
                    }
                    else
                    {
                        isValid = true;
                    }
                    break;
            }

            BtnAddShortcut.IsEnabled = isValid;
            if (isValid)
            {
                TxtError.Visibility = Visibility.Collapsed;
            }
            else
            {
                TxtError.Text = errorMsg;
                TxtError.Visibility = Visibility.Visible;
            }
        }

        private bool IsSystemCommand(string fileName)
        {
            string ext = Path.GetExtension(fileName).ToLowerInvariant();
            if (string.IsNullOrEmpty(ext) || ext == ".exe")
            {
                string name = Path.GetFileNameWithoutExtension(fileName).ToLowerInvariant();
                return name == "cmd" || name == "powershell" || name == "notepad" || name == "explorer" || name == "calc" || name == "taskmgr";
            }
            return false;
        }

        private bool IsValidUrl(string url)
        {
            string testUrl = url;
            if (!testUrl.Contains("://"))
            {
                testUrl = "https://" + testUrl;
            }
            return Uri.TryCreate(testUrl, UriKind.Absolute, out Uri? uri) && 
                   (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) && 
                   uri.Host.Contains(".");
        }

        private void TxtAppPath_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(TxtAppName.Text))
            {
                try
                {
                    string path = TxtAppPath.Text;
                    if (!string.IsNullOrEmpty(path))
                    {
                        TxtAppName.Text = Path.GetFileNameWithoutExtension(path);
                    }
                }
                catch { }
            }
            ValidateInputs(sender, e);
        }

        private void TxtFolderPath_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(TxtFolderName.Text))
            {
                try
                {
                    string path = TxtFolderPath.Text;
                    if (!string.IsNullOrEmpty(path))
                    {
                        TxtFolderName.Text = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar));
                    }
                }
                catch { }
            }
            ValidateInputs(sender, e);
        }

        private void TxtFilePath_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(TxtFileName.Text))
            {
                try
                {
                    string path = TxtFilePath.Text;
                    if (!string.IsNullOrEmpty(path))
                    {
                        TxtFileName.Text = Path.GetFileNameWithoutExtension(path);
                    }
                }
                catch { }
            }
            ValidateInputs(sender, e);
        }

        private void TxtWebUrl_TextChanged(object sender, TextChangedEventArgs e)
        {
            string url = TxtWebUrl.Text;
            if (string.IsNullOrEmpty(TxtWebName.Text) || TxtWebName.Tag != null)
            {
                string domain = ExtractDomainName(url);
                if (!string.IsNullOrEmpty(domain))
                {
                    TxtWebName.Text = domain;
                    TxtWebName.Tag = "auto"; // Mark as autocompleted
                }
            }
            ValidateInputs(sender, e);
        }

        private string ExtractDomainName(string url)
        {
            try
            {
                string cleaned = url.Trim();
                if (string.IsNullOrEmpty(cleaned)) return "";
                if (!cleaned.Contains("://"))
                {
                    cleaned = "https://" + cleaned;
                }
                if (Uri.TryCreate(cleaned, UriKind.Absolute, out Uri? uri))
                {
                    string host = uri.Host;
                    if (host.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
                    {
                        host = host.Substring(4);
                    }
                    int dotIdx = host.IndexOf('.');
                    if (dotIdx > 0)
                    {
                        string name = host.Substring(0, dotIdx);
                        // Map known sites to capitalized CamelCase format
                        string nameLower = name.ToLowerInvariant();
                        if (nameLower == "chatgpt") return "ChatGPT";
                        if (nameLower == "github") return "GitHub";
                        if (nameLower == "youtube") return "YouTube";
                        if (nameLower == "google") return "Google";
                        
                        if (name.Length > 0)
                        {
                            return char.ToUpper(name[0]) + name.Substring(1);
                        }
                    }
                }
            }
            catch { }
            return "";
        }

        private void ComboSysAction_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int index = ComboSysAction.SelectedIndex;
            string name = "";
            switch (index)
            {
                case 0:
                    name = "Settings";
                    break;
                case 1:
                    name = "Task Manager";
                    break;
                case 2:
                    name = "Terminal";
                    break;
                case 3:
                    name = "Lock Screen";
                    break;
                case 4:
                    name = "Show Desktop";
                    break;
                case 5:
                    name = "Sleep PC";
                    break;
                case 6:
                    name = "Hibernate PC";
                    break;
                case 7:
                    name = "Shutdown PC";
                    break;
                case 8:
                    name = "Restart PC";
                    break;
                case 9:
                    name = "Sign Out";
                    break;
            }
            TxtSysName.Text = name;
            ValidateInputs(sender, null);
        }

        private void BtnBrowseApp_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Executable Files (*.exe)|*.exe|All Files (*.*)|*.*",
                Title = "Select Application Exe"
            };
            if (ofd.ShowDialog() == true)
            {
                TxtAppPath.Text = ofd.FileName;
            }
        }

        private void BtnBrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            using (var fbd = new System.Windows.Forms.FolderBrowserDialog())
            {
                fbd.Description = "Select Shortcut Folder";
                if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    TxtFolderPath.Text = fbd.SelectedPath;
                }
            }
        }

        private void BtnBrowseFile_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "All Files (*.*)|*.*",
                Title = "Select File"
            };
            if (ofd.ShowDialog() == true)
            {
                TxtFilePath.Text = ofd.FileName;
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnAddShortcut_Click(object sender, RoutedEventArgs e)
        {
            // Lock double clicks
            BtnAddShortcut.IsEnabled = false;

            int index = ListTypes.SelectedIndex;
            string title = "";
            string target = "";
            string arguments = "";
            string type = "";

            switch (index)
            {
                case 0: // App
                    title = TxtAppName.Text.Trim();
                    target = TxtAppPath.Text.Trim();
                    arguments = TxtAppArguments.Text.Trim();
                    type = "app";
                    break;

                case 1: // Folder
                    title = TxtFolderName.Text.Trim();
                    target = TxtFolderPath.Text.Trim();
                    type = "folder";
                    break;

                case 2: // File
                    title = TxtFileName.Text.Trim();
                    target = TxtFilePath.Text.Trim();
                    type = "file";
                    break;

                case 3: // Website
                    title = TxtWebName.Text.Trim();
                    target = TxtWebUrl.Text.Trim();
                    if (!target.Contains("://"))
                    {
                        target = "https://" + target;
                    }
                    if (string.IsNullOrEmpty(title))
                    {
                        title = ExtractDomainName(target);
                        if (string.IsNullOrEmpty(title)) title = "Website";
                    }
                    type = "website";
                    break;

                case 4: // System Action
                    title = TxtSysName.Text.Trim();
                    type = "systemAction";
                    int sysIndex = ComboSysAction.SelectedIndex;
                    switch (sysIndex)
                    {
                        case 0:
                            target = "settings";
                            break;
                        case 1:
                            target = "taskmanager";
                            break;
                        case 2:
                            target = "terminal";
                            break;
                        case 3:
                            target = "lock";
                            break;
                        case 4:
                            target = "desktop";
                            break;
                        case 5:
                            target = "sleep";
                            break;
                        case 6:
                            target = "hibernate";
                            break;
                        case 7:
                            target = "shutdown";
                            break;
                        case 8:
                            target = "restart";
                            break;
                        case 9:
                            target = "signout";
                            break;
                    }
                    break;
            }

            OutputItem = new CanvasItem
            {
                Title = title,
                Action = new ShortcutAction
                {
                    Type = type,
                    Target = target,
                    Arguments = arguments
                }
            };

            DialogResult = true;
            Close();
        }
    }
}
