namespace ApexComputerUse
{
    partial class ClientEditForm
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
            tabControl        = new TabControl();
            tabConnection     = new TabPage();
            tabPermissions    = new TabPage();
            lblName           = new Label();
            txtName           = new TextBox();
            lblHost           = new Label();
            txtHost           = new TextBox();
            lblPort           = new Label();
            txtPort           = new TextBox();
            lblApiKey         = new Label();
            txtApiKey         = new TextBox();
            lblOsVersion      = new Label();
            txtOsVersion      = new TextBox();
            lblDescription    = new Label();
            txtDescription    = new TextBox();
            chkAutomation     = new CheckBox();
            chkCapture        = new CheckBox();
            chkAi             = new CheckBox();
            chkScenes         = new CheckBox();
            chkShellRun       = new CheckBox();
            chkClients        = new CheckBox();
            lblPermNote       = new Label();
            btnOk             = new Button();
            btnCancel         = new Button();

            SuspendLayout();

            // ── Tab control ───────────────────────────────────────────────
            tabControl.Location = new Point(8, 8);
            tabControl.Size     = new Size(368, 270);
            tabControl.TabIndex = 0;
            tabControl.Controls.Add(tabConnection);
            tabControl.Controls.Add(tabPermissions);

            // ── Connection tab ────────────────────────────────────────────
            tabConnection.Text    = "Connection";
            tabConnection.Padding = new Padding(4);
            tabConnection.Controls.Add(lblName);
            tabConnection.Controls.Add(txtName);
            tabConnection.Controls.Add(lblHost);
            tabConnection.Controls.Add(txtHost);
            tabConnection.Controls.Add(lblPort);
            tabConnection.Controls.Add(txtPort);
            tabConnection.Controls.Add(lblApiKey);
            tabConnection.Controls.Add(txtApiKey);
            tabConnection.Controls.Add(lblOsVersion);
            tabConnection.Controls.Add(txtOsVersion);
            tabConnection.Controls.Add(lblDescription);
            tabConnection.Controls.Add(txtDescription);

            lblName.AutoSize = true;
            lblName.Location = new Point(10, 14);
            lblName.Text     = "Name:";

            txtName.Location = new Point(96, 11);
            txtName.Size     = new Size(258, 23);

            lblHost.AutoSize = true;
            lblHost.Location = new Point(10, 46);
            lblHost.Text     = "Host / IP:";

            txtHost.Location = new Point(96, 43);
            txtHost.Size     = new Size(258, 23);

            lblPort.AutoSize = true;
            lblPort.Location = new Point(10, 78);
            lblPort.Text     = "Port:";

            txtPort.Location = new Point(96, 75);
            txtPort.Size     = new Size(80, 23);

            lblApiKey.AutoSize = true;
            lblApiKey.Location = new Point(10, 110);
            lblApiKey.Text     = "API Key:";

            txtApiKey.Location     = new Point(96, 107);
            txtApiKey.Size         = new Size(258, 23);
            txtApiKey.PasswordChar = '*';

            lblOsVersion.AutoSize = true;
            lblOsVersion.Location = new Point(10, 142);
            lblOsVersion.Text     = "OS Version:";

            txtOsVersion.Location = new Point(96, 139);
            txtOsVersion.Size     = new Size(258, 23);

            lblDescription.AutoSize = true;
            lblDescription.Location = new Point(10, 174);
            lblDescription.Text     = "Description:";

            txtDescription.Location  = new Point(96, 171);
            txtDescription.Size      = new Size(258, 55);
            txtDescription.Multiline = true;

            // ── Permissions tab ───────────────────────────────────────────
            tabPermissions.Text    = "Permissions";
            tabPermissions.Padding = new Padding(4);
            tabPermissions.Controls.Add(lblPermNote);
            tabPermissions.Controls.Add(chkAutomation);
            tabPermissions.Controls.Add(chkCapture);
            tabPermissions.Controls.Add(chkAi);
            tabPermissions.Controls.Add(chkScenes);
            tabPermissions.Controls.Add(chkShellRun);
            tabPermissions.Controls.Add(chkClients);

            lblPermNote.AutoSize  = true;
            lblPermNote.ForeColor = System.Drawing.Color.Gray;
            lblPermNote.Location  = new Point(10, 10);
            lblPermNote.Text      = "Controls what this client can do when it connects to this server.";

            chkAutomation.AutoSize = true;
            chkAutomation.Location = new Point(10, 38);
            chkAutomation.Text     = "Allow Automation  (find, exec, elements, windows)";

            chkCapture.AutoSize = true;
            chkCapture.Location = new Point(10, 66);
            chkCapture.Text     = "Allow Capture  (screenshots, OCR)";

            chkAi.AutoSize = true;
            chkAi.Location = new Point(10, 94);
            chkAi.Text     = "Allow AI  (inference, chat)";

            chkScenes.AutoSize = true;
            chkScenes.Location = new Point(10, 122);
            chkScenes.Text     = "Allow Scenes  (scene editor)";

            chkShellRun.AutoSize    = true;
            chkShellRun.Location    = new Point(10, 150);
            chkShellRun.Text        = "Allow Shell Run  (execute OS commands — use with caution)";
            chkShellRun.ForeColor   = System.Drawing.Color.OrangeRed;

            chkClients.AutoSize = true;
            chkClients.Location = new Point(10, 178);
            chkClients.Text     = "Allow Client List  (see and manage other connected clients)";

            // ── Buttons ───────────────────────────────────────────────────
            btnOk.Location = new Point(216, 288);
            btnOk.Size     = new Size(75, 28);
            btnOk.Text     = "OK";
            btnOk.Click   += btnOk_Click;

            btnCancel.Location = new Point(297, 288);
            btnCancel.Size     = new Size(75, 28);
            btnCancel.Text     = "Cancel";
            btnCancel.Click   += btnCancel_Click;

            // ── Form ──────────────────────────────────────────────────────
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode       = AutoScaleMode.Font;
            ClientSize          = new Size(384, 328);
            Controls.Add(tabControl);
            Controls.Add(btnOk);
            Controls.Add(btnCancel);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox     = false;
            MinimizeBox     = false;
            StartPosition   = FormStartPosition.CenterParent;

            ResumeLayout(false);
            PerformLayout();
        }

        private TabControl tabControl;
        private TabPage    tabConnection, tabPermissions;
        private Label      lblName, lblHost, lblPort, lblApiKey, lblOsVersion, lblDescription;
        private TextBox    txtName, txtHost, txtPort, txtApiKey, txtOsVersion, txtDescription;
        private CheckBox   chkAutomation, chkCapture, chkAi, chkScenes, chkShellRun, chkClients;
        private Label      lblPermNote;
        private Button     btnOk, btnCancel;
    }
}
