namespace MenuTestApp;

partial class Form1
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
            components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        menuStrip1 = new MenuStrip();
        fileToolStripMenuItem = new ToolStripMenuItem();
        newToolStripMenuItem = new ToolStripMenuItem();
        openToolStripMenuItem = new ToolStripMenuItem();
        toolStripSeparator1 = new ToolStripSeparator();
        saveToolStripMenuItem = new ToolStripMenuItem();
        saveAsToolStripMenuItem = new ToolStripMenuItem();
        toolStripSeparator2 = new ToolStripSeparator();
        exitToolStripMenuItem = new ToolStripMenuItem();
        editToolStripMenuItem = new ToolStripMenuItem();
        copyToolStripMenuItem = new ToolStripMenuItem();
        pasteToolStripMenuItem = new ToolStripMenuItem();
        toolStripSeparator3 = new ToolStripSeparator();
        findToolStripMenuItem = new ToolStripMenuItem();
        findMenuItem = new ToolStripMenuItem();
        findNextToolStripMenuItem = new ToolStripMenuItem();
        findPreviousToolStripMenuItem = new ToolStripMenuItem();
        viewToolStripMenuItem = new ToolStripMenuItem();
        zoomToolStripMenuItem = new ToolStripMenuItem();
        zoomInToolStripMenuItem = new ToolStripMenuItem();
        zoomOutToolStripMenuItem = new ToolStripMenuItem();
        resetZoomToolStripMenuItem = new ToolStripMenuItem();
        toolStripSeparator4 = new ToolStripSeparator();
        statusBarToolStripMenuItem = new ToolStripMenuItem();
        helpToolStripMenuItem = new ToolStripMenuItem();
        helpContentsToolStripMenuItem = new ToolStripMenuItem();
        toolStripSeparator5 = new ToolStripSeparator();
        aboutToolStripMenuItem = new ToolStripMenuItem();
        statusStrip1 = new StatusStrip();
        toolStripStatusLabel1 = new ToolStripStatusLabel();
        label1 = new Label();
        menuStrip1.SuspendLayout();
        statusStrip1.SuspendLayout();
        SuspendLayout();
        //
        // menuStrip1
        //
        menuStrip1.Items.AddRange(new ToolStripItem[] { fileToolStripMenuItem, editToolStripMenuItem, viewToolStripMenuItem, helpToolStripMenuItem });
        menuStrip1.Location = new Point(0, 0);
        menuStrip1.Name = "menuStrip1";
        menuStrip1.Size = new Size(600, 24);
        menuStrip1.TabIndex = 0;
        menuStrip1.Text = "menuStrip1";
        //
        // fileToolStripMenuItem
        //
        fileToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { newToolStripMenuItem, openToolStripMenuItem, toolStripSeparator1, saveToolStripMenuItem, saveAsToolStripMenuItem, toolStripSeparator2, exitToolStripMenuItem });
        fileToolStripMenuItem.Name = "fileToolStripMenuItem";
        fileToolStripMenuItem.Size = new Size(37, 20);
        fileToolStripMenuItem.Text = "&File";
        //
        // newToolStripMenuItem
        //
        newToolStripMenuItem.Name = "newToolStripMenuItem";
        newToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.N;
        newToolStripMenuItem.Size = new Size(180, 22);
        newToolStripMenuItem.Text = "&New";
        //
        // openToolStripMenuItem
        //
        openToolStripMenuItem.Name = "openToolStripMenuItem";
        openToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.O;
        openToolStripMenuItem.Size = new Size(180, 22);
        openToolStripMenuItem.Text = "&Open...";
        //
        // toolStripSeparator1
        //
        toolStripSeparator1.Name = "toolStripSeparator1";
        toolStripSeparator1.Size = new Size(177, 6);
        //
        // saveToolStripMenuItem
        //
        saveToolStripMenuItem.Name = "saveToolStripMenuItem";
        saveToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.S;
        saveToolStripMenuItem.Size = new Size(180, 22);
        saveToolStripMenuItem.Text = "&Save";
        //
        // saveAsToolStripMenuItem
        //
        saveAsToolStripMenuItem.Name = "saveAsToolStripMenuItem";
        saveAsToolStripMenuItem.Size = new Size(180, 22);
        saveAsToolStripMenuItem.Text = "Save &As...";
        //
        // toolStripSeparator2
        //
        toolStripSeparator2.Name = "toolStripSeparator2";
        toolStripSeparator2.Size = new Size(177, 6);
        //
        // exitToolStripMenuItem
        //
        exitToolStripMenuItem.Name = "exitToolStripMenuItem";
        exitToolStripMenuItem.Size = new Size(180, 22);
        exitToolStripMenuItem.Text = "E&xit";
        exitToolStripMenuItem.Click += exitToolStripMenuItem_Click;
        //
        // editToolStripMenuItem
        //
        editToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { copyToolStripMenuItem, pasteToolStripMenuItem, toolStripSeparator3, findToolStripMenuItem });
        editToolStripMenuItem.Name = "editToolStripMenuItem";
        editToolStripMenuItem.Size = new Size(39, 20);
        editToolStripMenuItem.Text = "&Edit";
        //
        // copyToolStripMenuItem
        //
        copyToolStripMenuItem.Name = "copyToolStripMenuItem";
        copyToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.C;
        copyToolStripMenuItem.Size = new Size(180, 22);
        copyToolStripMenuItem.Text = "&Copy";
        //
        // pasteToolStripMenuItem
        //
        pasteToolStripMenuItem.Name = "pasteToolStripMenuItem";
        pasteToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.V;
        pasteToolStripMenuItem.Size = new Size(180, 22);
        pasteToolStripMenuItem.Text = "&Paste";
        //
        // toolStripSeparator3
        //
        toolStripSeparator3.Name = "toolStripSeparator3";
        toolStripSeparator3.Size = new Size(177, 6);
        //
        // findToolStripMenuItem
        //
        findToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { findMenuItem, findNextToolStripMenuItem, findPreviousToolStripMenuItem });
        findToolStripMenuItem.Name = "findToolStripMenuItem";
        findToolStripMenuItem.Size = new Size(180, 22);
        findToolStripMenuItem.Text = "&Find";
        //
        // findMenuItem
        //
        findMenuItem.Name = "findMenuItem";
        findMenuItem.ShortcutKeys = Keys.Control | Keys.F;
        findMenuItem.Size = new Size(180, 22);
        findMenuItem.Text = "&Find...";
        //
        // findNextToolStripMenuItem
        //
        findNextToolStripMenuItem.Name = "findNextToolStripMenuItem";
        findNextToolStripMenuItem.ShortcutKeys = Keys.F3;
        findNextToolStripMenuItem.Size = new Size(180, 22);
        findNextToolStripMenuItem.Text = "Find &Next";
        //
        // findPreviousToolStripMenuItem
        //
        findPreviousToolStripMenuItem.Name = "findPreviousToolStripMenuItem";
        findPreviousToolStripMenuItem.ShortcutKeys = Keys.Shift | Keys.F3;
        findPreviousToolStripMenuItem.Size = new Size(180, 22);
        findPreviousToolStripMenuItem.Text = "Find &Previous";
        //
        // viewToolStripMenuItem
        //
        viewToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { zoomToolStripMenuItem, toolStripSeparator4, statusBarToolStripMenuItem });
        viewToolStripMenuItem.Name = "viewToolStripMenuItem";
        viewToolStripMenuItem.Size = new Size(44, 20);
        viewToolStripMenuItem.Text = "&View";
        //
        // zoomToolStripMenuItem
        //
        zoomToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { zoomInToolStripMenuItem, zoomOutToolStripMenuItem, resetZoomToolStripMenuItem });
        zoomToolStripMenuItem.Name = "zoomToolStripMenuItem";
        zoomToolStripMenuItem.Size = new Size(180, 22);
        zoomToolStripMenuItem.Text = "&Zoom";
        //
        // zoomInToolStripMenuItem
        //
        zoomInToolStripMenuItem.Name = "zoomInToolStripMenuItem";
        zoomInToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.Add;
        zoomInToolStripMenuItem.Size = new Size(180, 22);
        zoomInToolStripMenuItem.Text = "Zoom &In";
        //
        // zoomOutToolStripMenuItem
        //
        zoomOutToolStripMenuItem.Name = "zoomOutToolStripMenuItem";
        zoomOutToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.Subtract;
        zoomOutToolStripMenuItem.Size = new Size(180, 22);
        zoomOutToolStripMenuItem.Text = "Zoom &Out";
        //
        // resetZoomToolStripMenuItem
        //
        resetZoomToolStripMenuItem.Name = "resetZoomToolStripMenuItem";
        resetZoomToolStripMenuItem.Size = new Size(180, 22);
        resetZoomToolStripMenuItem.Text = "&Reset Zoom";
        //
        // toolStripSeparator4
        //
        toolStripSeparator4.Name = "toolStripSeparator4";
        toolStripSeparator4.Size = new Size(177, 6);
        //
        // statusBarToolStripMenuItem
        //
        statusBarToolStripMenuItem.Checked = true;
        statusBarToolStripMenuItem.CheckOnClick = true;
        statusBarToolStripMenuItem.CheckState = CheckState.Checked;
        statusBarToolStripMenuItem.Name = "statusBarToolStripMenuItem";
        statusBarToolStripMenuItem.Size = new Size(180, 22);
        statusBarToolStripMenuItem.Text = "Status &Bar";
        //
        // helpToolStripMenuItem
        //
        helpToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { helpContentsToolStripMenuItem, toolStripSeparator5, aboutToolStripMenuItem });
        helpToolStripMenuItem.Name = "helpToolStripMenuItem";
        helpToolStripMenuItem.Size = new Size(44, 20);
        helpToolStripMenuItem.Text = "&Help";
        //
        // helpContentsToolStripMenuItem
        //
        helpContentsToolStripMenuItem.Name = "helpContentsToolStripMenuItem";
        helpContentsToolStripMenuItem.Size = new Size(180, 22);
        helpContentsToolStripMenuItem.Text = "Help &Contents";
        //
        // toolStripSeparator5
        //
        toolStripSeparator5.Name = "toolStripSeparator5";
        toolStripSeparator5.Size = new Size(177, 6);
        //
        // aboutToolStripMenuItem
        //
        aboutToolStripMenuItem.Name = "aboutToolStripMenuItem";
        aboutToolStripMenuItem.Size = new Size(180, 22);
        aboutToolStripMenuItem.Text = "&About";
        aboutToolStripMenuItem.Click += aboutToolStripMenuItem_Click;
        //
        // statusStrip1
        //
        statusStrip1.Items.AddRange(new ToolStripItem[] { toolStripStatusLabel1 });
        statusStrip1.Location = new Point(0, 378);
        statusStrip1.Name = "statusStrip1";
        statusStrip1.Size = new Size(600, 22);
        statusStrip1.TabIndex = 1;
        statusStrip1.Text = "statusStrip1";
        //
        // toolStripStatusLabel1
        //
        toolStripStatusLabel1.Name = "toolStripStatusLabel1";
        toolStripStatusLabel1.Size = new Size(39, 17);
        toolStripStatusLabel1.Text = "Ready";
        //
        // label1
        //
        label1.Dock = DockStyle.Fill;
        label1.Font = new Font("Segoe UI", 14F, FontStyle.Regular, GraphicsUnit.Point);
        label1.Location = new Point(0, 24);
        label1.Name = "label1";
        label1.Size = new Size(600, 354);
        label1.TabIndex = 2;
        label1.Text = "ApexUIBridge Test Application - Menu";
        label1.TextAlign = ContentAlignment.MiddleCenter;
        //
        // Form1
        //
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(600, 400);
        Controls.Add(label1);
        Controls.Add(statusStrip1);
        Controls.Add(menuStrip1);
        MainMenuStrip = menuStrip1;
        Name = "Form1";
        Text = "ApexUIBridge Test Application - Menu";
        menuStrip1.ResumeLayout(false);
        menuStrip1.PerformLayout();
        statusStrip1.ResumeLayout(false);
        statusStrip1.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
    }

    private MenuStrip menuStrip1;
    private ToolStripMenuItem fileToolStripMenuItem;
    private ToolStripMenuItem newToolStripMenuItem;
    private ToolStripMenuItem openToolStripMenuItem;
    private ToolStripSeparator toolStripSeparator1;
    private ToolStripMenuItem saveToolStripMenuItem;
    private ToolStripMenuItem saveAsToolStripMenuItem;
    private ToolStripSeparator toolStripSeparator2;
    private ToolStripMenuItem exitToolStripMenuItem;
    private ToolStripMenuItem editToolStripMenuItem;
    private ToolStripMenuItem copyToolStripMenuItem;
    private ToolStripMenuItem pasteToolStripMenuItem;
    private ToolStripSeparator toolStripSeparator3;
    private ToolStripMenuItem findToolStripMenuItem;
    private ToolStripMenuItem findMenuItem;
    private ToolStripMenuItem findNextToolStripMenuItem;
    private ToolStripMenuItem findPreviousToolStripMenuItem;
    private ToolStripMenuItem viewToolStripMenuItem;
    private ToolStripMenuItem zoomToolStripMenuItem;
    private ToolStripMenuItem zoomInToolStripMenuItem;
    private ToolStripMenuItem zoomOutToolStripMenuItem;
    private ToolStripMenuItem resetZoomToolStripMenuItem;
    private ToolStripSeparator toolStripSeparator4;
    private ToolStripMenuItem statusBarToolStripMenuItem;
    private ToolStripMenuItem helpToolStripMenuItem;
    private ToolStripMenuItem helpContentsToolStripMenuItem;
    private ToolStripSeparator toolStripSeparator5;
    private ToolStripMenuItem aboutToolStripMenuItem;
    private StatusStrip statusStrip1;
    private ToolStripStatusLabel toolStripStatusLabel1;
    private Label label1;
}
