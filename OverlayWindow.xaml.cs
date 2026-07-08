using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using HoldSpace.Models;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace HoldSpace
{
    public partial class OverlayWindow : Window
    {
        private readonly AppSettings _settings;
        public CanvasItem? HoveredItem { get; private set; }

        public OverlayWindow(CanvasLayout layout, AppSettings settings, bool isTestMode = false)
        {
            InitializeComponent();
            _settings = settings;
            HoveredItemText.Text = "None";

            if (isTestMode)
            {
                var testLayout = new CanvasLayout();
                testLayout.Items.Add(new CanvasItem
                {
                    Id = Guid.NewGuid().ToString(),
                    Title = "Browser",
                    X = 35,
                    Y = 50,
                    Action = new ShortcutAction { Type = "website", Target = "https://www.google.com" }
                });
                testLayout.Items.Add(new CanvasItem
                {
                    Id = Guid.NewGuid().ToString(),
                    Title = "Folder",
                    X = 50,
                    Y = 50,
                    Action = new ShortcutAction { Type = "folder", Target = "C:\\" }
                });
                testLayout.Items.Add(new CanvasItem
                {
                    Id = Guid.NewGuid().ToString(),
                    Title = "Settings",
                    X = 65,
                    Y = 50,
                    Action = new ShortcutAction { Type = "systemAction", Target = "settings" }
                });

                ShortcutsItemsControl.ItemsSource = testLayout.Items;
            }
            else
            {
                ShortcutsItemsControl.ItemsSource = layout.Items;
            }

            // Set initial opacity for fade-in
            Opacity = 0.0;
            this.Loaded += (s, e) =>
            {
                var fadeAnimation = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(settings.AnimationDurationMs));
                this.BeginAnimation(Window.OpacityProperty, fadeAnimation);
            };

            // Configure transparent vs dim background based on settings
            if (settings.BackgroundDim)
            {
                Background = new SolidColorBrush(Color.FromArgb(
                    (byte)(settings.OverlayOpacity * 255), 10, 10, 10));
            }
            else
            {
                Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));
            }

            // Show empty state if no items exist
            if (!isTestMode && (layout.Items == null || layout.Items.Count == 0))
            {
                EmptyStateText.Visibility = Visibility.Visible;
            }
        }

        private Border? _hoveredBorder;
        public DateTime HoverStartedAt { get; private set; } = DateTime.MinValue;
        public static event Action? EscPressed;

        private void Card_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (sender is Border border && border.DataContext is CanvasItem item)
            {
                HoveredItem = item;
                HoverStartedAt = DateTime.UtcNow;
                _hoveredBorder = border;
                HoveredItemText.Text = item.Title;
                HoverIndicator.Visibility = Visibility.Visible;

                if (item.VisualStyle.IsDangerous)
                {
                    HelperText.Text = "Release to confirm power action  •  ";
                    HoverIndicator.Background = item.VisualStyle.HoverBackgroundBrush;
                    HoverIndicator.BorderBrush = item.VisualStyle.BorderBrush;
                    HoveredItemText.Foreground = item.VisualStyle.BadgeTextBrush;
                }
                else
                {
                    HelperText.Text = "Release trigger to launch: ";
                    HoverIndicator.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1D2A3A"));
                    HoverIndicator.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0A84FF"));
                    HoveredItemText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0A84FF"));
                }

                // Animate Hover Entrance
                AnimateCard(border, true);
            }
        }

        private void Card_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (sender is Border border && border.DataContext is CanvasItem item)
            {
                if (HoveredItem == item)
                {
                    HoveredItem = null;
                    HoveredItemText.Text = "None";
                    HoverIndicator.Visibility = Visibility.Collapsed;
                    HelperText.Text = "Release to launch  •  Esc to cancel";
                    HoverStartedAt = DateTime.MinValue;
                    _hoveredBorder = null;
                }

                // Animate Hover Exit
                AnimateCard(border, false);
            }
        }

        private void AnimateCard(Border border, bool isHovered)
        {
            var item = border.DataContext as CanvasItem;
            if (item == null) return;

            var style = item.VisualStyle;
            double durationMs = _settings.AnimationDurationMs;
            var duration = TimeSpan.FromMilliseconds(durationMs);

            // 1. Scale Animation
            var scale = border.RenderTransform as ScaleTransform;
            if (scale == null || scale.IsFrozen)
            {
                scale = new ScaleTransform(1.0, 1.0);
                border.RenderTransform = scale;
            }
            double targetScale = isHovered ? 1.05 : 1.0;
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(targetScale, duration));
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(targetScale, duration));

            // Extract Colors from Style Brushes
            Color normalBg = style.BackgroundBrush is SolidColorBrush sb1 ? sb1.Color : (Color)ColorConverter.ConvertFromString("#131315");
            Color hoverBg = style.HoverBackgroundBrush is SolidColorBrush sb2 ? sb2.Color : (Color)ColorConverter.ConvertFromString("#1C1C1E");
            Color normalBorder = style.BorderBrush is SolidColorBrush sb3 ? sb3.Color : (Color)ColorConverter.ConvertFromString("#262629");
            Color hoverBorder = style.HoverBorderBrush is SolidColorBrush sb4 ? sb4.Color : (Color)ColorConverter.ConvertFromString("#0A84FF");

            Color startBg = isHovered ? normalBg : hoverBg;
            Color endBg = isHovered ? hoverBg : normalBg;
            Color startBorder = isHovered ? normalBorder : hoverBorder;
            Color endBorder = isHovered ? hoverBorder : normalBorder;

            // 2. Background Animation
            var bgBrush = new SolidColorBrush(startBg);
            border.Background = bgBrush;
            bgBrush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(endBg, duration));

            // 3. Border Color Animation
            var borderBrush = new SolidColorBrush(startBorder);
            border.BorderBrush = borderBrush;
            borderBrush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(endBorder, duration));

            // 4. Drop Shadow Glow Animation
            var shadow = border.Effect as DropShadowEffect;
            if (shadow == null || shadow.IsFrozen)
            {
                shadow = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 12,
                    ShadowDepth = 2,
                    Opacity = 0.4
                };
                border.Effect = shadow;
            }

            Color targetShadowColor = isHovered && style.IsDangerous ? (style.AccentBrush is SolidColorBrush sb5 ? sb5.Color : Colors.Black) : Colors.Black;
            double targetBlur = isHovered ? (style.IsDangerous ? 28.0 : 20.0) : 12.0;
            double targetOpacity = isHovered ? (style.IsDangerous ? 0.7 : 0.6) : 0.4;

            shadow.BeginAnimation(DropShadowEffect.ColorProperty, new ColorAnimation(targetShadowColor, duration));
            shadow.BeginAnimation(DropShadowEffect.BlurRadiusProperty, new DoubleAnimation(targetBlur, duration));
            shadow.BeginAnimation(DropShadowEffect.OpacityProperty, new DoubleAnimation(targetOpacity, duration));
        }

        public void ShowPressedStateAndClose(Action onCompleted)
        {
            if (_hoveredBorder != null)
            {
                var duration = TimeSpan.FromMilliseconds(60);
                var scale = _hoveredBorder.RenderTransform as ScaleTransform ?? new ScaleTransform(1.05, 1.05);
                _hoveredBorder.RenderTransform = scale;
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.96, duration));
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.96, duration));

                var borderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0A84FF"));
                _hoveredBorder.BorderBrush = borderBrush;

                var bgBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0C0C0D"));
                _hoveredBorder.Background = bgBrush;

                var timer = new System.Windows.Threading.DispatcherTimer();
                timer.Interval = TimeSpan.FromMilliseconds(80);
                timer.Tick += (s, ev) =>
                {
                    timer.Stop();
                    FadeOutAndClose(onCompleted);
                };
                timer.Start();
            }
            else
            {
                FadeOutAndClose(onCompleted);
            }
        }

        public void FadeOutAndClose(Action onCompleted)
        {
            var fadeAnimation = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(_settings.AnimationDurationMs));
            fadeAnimation.Completed += (s, e) => onCompleted();
            this.BeginAnimation(Window.OpacityProperty, fadeAnimation);
        }

        protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.Escape)
            {
                EscPressed?.Invoke();
            }
        }
    }
}
