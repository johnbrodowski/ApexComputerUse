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
            lblName        = new Label();
            txtName        = new TextBox();
            lblHost        = new Label();
            txtHost        = new TextBox();
            lblPort        = new Label();
            txtPort        = new TextBox();
            lblApiKey      = new Label();
            txtApiKey      = new TextBox();
            lblOsVersion   = new Label();
            txtOsVersion   = new TextBox();
            lblDescription = new Label();
            txtDescription = new TextBox();
            btnOk          = new Button();
            btnCancel      = new Button();

            SuspendLayout();

            // lblName
            lblName.AutoSize = true;
            lblName.Location = new Point(12, 15);
            lblName.Text     = "Name:";

            // txtName
            txtName.Location = new Point(100, 12);
            txtName.Size     = new Size(260, 23);

            // lblHost
            lblHost.AutoSize = true;
            lblHost.Location = new Point(12, 47);
            lblHost.Text     = "Host / IP:";

            // txtHost
            txtHost.Location = new Point(100, 44);
            txtHost.Size     = new Size(260, 23);

            // lblPort
            lblPort.AutoSize = true;
            lblPort.Location = new Point(12, 79);
            lblPort.Text     = "Port:";

            // txtPort
            txtPort.Location = new Point(100, 76);
            txtPort.Size     = new Size(80, 23);

            // lblApiKey
            lblApiKey.AutoSize = true;
            lblApiKey.Location = new Point(12, 111);
            lblApiKey.Text     = "API Key:";

            // txtApiKey
            txtApiKey.Location     = new Point(100, 108);
            txtApiKey.Size         = new Size(260, 23);
            txtApiKey.PasswordChar = '*';

            // lblOsVersion
            lblOsVersion.AutoSize = true;
            lblOsVersion.Location = new Point(12, 143);
            lblOsVersion.Text     = "OS Version:";

            // txtOsVersion
            txtOsVersion.Location = new Point(100, 140);
            txtOsVersion.Size     = new Size(260, 23);

            // lblDescription
            lblDescription.AutoSize = true;
            lblDescription.Location = new Point(12, 175);
            lblDescription.Text     = "Description:";

            // txtDescription
            txtDescription.Location  = new Point(100, 172);
            txtDescription.Size      = new Size(260, 60);
            txtDescription.Multiline = true;

            // btnOk
            btnOk.Location    = new Point(204, 250);
            btnOk.Size        = new Size(75, 28);
            btnOk.Text        = "OK";
            btnOk.Click      += btnOk_Click;

            // btnCancel
            btnCancel.Location    = new Point(285, 250);
            btnCancel.Size        = new Size(75, 28);
            btnCancel.Text        = "Cancel";
            btnCancel.Click      += btnCancel_Click;

            // Form
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode       = AutoScaleMode.Font;
            ClientSize          = new Size(376, 292);
            Controls.Add(lblName);
            Controls.Add(txtName);
            Controls.Add(lblHost);
            Controls.Add(txtHost);
            Controls.Add(lblPort);
            Controls.Add(txtPort);
            Controls.Add(lblApiKey);
            Controls.Add(txtApiKey);
            Controls.Add(lblOsVersion);
            Controls.Add(txtOsVersion);
            Controls.Add(lblDescription);
            Controls.Add(txtDescription);
            Controls.Add(btnOk);
            Controls.Add(btnCancel);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox     = false;
            MinimizeBox     = false;
            StartPosition   = FormStartPosition.CenterParent;

            ResumeLayout(false);
            PerformLayout();
        }

        private Label   lblName, lblHost, lblPort, lblApiKey, lblOsVersion, lblDescription;
        private TextBox txtName, txtHost, txtPort, txtApiKey, txtOsVersion, txtDescription;
        private Button  btnOk, btnCancel;
    }
}
