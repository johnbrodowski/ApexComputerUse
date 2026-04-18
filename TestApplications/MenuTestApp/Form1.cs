namespace MenuTestApp;

public partial class Form1 : Form
{
    public Form1()
    {
        InitializeComponent();
    }

    private void exitToolStripMenuItem_Click(object sender, EventArgs e) => Close();

    private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
    {
        MessageBox.Show("FlaUI Menu Test App\nVersion 1.0", "About", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
