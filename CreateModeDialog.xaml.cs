using System.Windows;
using HoldSpace.Services;

namespace HoldSpace
{
    public partial class CreateModeDialog : Window
    {
        public string ModeName { get; private set; } = "";
        public string TemplateName { get; private set; } = "Empty";

        public CreateModeDialog()
        {
            InitializeComponent();
            CmbTemplate.ItemsSource = ModeTemplateService.TemplateNames;
            CmbTemplate.SelectedIndex = 0;
            TxtName.Text = "New Mode";
            TxtName.SelectAll();
            TxtName.Focus();
            RefreshPreview();
        }

        private void CmbTemplate_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            RefreshPreview();
            // Auto-fill name when user picks a template (if name is generic)
            string selected = CmbTemplate.SelectedItem?.ToString() ?? "";
            if (selected != "Empty" && (TxtName.Text == "New Mode" || string.IsNullOrWhiteSpace(TxtName.Text)))
                TxtName.Text = selected;
        }

        private void RefreshPreview()
        {
            string template = CmbTemplate.SelectedItem?.ToString() ?? "Empty";
            PreviewList.ItemsSource = ModeTemplateService.GetPreview(template);
        }

        private void BtnCreate_Click(object sender, RoutedEventArgs e)
        {
            string name = TxtName.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                TxtName.Focus();
                return;
            }
            ModeName = name;
            TemplateName = CmbTemplate.SelectedItem?.ToString() ?? "Empty";
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
