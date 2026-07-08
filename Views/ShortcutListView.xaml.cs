using System.Windows;
using System.Windows.Controls;
using HoldSpace.Services;

namespace HoldSpace.Views
{
    public partial class ShortcutListView : System.Windows.Controls.UserControl
    {
        public ShortcutListView()
        {
            InitializeComponent();
        }

        public void Initialize(SettingsService settingsService)
        {
            var items = settingsService.CurrentLayout.Items;
            GridShortcuts.ItemsSource = items;

            if (items == null || items.Count == 0)
            {
                GridBorder.Visibility = Visibility.Collapsed;
                EmptyStateText.Visibility = Visibility.Visible;
            }
            else
            {
                GridBorder.Visibility = Visibility.Visible;
                EmptyStateText.Visibility = Visibility.Collapsed;
            }
        }

        private void BtnGoToCanvas_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Window.GetWindow(this) as MainWindow;
            if (mainWindow != null)
            {
                mainWindow.SidebarMenu.SelectedIndex = 1; // Canvas Editor index
            }
        }
    }
}
