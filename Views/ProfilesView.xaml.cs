using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using HoldSpace.Models;
using HoldSpace.Services;
using Button = System.Windows.Controls.Button;
using TextBox = System.Windows.Controls.TextBox;
using MessageBox = System.Windows.MessageBox;
using Application = System.Windows.Application;

namespace HoldSpace.Views
{
    public partial class ProfilesView : System.Windows.Controls.UserControl
    {
        private SettingsService? _settingsService;

        public ProfilesView() { InitializeComponent(); }

        public void Initialize(SettingsService svc)
        {
            _settingsService = svc;
            Refresh();
        }

        public void Refresh()
        {
            if (_settingsService == null) return;
            ProfilesList.ItemsSource = null;
            ProfilesList.ItemsSource = _settingsService.ProfilesLayout.Profiles;

            // Update badge and count labels for each rendered container
            ProfilesList.UpdateLayout();
            foreach (var profile in _settingsService.ProfilesLayout.Profiles)
            {
                var container = ProfilesList.ItemContainerGenerator.ContainerFromItem(profile) as FrameworkElement;
                if (container == null) continue;

                var badge = FindChild<Border>(container, "ActiveBadge");
                var countLabel = FindChild<TextBlock>(container, "ShortcutCount");
                var setActiveBtn = FindChild<Button>(container, "BtnSetActive");

                bool isActive = profile.Id == _settingsService.ProfilesLayout.ActiveProfileId;
                if (badge != null) badge.Visibility = isActive ? Visibility.Visible : Visibility.Collapsed;
                if (setActiveBtn != null) setActiveBtn.IsEnabled = !isActive;
                if (countLabel != null)
                {
                    int count = profile.Items?.Count ?? 0;
                    string updated = profile.UpdatedAt.ToLocalTime().ToString("MMM d, yyyy");
                    countLabel.Text = $"{count} shortcut{(count != 1 ? "s" : "")}  ·  Updated {updated}";
                }
            }
        }

        private void BtnCreate_Click(object sender, RoutedEventArgs e)
        {
            if (_settingsService == null) return;

            var dialog = new CreateModeDialog { Owner = Window.GetWindow(this) };
            if (dialog.ShowDialog() != true) return;

            var items = Services.ModeTemplateService.BuildItems(dialog.TemplateName);
            _settingsService.CreateProfile(dialog.ModeName, items);
            RefreshAll();
        }

        private void BtnSetActive_Click(object sender, RoutedEventArgs e)
        {
            string? id = (sender as Button)?.Tag?.ToString();
            if (id == null || _settingsService == null) return;
            _settingsService.SetActiveProfile(id);
            RefreshAll();
        }

        private void BtnRename_Click(object sender, RoutedEventArgs e)
        {
            string? id = (sender as Button)?.Tag?.ToString();
            if (id == null || _settingsService == null) return;
            var p = _settingsService.ProfilesLayout.Profiles.FirstOrDefault(x => x.Id == id);
            if (p == null) return;
            string name = PromptName("Rename Mode", "New Name:", p.Name);
            if (!string.IsNullOrWhiteSpace(name))
            {
                _settingsService.RenameProfile(id, name);
                RefreshAll();
            }
        }

        private void BtnDuplicate_Click(object sender, RoutedEventArgs e)
        {
            string? id = (sender as Button)?.Tag?.ToString();
            if (id == null || _settingsService == null) return;
            _settingsService.DuplicateProfile(id);
            RefreshAll();
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            string? id = (sender as Button)?.Tag?.ToString();
            if (id == null || _settingsService == null) return;

            if (_settingsService.ProfilesLayout.Profiles.Count <= 1)
            {
                MessageBox.Show("Cannot delete the last mode.", "HoldSpace", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var p = _settingsService.ProfilesLayout.Profiles.FirstOrDefault(x => x.Id == id);
            string modeName = p?.Name ?? "this mode";

            var result = MessageBox.Show(
                $"This will remove \"{modeName}\" and its shortcuts. This cannot be undone.",
                "Delete Mode?", MessageBoxButton.OKCancel, MessageBoxImage.Warning);

            if (result == MessageBoxResult.OK)
            {
                _settingsService.DeleteProfile(id);
                RefreshAll();
            }
        }

        private void RefreshAll()
        {
            Refresh();
            var mw = Window.GetWindow(this) as MainWindow;
            mw?.OnProfileChanged();
        }

        private static string PromptName(string title, string label, string defaultValue)
        {
            var dialog = new Window
            {
                Title = title,
                Width = 340, Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                Background = System.Windows.Media.Brushes.Black
            };

            var stack = new StackPanel { Margin = new Thickness(20) };
            var lbl = new TextBlock { Text = label, Foreground = System.Windows.Media.Brushes.White, Margin = new Thickness(0, 0, 0, 8) };
            var txt = new TextBox { Text = defaultValue, SelectionStart = 0, SelectionLength = defaultValue.Length };
            var btn = new Button { Content = "OK", Margin = new Thickness(0, 12, 0, 0), IsDefault = true };

            string result = string.Empty;
            btn.Click += (s, e) => { result = txt.Text; dialog.DialogResult = true; };

            stack.Children.Add(lbl);
            stack.Children.Add(txt);
            stack.Children.Add(btn);
            dialog.Content = stack;

            if (Application.Current.MainWindow != null)
                dialog.Owner = Application.Current.MainWindow;

            dialog.ShowDialog();
            return result;
        }

        private static T? FindChild<T>(DependencyObject parent, string name) where T : FrameworkElement
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T fe && fe.Name == name) return fe;
                var result = FindChild<T>(child, name);
                if (result != null) return result;
            }
            return null;
        }
    }
}
