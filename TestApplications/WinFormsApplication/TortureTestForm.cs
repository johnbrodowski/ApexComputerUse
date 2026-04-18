using System;
using System.Windows.Forms;

namespace WinFormsApplication
{
    public partial class TortureTestForm : Form
    {
        public TortureTestForm()
        {
            InitializeComponent();
        }

        private void ExitMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void OpenFileButton_Click(object sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog
            {
                Title = "Open file",
                Filter = "All files (*.*)|*.*|Text files (*.txt)|*.txt"
            };
            dialog.ShowDialog(this);
        }

        private void SaveFileButton_Click(object sender, EventArgs e)
        {
            using var dialog = new SaveFileDialog
            {
                Title = "Save file",
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*"
            };
            dialog.ShowDialog(this);
        }

        private void PickColorButton_Click(object sender, EventArgs e)
        {
            using var dialog = new ColorDialog { AnyColor = true };
            dialog.ShowDialog(this);
        }

        private void PickFontButton_Click(object sender, EventArgs e)
        {
            using var dialog = new FontDialog();
            dialog.ShowDialog(this);
        }

        private void MessageButton_Click(object sender, EventArgs e)
        {
            MessageBox.Show(this, "Torture test form message dialog.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
