using System;
using System.Windows;
using System.Windows.Controls;
using HoldSpace.Services;

namespace HoldSpace.Views
{
    public partial class OverviewView : System.Windows.Controls.UserControl
    {
        private SettingsService? _settingsService;

        public OverviewView()
        {
            InitializeComponent();
        }

        public void Initialize(SettingsService settingsService)
        {
            _settingsService = settingsService;
            RefreshMetrics();
        }

        public void RefreshMetrics()
        {
            if (_settingsService != null)
            {
                var settings = _settingsService.CurrentSettings;
                var layout = _settingsService.CurrentLayout;

                TxtTriggerKey.Text = settings.Trigger.KeyName;
                TxtTriggerDelay.Text = $"{settings.HoldDelayMs}ms hold threshold";
                TxtShortcutCount.Text = layout.Items.Count.ToString();
                TxtStartupStatus.Text = StartupService.IsStartupEnabled() ? "Enabled" : "Disabled";
                TxtDimStatus.Text = settings.BackgroundDim ? "Enabled" : "Disabled";
            }
        }

        private void BtnTestOverlay_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Trigger overlay test using App public method
                var app = System.Windows.Application.Current as App;
                app?.ShowOverlayTest();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to test overlay: {ex.Message}");
            }
        }

        private void BtnEditCanvas_Click(object sender, RoutedEventArgs e)
        {
            NavigateToTab(1); // Canvas Editor is at Index 1
        }

        private void BtnAddShortcut_Click(object sender, RoutedEventArgs e)
        {
            NavigateToTab(1); // Canvas Editor
            var mainWindow = Window.GetWindow(this) as MainWindow;
            mainWindow?.CanvasEditor.TriggerAddShortcut();
        }

        private void BtnChangeTrigger_Click(object sender, RoutedEventArgs e)
        {
            NavigateToTab(3); // Trigger Settings is at Index 3
        }

        private void NavigateToTab(int index)
        {
            var mainWindow = Window.GetWindow(this) as MainWindow;
            if (mainWindow != null)
            {
                mainWindow.SidebarMenu.SelectedIndex = index;
            }
        }

        private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateResponsiveLayout(e.NewSize.Width);
        }

        private void UpdateResponsiveLayout(double width)
        {
            if (CardsGrid == null || CardStatus == null || CardMetrics == null || CardActions == null) return;

            CardsGrid.ColumnDefinitions.Clear();
            CardsGrid.RowDefinitions.Clear();

            if (width > 880)
            {
                // Wide window: 3 columns
                CardsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                CardsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                CardsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.3, GridUnitType.Star) });

                CardsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                Grid.SetColumn(CardStatus, 0);
                Grid.SetRow(CardStatus, 0);
                Grid.SetColumnSpan(CardStatus, 1);

                Grid.SetColumn(CardMetrics, 1);
                Grid.SetRow(CardMetrics, 0);
                Grid.SetColumnSpan(CardMetrics, 1);

                Grid.SetColumn(CardActions, 2);
                Grid.SetRow(CardActions, 0);
                Grid.SetColumnSpan(CardActions, 1);
            }
            else if (width > 580)
            {
                // Medium window: 2 columns
                CardsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                CardsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                CardsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                CardsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                Grid.SetColumn(CardStatus, 0);
                Grid.SetRow(CardStatus, 0);
                Grid.SetColumnSpan(CardStatus, 1);

                Grid.SetColumn(CardMetrics, 1);
                Grid.SetRow(CardMetrics, 0);
                Grid.SetColumnSpan(CardMetrics, 1);

                Grid.SetColumn(CardActions, 0);
                Grid.SetRow(CardActions, 1);
                Grid.SetColumnSpan(CardActions, 2);
            }
            else
            {
                // Narrow window: 1 column
                CardsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                CardsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                CardsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                CardsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                Grid.SetColumn(CardStatus, 0);
                Grid.SetRow(CardStatus, 0);
                Grid.SetColumnSpan(CardStatus, 1);

                Grid.SetColumn(CardMetrics, 0);
                Grid.SetRow(CardMetrics, 1);
                Grid.SetColumnSpan(CardMetrics, 1);

                Grid.SetColumn(CardActions, 0);
                Grid.SetRow(CardActions, 2);
                Grid.SetColumnSpan(CardActions, 1);
            }
        }
    }
}
