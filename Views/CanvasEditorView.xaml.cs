using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using HoldSpace.Models;
using HoldSpace.Services;

namespace HoldSpace.Views
{
    public partial class CanvasEditorView : System.Windows.Controls.UserControl
    {
        private SettingsService? _settingsService;
        private bool _isDragging;
        private CanvasItem? _draggedItem;
        private CanvasItem? _selectedItem;

        private bool _hasUnsavedChanges;
        private bool _isBindingProperties;

        // Dragging & Snap States
        private System.Windows.Point _dragStartOffset;
        private bool _isSnapToGrid = true;
        private bool _suppressModeSwitch = false;
        
        // Undo / Redo History Stacks
        private readonly System.Collections.Generic.List<string> _undoStack = new System.Collections.Generic.List<string>();
        private readonly System.Collections.Generic.List<string> _redoStack = new System.Collections.Generic.List<string>();

        public CanvasEditorView()
        {
            InitializeComponent();
            Focusable = true;
            PreviewKeyDown += CanvasEditorView_PreviewKeyDown;
        }

        public void Initialize(SettingsService settingsService)
        {
            _settingsService = settingsService;
            RefreshItems();
            SetUnsavedChanges(false);
            ClearSelection();

            _undoStack.Clear();
            _redoStack.Clear();
            UpdateUndoRedoButtons();

            if (CheckSnap != null)
                CheckSnap.IsChecked = _isSnapToGrid;

            // Populate mode switcher
            if (ModeSwitcher != null && _settingsService?.ProfilesLayout != null)
            {
                _suppressModeSwitch = true;
                ModeSwitcher.ItemsSource = null;
                ModeSwitcher.ItemsSource = _settingsService.ProfilesLayout.Profiles;
                ModeSwitcher.DisplayMemberPath = "Name";
                ModeSwitcher.SelectedItem = _settingsService.ActiveProfile;
                _suppressModeSwitch = false;
            }
        }

        private void SetUnsavedChanges(bool hasChanges)
        {
            _hasUnsavedChanges = hasChanges;
            if (TxtUnsavedStatus != null)
            {
                TxtUnsavedStatus.Visibility = hasChanges ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void RefreshItems()
        {
            if (_settingsService != null)
            {
                EditorItemsControl.ItemsSource = null;
                EditorItemsControl.ItemsSource = _settingsService.CurrentLayout.Items;
            }
        }

        private void ModeSwitcher_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressModeSwitch || _settingsService == null) return;
            if (ModeSwitcher.SelectedItem is HoldSpace.Models.ShortcutProfile selected)
            {
                _settingsService.SetActiveProfile(selected.Id);
                RefreshItems();
                ClearSelection();
                SetUnsavedChanges(false);
                // Propagate to rest of dashboard
                var mw = Window.GetWindow(this) as MainWindow;
                mw?.OnProfileChanged();
            }
        }

