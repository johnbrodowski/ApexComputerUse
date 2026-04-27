namespace ApexComputerUse
{
    internal sealed partial class ClientEditForm : Form
    {
        public RemoteClient? Result { get; private set; }

        private readonly string? _existingId;
        private readonly string  _existingCreatedAt;

        internal ClientEditForm(RemoteClient? existing = null)
        {
            InitializeComponent();

            _existingId        = existing?.Id;
            _existingCreatedAt = existing?.CreatedAt ?? DateTimeOffset.UtcNow.ToString("O");

            var perms = existing?.Permissions ?? new ClientPermissions();

            if (existing != null)
            {
                txtName.Text        = existing.Name;
                txtHost.Text        = existing.Host;
                txtPort.Text        = existing.Port.ToString();
                txtApiKey.Text      = existing.ApiKey;
                txtOsVersion.Text   = existing.OsVersion;
                txtDescription.Text = existing.Description;
                Text                = "Edit Client";
            }
            else
            {
                txtPort.Text = "8080";
                Text         = "Add Client";
            }

            // Load permission checkboxes
            chkAutomation.Checked = perms.AllowAutomation;
            chkCapture.Checked    = perms.AllowCapture;
            chkAi.Checked         = perms.AllowAi;
            chkScenes.Checked     = perms.AllowScenes;
            chkShellRun.Checked   = perms.AllowShellRun;
            chkClients.Checked    = perms.AllowClients;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            string name = txtName.Text.Trim();
            string host = txtHost.Text.Trim();

            if (name.Length == 0 || host.Length == 0)
            {
                MessageBox.Show("Name and Host are required.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!int.TryParse(txtPort.Text.Trim(), out int port) || port < 1 || port > 65535)
            {
                MessageBox.Show("Port must be a number between 1 and 65535.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Result = new RemoteClient
            {
                Id          = _existingId ?? ClientIds.New(),
                Name        = name,
                Host        = host,
                Port        = port,
                ApiKey      = txtApiKey.Text.Trim(),
                OsVersion   = txtOsVersion.Text.Trim(),
                Description = txtDescription.Text.Trim(),
                CreatedAt   = _existingCreatedAt,
                Permissions = new ClientPermissions
                {
                    AllowAutomation = chkAutomation.Checked,
                    AllowCapture    = chkCapture.Checked,
                    AllowAi         = chkAi.Checked,
                    AllowScenes     = chkScenes.Checked,
                    AllowShellRun   = chkShellRun.Checked,
                    AllowClients    = chkClients.Checked,
                }
            };

            DialogResult = DialogResult.OK;
        }

        private void btnCancel_Click(object sender, EventArgs e) =>
            DialogResult = DialogResult.Cancel;
    }
}
