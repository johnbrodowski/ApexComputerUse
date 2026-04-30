using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace WpfApplication
{
    public partial class TortureTestWindow : Window
    {
        public TortureTestWindow()
        {
            InitializeComponent();
            DataContext = new TortureTestViewModel();
        }

        // \-\- Identity tab \-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-
        private void OnYearsUp(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(YearsBox.Text, out int v))
                YearsBox.Text = Math.Min(v + 1, 50).ToString();
        }

        private void OnYearsDown(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(YearsBox.Text, out int v))
                YearsBox.Text = Math.Max(v - 1, 0).ToString();
        }

        // \-\- Logs tab \-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-
        private void OnClearLog(object sender, RoutedEventArgs e)
        {
            LogBox.Document.Blocks.Clear();
        }

        // \-\- WPF tab \-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-
        private void OnClearInk(object sender, RoutedEventArgs e)
        {
            TortureInkCanvas.Strokes.Clear();
        }

        // \-\- Menu \-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-
        private void OnExit(object sender, RoutedEventArgs e) => Close();

        // \-\- Dialogs tab \-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-
        private void OnOpenFile(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "All files|*.*|Text files|*.txt" };
            dlg.ShowDialog(this);
        }

        private void OnSaveFile(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog { Filter = "JSON files|*.json|All files|*.*" };
            dlg.ShowDialog(this);
        }

        private void OnBrowseFolder(object sender, RoutedEventArgs e)
        {
            // WPF doesn't have FolderBrowserDialog natively; use the OpenFileDialog trick
            var dlg = new OpenFileDialog
            {
                Title            = "Select a folder",
                Filter           = "Folders|\n",
                CheckFileExists  = false,
                CheckPathExists  = true,
                FileName         = "Select Folder",
            };
            dlg.ShowDialog(this);
        }

        private void OnChooseFont(object sender, RoutedEventArgs e)
        {
            if (!OperatingSystem.IsWindowsVersionAtLeast(6, 1))
            {
                return;
            }

            using var dlg = new System.Windows.Forms.FontDialog();
            _ = dlg.ShowDialog();
        }

        private void OnChooseColor(object sender, RoutedEventArgs e)
        {
            if (!OperatingSystem.IsWindowsVersionAtLeast(6, 1))
            {
                return;
            }

            using var dlg = new System.Windows.Forms.ColorDialog { AnyColor = true };
            _ = dlg.ShowDialog();
        }

        private void OnMsgInfo(object sender, RoutedEventArgs e)
            => MessageBox.Show("This is an information message.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);

        private void OnMsgWarn(object sender, RoutedEventArgs e)
            => MessageBox.Show("This action cannot be undone.", "Warning", MessageBoxButton.OKCancel, MessageBoxImage.Warning);

        private void OnMsgError(object sender, RoutedEventArgs e)
            => MessageBox.Show("A critical error occurred.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);

        private void OnMsgQuestion(object sender, RoutedEventArgs e)
            => MessageBox.Show("Do you want to proceed?", "Confirm", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
    }
}