        private void Card_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border cardBorder && cardBorder.DataContext is CanvasItem item)
            {
                PushUndoState();

                _isDragging = true;
                _draggedItem = item;
                cardBorder.CaptureMouse();

                var canvas = FindVisualParent<Canvas>(cardBorder);
                if (canvas != null)
                {
                    System.Windows.Point mousePos = e.GetPosition(canvas);
                    double itemXAbs = (_draggedItem.X / 100.0) * canvas.ActualWidth;
                    double itemYAbs = (_draggedItem.Y / 100.0) * canvas.ActualHeight;

                    _dragStartOffset = new System.Windows.Point(mousePos.X - itemXAbs, mousePos.Y - itemYAbs);
                }

                SelectItem(item);
                e.Handled = true;
            }
        }

        private void Card_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_isDragging && _draggedItem != null && sender is Border cardBorder)
            {
                var canvas = FindVisualParent<Canvas>(cardBorder);
                if (canvas != null)
                {
                    System.Windows.Point mousePos = e.GetPosition(canvas);
                    
                    double targetXAbs = mousePos.X - _dragStartOffset.X;
                    double targetYAbs = mousePos.Y - _dragStartOffset.Y;

                    // 1. Alignment snapping checks
                    bool showVGuide = false;
                    bool showHGuide = false;
                    double vGuideX = 0;
                    double hGuideY = 0;

                    if (_settingsService != null)
                    {
                        foreach (var otherItem in _settingsService.CurrentLayout.Items)
                        {
                            if (otherItem == _draggedItem) continue;

                            double otherXAbs = (otherItem.X / 100.0) * canvas.ActualWidth;
                            double otherYAbs = (otherItem.Y / 100.0) * canvas.ActualHeight;

                            if (Math.Abs(targetXAbs - otherXAbs) < 8.0)
                            {
                                targetXAbs = otherXAbs;
                                showVGuide = true;
                                vGuideX = otherXAbs;
                            }

                            if (Math.Abs(targetYAbs - otherYAbs) < 8.0)
                            {
                                targetYAbs = otherYAbs;
                                showHGuide = true;
                                hGuideY = otherYAbs;
                            }
                        }
                    }

                    // 2. Snap to grid if enabled (and Alt is not held)
                    if (_isSnapToGrid && !Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
                    {
                        if (!showVGuide) targetXAbs = Math.Round(targetXAbs / 24.0) * 24.0;
                        if (!showHGuide) targetYAbs = Math.Round(targetYAbs / 24.0) * 24.0;
                    }

                    // 3. Render Guides
                    if (GuideVertical != null)
                    {
                        if (showVGuide)
                        {
                            GuideVertical.X1 = vGuideX;
                            GuideVertical.Y1 = 0;
                            GuideVertical.X2 = vGuideX;
                            GuideVertical.Y2 = canvas.ActualHeight;
                            GuideVertical.Visibility = Visibility.Visible;
                        }
                        else
                        {
                            GuideVertical.Visibility = Visibility.Collapsed;
                        }
                    }

                    if (GuideHorizontal != null)
                    {
                        if (showHGuide)
                        {
                            GuideHorizontal.X1 = 0;
                            GuideHorizontal.Y1 = hGuideY;
                            GuideHorizontal.X2 = canvas.ActualWidth;
                            GuideHorizontal.Y2 = hGuideY;
                            GuideHorizontal.Visibility = Visibility.Visible;
                        }
                        else
                        {
                            GuideHorizontal.Visibility = Visibility.Collapsed;
                        }
                    }

                    // 4. Update coordinates
                    double pctX = (targetXAbs / canvas.ActualWidth) * 100.0;
                    double pctY = (targetYAbs / canvas.ActualHeight) * 100.0;

                    pctX = Math.Clamp(pctX, 5.0, 95.0);
                    pctY = Math.Clamp(pctY, 5.0, 95.0);

                    _draggedItem.X = Math.Round(pctX, 1);
                    _draggedItem.Y = Math.Round(pctY, 1);

                    _isBindingProperties = true;
                    try
                    {
                        TxtCoordX.Text = _draggedItem.X.ToString("F1");
                        TxtCoordY.Text = _draggedItem.Y.ToString("F1");
                    }
                    finally
                    {
                        _isBindingProperties = false;
                    }

                    SetUnsavedChanges(true);
                }
            }
        }

        private void Card_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging && sender is Border cardBorder)
            {
                _isDragging = false;
                _draggedItem = null;
                cardBorder.ReleaseMouseCapture();

                if (GuideVertical != null) GuideVertical.Visibility = Visibility.Collapsed;
                if (GuideHorizontal != null) GuideHorizontal.Visibility = Visibility.Collapsed;

                SetUnsavedChanges(true);
            }
        }

        private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ClearSelection();
        }

        private void SelectItem(CanvasItem item)
        {
            _isBindingProperties = true;
            try
            {
                if (_settingsService != null)
                {
                    foreach (var i in _settingsService.CurrentLayout.Items)
                    {
                        i.IsSelected = false;
                    }
                }

                _selectedItem = item;
                _selectedItem.IsSelected = true;

                PropertiesFormPanel.Visibility = Visibility.Visible;
                NoSelectionPanel.Visibility = Visibility.Collapsed;

                TxtTitle.Text = _selectedItem.Title;
                TxtTarget.Text = _selectedItem.Action.Target;
                TxtArguments.Text = _selectedItem.Action.Arguments;
                TxtCoordX.Text = _selectedItem.X.ToString("F1");
                TxtCoordY.Text = _selectedItem.Y.ToString("F1");

                if (TxtInspectorIcon != null)
                {
                    TxtInspectorIcon.Text = _selectedItem.DisplayIcon;
                }

                string type = _selectedItem.Action.Type.ToLowerInvariant();
                switch (type)
                {
                    case "app":
                        ComboType.SelectedIndex = 0;
                        break;
                    case "folder":
                        ComboType.SelectedIndex = 1;
                        break;
                    case "file":
                        ComboType.SelectedIndex = 2;
                        break;
                    case "website":
                        ComboType.SelectedIndex = 3;
                        break;
                    case "systemaction":
                        ComboType.SelectedIndex = 4;
                        break;
                    default:
                        ComboType.SelectedIndex = 3;
                        break;
                }

                ConfigureTargetLabel();
            }
            finally
            {
                _isBindingProperties = false;
            }
        }

        private void ClearSelection()
        {
            if (_settingsService != null)
            {
                foreach (var i in _settingsService.CurrentLayout.Items)
                {
                    i.IsSelected = false;
                }
            }

            _selectedItem = null;
            PropertiesFormPanel.Visibility = Visibility.Collapsed;
            NoSelectionPanel.Visibility = Visibility.Visible;
        }

        private void ConfigureTargetLabel()
        {
            if (ComboType == null || LblTarget == null || LblArguments == null || TxtArguments == null || BtnBrowse == null) return;

            string selectedText = (ComboType.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
            
            if (selectedText.Contains("App"))
            {
                LblTarget.Text = "Executable File Path (.exe)";
                LblArguments.Visibility = Visibility.Visible;
                TxtArguments.Visibility = Visibility.Visible;
                BtnBrowse.Visibility = Visibility.Visible;
            }
            else if (selectedText.Contains("Folder"))
            {
                LblTarget.Text = "Folder Path";
                LblArguments.Visibility = Visibility.Collapsed;
                TxtArguments.Visibility = Visibility.Collapsed;
                BtnBrowse.Visibility = Visibility.Visible;
            }
            else if (selectedText.Contains("File"))
            {
                LblTarget.Text = "File Path";
                LblArguments.Visibility = Visibility.Collapsed;
                TxtArguments.Visibility = Visibility.Collapsed;
                BtnBrowse.Visibility = Visibility.Visible;
            }
            else if (selectedText.Contains("Website"))
            {
                LblTarget.Text = "Website URL";
                LblArguments.Visibility = Visibility.Collapsed;
                TxtArguments.Visibility = Visibility.Collapsed;
                BtnBrowse.Visibility = Visibility.Collapsed;
            }
            else if (selectedText.Contains("System Action"))
            {
                LblTarget.Text = "Action Command ('settings', 'taskmanager', 'terminal', 'lock', 'desktop')";
                LblArguments.Visibility = Visibility.Collapsed;
                TxtArguments.Visibility = Visibility.Collapsed;
                BtnBrowse.Visibility = Visibility.Collapsed;
            }
        }

        private void TxtTitle_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_selectedItem != null && TxtTitle != null)
            {
                _selectedItem.Title = TxtTitle.Text;

                if (TxtInspectorIcon != null)
                {
                    TxtInspectorIcon.Text = _selectedItem.DisplayIcon;
                }

                if (!_isBindingProperties)
                {
                    SetUnsavedChanges(true);
                }
            }
        }

        private void TxtTarget_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_selectedItem != null && TxtTarget != null)
            {
                _selectedItem.Action.Target = TxtTarget.Text;

                if (!_isBindingProperties)
                {
                    SetUnsavedChanges(true);
                }
            }
        }

        private void TxtArguments_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_selectedItem != null && TxtArguments != null)
            {
                _selectedItem.Action.Arguments = TxtArguments.Text;

                if (!_isBindingProperties)
                {
                    SetUnsavedChanges(true);
                }
            }
        }

        private void ComboType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ConfigureTargetLabel();

            if (_selectedItem != null && ComboType != null)
            {
                switch (ComboType.SelectedIndex)
                {
                    case 0:
                        _selectedItem.Action.Type = "app";
                        break;
                    case 1:
                        _selectedItem.Action.Type = "folder";
                        break;
                    case 2:
                        _selectedItem.Action.Type = "file";
                        break;
                    case 3:
                        _selectedItem.Action.Type = "website";
                        break;
                    case 4:
                        _selectedItem.Action.Type = "systemAction";
                        break;
                }

                _selectedItem.NotifyIconChanged();
                if (TxtInspectorIcon != null)
                {
                    TxtInspectorIcon.Text = _selectedItem.DisplayIcon;
                }

                if (!_isBindingProperties)
                {
                    SetUnsavedChanges(true);
                }
            }
        }

        private void TxtCoordX_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isBindingProperties || _selectedItem == null) return;
            if (double.TryParse(TxtCoordX.Text, out double x))
            {
                _selectedItem.X = Math.Clamp(x, 5.0, 95.0);
                SetUnsavedChanges(true);
            }
        }

        private void TxtCoordY_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isBindingProperties || _selectedItem == null) return;
            if (double.TryParse(TxtCoordY.Text, out double y))
            {
                _selectedItem.Y = Math.Clamp(y, 5.0, 95.0);
                SetUnsavedChanges(true);
            }
        }

        private void InspectorField_GotFocus(object sender, RoutedEventArgs e)
        {
            PushUndoState();
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            if (ComboType == null || _selectedItem == null) return;

            string selectedText = (ComboType.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";

            if (selectedText.Contains("App"))
            {
                var ofd = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "Executable Files (*.exe)|*.exe|All Files (*.*)|*.*",
                    Title = "Select Application Exe"
                };
                if (ofd.ShowDialog() == true)
                {
                    TxtTarget.Text = ofd.FileName;
                }
            }
            else if (selectedText.Contains("Folder"))
            {
                using (var fbd = new System.Windows.Forms.FolderBrowserDialog())
                {
                    fbd.Description = "Select Shortcut Folder";
                    if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        TxtTarget.Text = fbd.SelectedPath;
                    }
                }
            }
            else if (selectedText.Contains("File"))
            {
                var ofd = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "All Files (*.*)|*.*",
                    Title = "Select File"
                };
                if (ofd.ShowDialog() == true)
                {
                    TxtTarget.Text = ofd.FileName;
                }
            }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedItem != null && _settingsService != null)
            {
                var result = System.Windows.MessageBox.Show(
                    "Are you sure you want to delete this shortcut?",
                    "Delete Shortcut",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    PushUndoState();

                    _settingsService.CurrentLayout.Items.Remove(_selectedItem);
                    ClearSelection();
                    RefreshItems();
                    SetUnsavedChanges(true);
                }
            }
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            if (_settingsService == null) return;

            var dialog = new AddShortcutWindow();
            var window = Window.GetWindow(this);
            if (window != null)
            {
                dialog.Owner = window;
            }

            if (dialog.ShowDialog() == true && dialog.OutputItem != null)
            {
                var newItem = dialog.OutputItem;

                if (IsDuplicate(newItem, out string existingTitle))
                {
                    var result = System.Windows.MessageBox.Show(
                        $"A shortcut with target '{newItem.Action.Target}' already exists under the name '{existingTitle}'.\n\nDo you want to add it anyway?",
                        "Duplicate Shortcut",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result != MessageBoxResult.Yes)
                    {
                        return;
                    }
                }

                PushUndoState();

                System.Windows.Point emptyLoc = FindSmartEmptyLocation(_settingsService.CurrentLayout);
                newItem.X = emptyLoc.X;
                newItem.Y = emptyLoc.Y;

                _settingsService.CurrentLayout.Items.Add(newItem);
                RefreshItems();
                SelectItem(newItem);
                SetUnsavedChanges(true);
            }
        }

        private System.Windows.Point FindSmartEmptyLocation(CanvasLayout layout)
        {
            double[] cols = { 20, 35, 50, 65, 80 };
            double[] rows = { 20, 35, 50, 65, 80 };

            foreach (double r in rows)
            {
                foreach (double c in cols)
                {
                    bool isOccupied = false;
                    foreach (var item in layout.Items)
                    {
                        double dx = item.X - c;
                        double dy = item.Y - r;
                        double dist = Math.Sqrt(dx * dx + dy * dy);
                        if (dist < 6.0)
                        {
                            isOccupied = true;
                            break;
                        }
                    }

                    if (!isOccupied)
                    {
                        return new System.Windows.Point(c, r);
                    }
                }
            }

            var random = new Random();
            return new System.Windows.Point(40 + random.NextDouble() * 20, 40 + random.NextDouble() * 20);
        }

        private bool IsDuplicate(CanvasItem newItem, out string existingTitle)
        {
            existingTitle = "";
            if (_settingsService == null) return false;

            string newTarget = NormalizeTarget(newItem.Action.Target, newItem.Action.Type);
            string newType = newItem.Action.Type.ToLowerInvariant();

            foreach (var item in _settingsService.CurrentLayout.Items)
            {
                string existingTarget = NormalizeTarget(item.Action.Target, item.Action.Type);
                string existingType = item.Action.Type.ToLowerInvariant();

                if (newType == existingType && newTarget == existingTarget)
                {
                    existingTitle = item.Title;
                    return true;
                }
            }
            return false;
        }

        private string NormalizeTarget(string target, string type)
        {
            string cleaned = target.Trim().ToLowerInvariant();

            if (type.ToLowerInvariant() == "app" || type.ToLowerInvariant() == "file" || type.ToLowerInvariant() == "folder")
            {
                cleaned = cleaned.Replace('/', '\\');
            }

            if (type.ToLowerInvariant() == "website")
            {
                if (!cleaned.Contains("://"))
                {
                    cleaned = "https://" + cleaned;
                }
                cleaned = cleaned.TrimEnd('/');
            }

            return cleaned;
        }

        public void TriggerAddShortcut()
        {
            BtnAdd_Click(this, new RoutedEventArgs());
        }

        private void BtnSaveLayout_Click(object sender, RoutedEventArgs e)
        {
            if (_settingsService != null)
            {
                _settingsService.SaveLayout();
                SetUnsavedChanges(false);

                BtnSaveLayout.Content = "Saved ✓";
                BtnSaveLayout.IsEnabled = false;

                var timer = new System.Windows.Threading.DispatcherTimer();
                timer.Interval = TimeSpan.FromSeconds(1.2);
                timer.Tick += (s, ev) =>
                {
                    timer.Stop();
                    BtnSaveLayout.Content = "Save Layout";
                    BtnSaveLayout.IsEnabled = true;
                };
                timer.Start();
            }
        }

        private void BtnResetLayout_Click(object sender, RoutedEventArgs e)
        {
            if (_settingsService == null) return;

            var result = System.Windows.MessageBox.Show(
                "Are you sure you want to reset the canvas layout? This will restore the default shortcuts layout and replace your current arrangement.",
                "Reset Canvas Layout",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                PushUndoState();

                _settingsService.ResetLayoutToDefault();
                ClearSelection();
                RefreshItems();
                SetUnsavedChanges(true);
            }
        }

        private void BtnTest_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var app = System.Windows.Application.Current as App;
                app?.ShowOverlayTest();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to test overlay: {ex.Message}");
            }
        }

        private void CheckSnap_Checked(object sender, RoutedEventArgs e)
        {
            _isSnapToGrid = true;
        }

        private void CheckSnap_Unchecked(object sender, RoutedEventArgs e)
        {
            _isSnapToGrid = false;
        }

        private void SliderZoom_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (CanvasScale != null && TxtZoomPct != null)
            {
                double val = e.NewValue;
                CanvasScale.ScaleX = val;
                CanvasScale.ScaleY = val;
                TxtZoomPct.Text = $"{Math.Round(val * 100)}%";
            }
        }

        private void PushUndoState()
        {
            if (_settingsService == null) return;

            string state = System.Text.Json.JsonSerializer.Serialize(_settingsService.CurrentLayout);
            _undoStack.Add(state);
            if (_undoStack.Count > 50)
            {
                _undoStack.RemoveAt(0);
            }
            _redoStack.Clear();
            UpdateUndoRedoButtons();
        }

        private void Undo()
        {
            if (_settingsService == null || _undoStack.Count == 0) return;

            string currentState = System.Text.Json.JsonSerializer.Serialize(_settingsService.CurrentLayout);
            _redoStack.Add(currentState);

            string lastState = _undoStack[_undoStack.Count - 1];
            _undoStack.RemoveAt(_undoStack.Count - 1);

            var restoredLayout = System.Text.Json.JsonSerializer.Deserialize<CanvasLayout>(lastState);
            if (restoredLayout != null)
            {
                _settingsService.CurrentLayout.Items.Clear();
                foreach (var item in restoredLayout.Items)
                {
                    _settingsService.CurrentLayout.Items.Add(item);
                }
            }

            RefreshItems();
            ClearSelection();
            SetUnsavedChanges(true);
            UpdateUndoRedoButtons();
        }

        private void Redo()
        {
            if (_settingsService == null || _redoStack.Count == 0) return;

            string currentState = System.Text.Json.JsonSerializer.Serialize(_settingsService.CurrentLayout);
            _undoStack.Add(currentState);

            string nextState = _redoStack[_redoStack.Count - 1];
            _redoStack.RemoveAt(_redoStack.Count - 1);

            var restoredLayout = System.Text.Json.JsonSerializer.Deserialize<CanvasLayout>(nextState);
            if (restoredLayout != null)
            {
                _settingsService.CurrentLayout.Items.Clear();
                foreach (var item in restoredLayout.Items)
                {
                    _settingsService.CurrentLayout.Items.Add(item);
                }
            }

            RefreshItems();
            ClearSelection();
            SetUnsavedChanges(true);
            UpdateUndoRedoButtons();
        }

        private void UpdateUndoRedoButtons()
        {
            if (BtnUndo != null) BtnUndo.IsEnabled = _undoStack.Count > 0;
            if (BtnRedo != null) BtnRedo.IsEnabled = _redoStack.Count > 0;
        }

        private void BtnUndo_Click(object sender, RoutedEventArgs e)
        {
            Undo();
        }

        private void BtnRedo_Click(object sender, RoutedEventArgs e)
        {
            Redo();
        }

        private void CanvasEditorView_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.S && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                BtnSaveLayout_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Z && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                Undo();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Y && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                Redo();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Delete && _selectedItem != null)
            {
                if (!(Keyboard.FocusedElement is System.Windows.Controls.TextBox))
                {
                    BtnDelete_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    return;
                }
            }

            if (e.Key == Key.Escape && _selectedItem != null)
            {
                ClearSelection();
                e.Handled = true;
                return;
            }

            if (_selectedItem != null && (e.Key == Key.Left || e.Key == Key.Right || e.Key == Key.Up || e.Key == Key.Down))
            {
                if (Keyboard.FocusedElement is System.Windows.Controls.TextBox tb && (tb.Name == "TxtCoordX" || tb.Name == "TxtCoordY" || tb.Name == "TxtTitle" || tb.Name == "TxtTarget" || tb.Name == "TxtArguments"))
                {
                    return;
                }

                if (!e.IsRepeat)
                {
                    PushUndoState();
                }

                double canvasWidth = 800.0;
                double canvasHeight = 500.0;

                foreach (Window win in System.Windows.Application.Current.Windows)
                {
                    if (win is MainWindow mainWin)
                    {
                        var canvas = FindVisualChild<Canvas>(mainWin);
                        if (canvas != null && canvas.ActualWidth > 0)
                        {
                            canvasWidth = canvas.ActualWidth;
                            canvasHeight = canvas.ActualHeight;
                            break;
                        }
                    }
                }

                double nudgePixels = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 10.0 : 1.0;
                double nudgePctX = (nudgePixels / canvasWidth) * 100.0;
                double nudgePctY = (nudgePixels / canvasHeight) * 100.0;

                switch (e.Key)
                {
                    case Key.Left:
                        _selectedItem.X = Math.Clamp(Math.Round(_selectedItem.X - nudgePctX, 1), 5.0, 95.0);
                        break;
                    case Key.Right:
                        _selectedItem.X = Math.Clamp(Math.Round(_selectedItem.X + nudgePctX, 1), 5.0, 95.0);
                        break;
                    case Key.Up:
                        _selectedItem.Y = Math.Clamp(Math.Round(_selectedItem.Y - nudgePctY, 1), 5.0, 95.0);
                        break;
                    case Key.Down:
                        _selectedItem.Y = Math.Clamp(Math.Round(_selectedItem.Y + nudgePctY, 1), 5.0, 95.0);
                        break;
                }

                _isBindingProperties = true;
                try
                {
                    TxtCoordX.Text = _selectedItem.X.ToString("F1");
                    TxtCoordY.Text = _selectedItem.Y.ToString("F1");
                }
                finally
                {
                    _isBindingProperties = false;
                }

                SetUnsavedChanges(true);
                e.Handled = true;
            }
        }

        private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T parent)
                {
                    return parent;
                }
                child = System.Windows.Media.VisualTreeHelper.GetParent(child);
            }
            return null;
        }

        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T childType)
                {
                    return childType;
                }
                var childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                {
                    return childOfChild;
                }
            }
            return null;
        }
    }
}
