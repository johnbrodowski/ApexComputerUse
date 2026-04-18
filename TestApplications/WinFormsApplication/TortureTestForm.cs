using System;
using System.Drawing;
using System.Windows.Forms;

namespace WinFormsApplication
{
    public class TortureTestForm : Form
    {
        private readonly ToolTip _toolTip = new ToolTip();
        private ProgressBar _marqueeBar = null!;

        public TortureTestForm()
        {
            Text = "System Configuration Console — UI Torture Test";
            Size = new Size(1150, 850);
            MinimumSize = new Size(900, 700);
            StartPosition = FormStartPosition.CenterScreen;
            BuildUI();
        }

        private void BuildUI()
        {
            SuspendLayout();
            var menu = BuildMenuStrip();
            var toolbar = BuildToolStrip();
            var status = BuildStatusStrip();
            var tabs = new TabControl { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9f) };
            tabs.TabPages.Add(BuildIdentityTab());
            tabs.TabPages.Add(BuildNetworkTab());
            tabs.TabPages.Add(BuildSchedulerTab());
            tabs.TabPages.Add(BuildLayoutTab());
            tabs.TabPages.Add(BuildDataTab());
            tabs.TabPages.Add(BuildLogsTab());
            tabs.TabPages.Add(BuildDisplayTab());
            tabs.TabPages.Add(BuildDatesTab());
            tabs.TabPages.Add(BuildDialogsTab());
            Controls.Add(tabs);
            Controls.Add(status);
            Controls.Add(toolbar);
            Controls.Add(menu);
            MainMenuStrip = menu;
            ResumeLayout();
        }

        // ── MENU ─────────────────────────────────────────────────────────────────
        private MenuStrip BuildMenuStrip()
        {
            var ms = new MenuStrip();
            var file = new ToolStripMenuItem("&File");
            file.DropDownItems.AddRange(new ToolStripItem[]
            {
                new ToolStripMenuItem("&New Profile", null, null, Keys.Control | Keys.N),
                new ToolStripMenuItem("&Open...", null, null, Keys.Control | Keys.O),
                new ToolStripMenuItem("&Save", null, null, Keys.Control | Keys.S),
                new ToolStripMenuItem("Save &As..."),
                new ToolStripSeparator(),
                new ToolStripMenuItem("Import Settings..."),
                new ToolStripMenuItem("Export Settings..."),
                new ToolStripSeparator(),
                new ToolStripMenuItem("E&xit", null, (_, _) => Close()),
            });
            var edit = new ToolStripMenuItem("&Edit");
            var copy = new ToolStripMenuItem("&Copy");
            copy.DropDownItems.AddRange(new ToolStripItem[]
            {
                new ToolStripMenuItem("Copy as &Plain Text"),
                new ToolStripMenuItem("Copy as &JSON"),
                new ToolStripMenuItem("Copy as &XML"),
            });
            edit.DropDownItems.AddRange(new ToolStripItem[]
            {
                new ToolStripMenuItem("&Undo", null, null, Keys.Control | Keys.Z),
                new ToolStripMenuItem("&Redo", null, null, Keys.Control | Keys.Y),
                new ToolStripSeparator(),
                copy,
                new ToolStripMenuItem("&Paste", null, null, Keys.Control | Keys.V),
                new ToolStripSeparator(),
                new ToolStripMenuItem("Select &All", null, null, Keys.Control | Keys.A),
                new ToolStripMenuItem("&Find...", null, null, Keys.Control | Keys.F),
            });
            var view = new ToolStripMenuItem("&View");
            var showGrid = new ToolStripMenuItem("Show &Grid Lines") { Checked = true, CheckOnClick = true };
            var showStatus = new ToolStripMenuItem("Show &Status Bar") { Checked = true, CheckOnClick = true };
            var compact = new ToolStripMenuItem("&Compact Layout") { CheckOnClick = true };
            view.DropDownItems.AddRange(new ToolStripItem[]
            {
                showGrid, showStatus, compact,
                new ToolStripSeparator(),
                new ToolStripMenuItem("&Refresh", null, null, Keys.F5),
                new ToolStripMenuItem("Reset &Layout"),
            });
            var tools = new ToolStripMenuItem("&Tools");
            var lang = new ToolStripMenuItem("&Language");
            lang.DropDownItems.AddRange(new ToolStripItem[]
            {
                new ToolStripMenuItem("English (US)") { Checked = true, CheckOnClick = true },
                new ToolStripMenuItem("Français") { CheckOnClick = true },
                new ToolStripMenuItem("Deutsch") { CheckOnClick = true },
                new ToolStripMenuItem("日本語") { CheckOnClick = true },
            });
            tools.DropDownItems.AddRange(new ToolStripItem[]
            {
                new ToolStripMenuItem("&Options..."),
                new ToolStripMenuItem("&Diagnostics"),
                lang,
                new ToolStripSeparator(),
                new ToolStripMenuItem("&Plugins..."),
            });
            var help = new ToolStripMenuItem("&Help");
            help.DropDownItems.AddRange(new ToolStripItem[]
            {
                new ToolStripMenuItem("&Documentation", null, null, Keys.F1),
                new ToolStripMenuItem("&Release Notes"),
                new ToolStripSeparator(),
                new ToolStripMenuItem("Check for &Updates..."),
                new ToolStripMenuItem("&About"),
            });
            ms.Items.AddRange(new ToolStripItem[] { file, edit, view, tools, help });
            return ms;
        }

        // ── TOOLBAR ──────────────────────────────────────────────────────────────
        private ToolStrip BuildToolStrip()
        {
            var ts = new ToolStrip();
            var envCombo = new ToolStripComboBox();
            envCombo.Items.AddRange(new object[] { "Development", "Staging", "Production", "DR" });
            envCombo.SelectedIndex = 0;
            var connectBtn = new ToolStripSplitButton("Connect");
            connectBtn.DropDownItems.AddRange(new ToolStripItem[]
            {
                new ToolStripMenuItem("Connect via VPN"),
                new ToolStripMenuItem("Connect via Proxy"),
                new ToolStripMenuItem("Connect Direct"),
            });
            var modeBtn = new ToolStripDropDownButton("Mode: Normal");
            modeBtn.DropDownItems.AddRange(new ToolStripItem[]
            {
                new ToolStripMenuItem("Normal Mode"),
                new ToolStripMenuItem("Read-Only Mode"),
                new ToolStripMenuItem("Maintenance Mode"),
                new ToolStripMenuItem("Debug Mode"),
            });
            var searchBox = new ToolStripTextBox { Text = "Search...", Size = new Size(160, 25) };
            var statusLabel = new ToolStripLabel("● Connected") { ForeColor = Color.Green };
            var syncProgress = new ToolStripProgressBar { Value = 65, Width = 80 };
            ts.Items.AddRange(new ToolStripItem[]
            {
                new ToolStripButton("New"),
                new ToolStripButton("Open"),
                new ToolStripButton("Save"),
                new ToolStripSeparator(),
                new ToolStripLabel("Environment:"),
                envCombo,
                new ToolStripSeparator(),
                connectBtn, modeBtn,
                new ToolStripSeparator(),
                searchBox,
                new ToolStripSeparator(),
                statusLabel,
                new ToolStripLabel("Sync:"),
                syncProgress,
            });
            return ts;
        }

        // ── STATUS STRIP ─────────────────────────────────────────────────────────
        private StatusStrip BuildStatusStrip()
        {
            var ss = new StatusStrip();
            var alertDrop = new ToolStripDropDownButton("0 Alerts");
            alertDrop.DropDownItems.Add(new ToolStripMenuItem("No active alerts"));
            ss.Items.AddRange(new ToolStripItem[]
            {
                new ToolStripStatusLabel("Ready"),
                new ToolStripStatusLabel("|"),
                new ToolStripStatusLabel("Profile: Default"),
                new ToolStripStatusLabel { Spring = true },
                new ToolStripStatusLabel("User: Administrator"),
                new ToolStripStatusLabel("|"),
                new ToolStripStatusLabel(DateTime.Now.ToString("HH:mm:ss")),
                new ToolStripStatusLabel("|"),
                alertDrop,
                new ToolStripProgressBar { Width = 80, Value = 65 },
            });
            return ss;
        }

        // ── TAB 1: IDENTITY ───────────────────────────────────────────────────────
        private TabPage BuildIdentityTab()
        {
            var page = new TabPage("Identity") { AutoScroll = true, Padding = new Padding(8) };
            var tl = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 4, Padding = new Padding(4) };
            tl.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            tl.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            tl.Controls.Add(Lbl("First Name:"), 0, 0);
            tl.Controls.Add(new TextBox { Text = "John", Dock = DockStyle.Fill }, 1, 0);
            tl.Controls.Add(Lbl("Last Name:"), 2, 0);
            tl.Controls.Add(new TextBox { Text = "Doe", Dock = DockStyle.Fill }, 3, 0);

            tl.Controls.Add(Lbl("Employee ID:"), 0, 1);
            tl.Controls.Add(new MaskedTextBox { Mask = "EMP-00000", Dock = DockStyle.Fill }, 1, 1);
            tl.Controls.Add(Lbl("Department:"), 2, 1);
            var dept = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
            dept.Items.AddRange(new object[] { "Engineering", "Finance", "HR", "IT Operations", "Legal", "Marketing", "Sales" });
            dept.SelectedIndex = 3;
            tl.Controls.Add(dept, 3, 1);

            tl.Controls.Add(Lbl("Phone:"), 0, 2);
            tl.Controls.Add(new MaskedTextBox { Mask = "(999) 000-0000", Dock = DockStyle.Fill }, 1, 2);
            tl.Controls.Add(Lbl("Extension:"), 2, 2);
            tl.Controls.Add(new MaskedTextBox { Mask = "0000", Dock = DockStyle.Fill }, 3, 2);

            tl.Controls.Add(Lbl("Email:"), 0, 3);
            var email = new TextBox { Text = "john.doe@company.com", Dock = DockStyle.Fill };
            _toolTip.SetToolTip(email, "Corporate email address");
            tl.Controls.Add(email, 1, 3);
            tl.Controls.Add(Lbl("Username:"), 2, 3);
            tl.Controls.Add(new TextBox { Text = "jdoe", Dock = DockStyle.Fill }, 3, 3);

            tl.Controls.Add(Lbl("Password:"), 0, 4);
            tl.Controls.Add(new TextBox { PasswordChar = '●', Dock = DockStyle.Fill }, 1, 4);
            tl.Controls.Add(Lbl("Confirm:"), 2, 4);
            tl.Controls.Add(new TextBox { PasswordChar = '●', Dock = DockStyle.Fill }, 3, 4);

            tl.Controls.Add(Lbl("Hire Date:"), 0, 5);
            tl.Controls.Add(new DateTimePicker { Format = DateTimePickerFormat.Short, Dock = DockStyle.Fill }, 1, 5);
            tl.Controls.Add(Lbl("Annual Salary:"), 2, 5);
            tl.Controls.Add(new NumericUpDown { Minimum = 0, Maximum = 999999, Value = 85000, DecimalPlaces = 2, ThousandsSeparator = true, Dock = DockStyle.Fill }, 3, 5);

            tl.Controls.Add(Lbl("Start Time:"), 0, 6);
            tl.Controls.Add(new DateTimePicker { Format = DateTimePickerFormat.Time, ShowUpDown = true, Dock = DockStyle.Fill }, 1, 6);
            tl.Controls.Add(Lbl("Years of Service:"), 2, 6);
            tl.Controls.Add(new NumericUpDown { Minimum = 0, Maximum = 50, Value = 5, Dock = DockStyle.Fill }, 3, 6);

            tl.Controls.Add(Lbl("Access Level:"), 0, 7);
            tl.Controls.Add(new TrackBar { Minimum = 1, Maximum = 5, Value = 3, TickFrequency = 1, Dock = DockStyle.Fill }, 1, 7);
            tl.Controls.Add(Lbl("Status:"), 2, 7);
            var status = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
            status.Items.AddRange(new object[] { "Active", "On Leave", "Suspended", "Terminated" });
            status.SelectedIndex = 0;
            tl.Controls.Add(status, 3, 7);

            tl.Controls.Add(Lbl("Flags:"), 0, 8);
            var flagPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
            flagPanel.Controls.AddRange(new Control[]
            {
                new CheckBox { Text = "AD Sync", AutoSize = true, Checked = true },
                new CheckBox { Text = "MFA Enabled", AutoSize = true, Checked = true },
                new CheckBox { Text = "VPN Access", AutoSize = true },
                new CheckBox { Text = "Remote Desktop", AutoSize = true },
                new CheckBox { Text = "Admin Override", AutoSize = true },
            });
            tl.SetColumnSpan(flagPanel, 3);
            tl.Controls.Add(flagPanel, 1, 8);

            tl.Controls.Add(Lbl("Notes:"), 0, 9);
            var notes = new RichTextBox { Height = 80, Dock = DockStyle.Fill, Text = "Senior engineer. Approved for production deployments.\nLast reviewed: Q3 2025." };
            tl.SetColumnSpan(notes, 3);
            tl.Controls.Add(notes, 1, 9);

            tl.Controls.Add(Lbl("Title:"), 0, 10);
            var titleSpin = new DomainUpDown { Dock = DockStyle.Fill };
            titleSpin.Items.AddRange(new object[] { "Mr.", "Ms.", "Mrs.", "Dr.", "Prof.", "Mx." });
            titleSpin.SelectedIndex = 0;
            tl.Controls.Add(titleSpin, 1, 10);
            tl.Controls.Add(Lbl("Location:"), 2, 10);
            var location = new ComboBox { Dock = DockStyle.Fill };
            location.Items.AddRange(new object[] { "New York", "London", "Tokyo", "Sydney", "Berlin", "Remote" });
            location.Text = "New York";
            tl.Controls.Add(location, 3, 10);

            tl.Controls.Add(Lbl("UUID:"), 0, 11);
            var uuid = new TextBox { Text = "f47ac10b-58cc-4372-a567-0e02b2c3d479", ReadOnly = true, BackColor = SystemColors.Control, Dock = DockStyle.Fill };
            tl.SetColumnSpan(uuid, 3);
            tl.Controls.Add(uuid, 1, 11);

            page.Controls.Add(tl);
            return page;
        }

        // ── TAB 2: NETWORK ────────────────────────────────────────────────────────
        private TabPage BuildNetworkTab()
        {
            var page = new TabPage("Network") { AutoScroll = true, Padding = new Padding(8) };
            var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterDistance = 420 };

            // Left
            var leftFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(4) };

            var connGroup = new GroupBox { Text = "Primary Connection", Dock = DockStyle.Top, Height = 220, Padding = new Padding(6) };
            var connTable = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            connTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            connTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            connTable.Controls.Add(Lbl("Protocol:"), 0, 0);
            var proto = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
            proto.Items.AddRange(new object[] { "HTTPS", "HTTP", "WebSocket", "gRPC", "AMQP" });
            proto.SelectedIndex = 0;
            connTable.Controls.Add(proto, 1, 0);
            connTable.Controls.Add(Lbl("Hostname:"), 0, 1);
            connTable.Controls.Add(new TextBox { Text = "api.company.internal", Dock = DockStyle.Fill }, 1, 1);
            connTable.Controls.Add(Lbl("Port:"), 0, 2);
            connTable.Controls.Add(new NumericUpDown { Minimum = 1, Maximum = 65535, Value = 443, Dock = DockStyle.Fill }, 1, 2);
            connTable.Controls.Add(Lbl("Timeout (s):"), 0, 3);
            connTable.Controls.Add(new NumericUpDown { Minimum = 1, Maximum = 300, Value = 30, Dock = DockStyle.Fill }, 1, 3);
            connTable.Controls.Add(Lbl("Retries:"), 0, 4);
            connTable.Controls.Add(new NumericUpDown { Minimum = 0, Maximum = 10, Value = 3, Dock = DockStyle.Fill }, 1, 4);
            connTable.Controls.Add(Lbl("Auth Token:"), 0, 5);
            connTable.Controls.Add(new TextBox { PasswordChar = '●', Text = "sk-live-abc123", Dock = DockStyle.Fill }, 1, 5);
            connGroup.Controls.Add(connTable);
            leftFlow.Controls.Add(connGroup);

            var tlsGroup = new GroupBox { Text = "TLS / Security", Dock = DockStyle.Top, Height = 140, Padding = new Padding(6) };
            var tlsFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown };
            tlsFlow.Controls.AddRange(new Control[]
            {
                new CheckBox { Text = "Verify Server Certificate", Checked = true, AutoSize = true },
                new CheckBox { Text = "Client Certificate Auth", AutoSize = true },
                new CheckBox { Text = "Mutual TLS (mTLS)", AutoSize = true },
                new CheckBox { Text = "Certificate Pinning", AutoSize = true },
                new CheckBox { Text = "HSTS Enforcement", Checked = true, AutoSize = true },
            });
            tlsGroup.Controls.Add(tlsFlow);
            leftFlow.Controls.Add(tlsGroup);

            var modeGroup = new GroupBox { Text = "Connection Mode", Dock = DockStyle.Top, Height = 110, Padding = new Padding(6) };
            var modeFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown };
            modeFlow.Controls.AddRange(new Control[]
            {
                new RadioButton { Text = "Direct", Checked = true, AutoSize = true },
                new RadioButton { Text = "Via Proxy", AutoSize = true },
                new RadioButton { Text = "Via VPN Tunnel", AutoSize = true },
                new RadioButton { Text = "SSH Tunnel", AutoSize = true },
            });
            modeGroup.Controls.Add(modeFlow);
            leftFlow.Controls.Add(modeGroup);
            split.Panel1.Controls.Add(leftFlow);

            // Right
            var rightFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(4) };

            var bwGroup = new GroupBox { Text = "Bandwidth Limits", Dock = DockStyle.Top, Height = 120, Padding = new Padding(6) };
            var bwTable = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            bwTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            bwTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            bwTable.Controls.Add(Lbl("Upload (Mbps):"), 0, 0);
            bwTable.Controls.Add(new TrackBar { Minimum = 0, Maximum = 1000, Value = 100, TickFrequency = 100, Dock = DockStyle.Fill }, 1, 0);
            bwTable.Controls.Add(Lbl("Download (Mbps):"), 0, 1);
            bwTable.Controls.Add(new TrackBar { Minimum = 0, Maximum = 1000, Value = 500, TickFrequency = 100, Dock = DockStyle.Fill }, 1, 1);
            bwGroup.Controls.Add(bwTable);
            rightFlow.Controls.Add(bwGroup);

            var prioGroup = new GroupBox { Text = "Endpoint Priority (Multi-select)", Dock = DockStyle.Top, Height = 160, Padding = new Padding(6) };
            var prioList = new ListBox { Dock = DockStyle.Fill, SelectionMode = SelectionMode.MultiExtended };
            prioList.Items.AddRange(new object[]
            {
                "api.company.internal (Primary)",
                "api-eu.company.internal (Failover EU)",
                "api-ap.company.internal (Failover APAC)",
                "10.0.0.42:8080 (Local Dev)",
                "localhost:3000 (Mock)",
            });
            prioList.SelectedIndex = 0;
            prioGroup.Controls.Add(prioList);
            rightFlow.Controls.Add(prioGroup);

            var featGroup = new GroupBox { Text = "Enabled Features", Dock = DockStyle.Top, Height = 200, Padding = new Padding(6) };
            var featList = new CheckedListBox { Dock = DockStyle.Fill };
            featList.Items.Add("Auto-reconnect", true);
            featList.Items.Add("Compression (gzip)", true);
            featList.Items.Add("Keep-Alive", true);
            featList.Items.Add("HTTP/2 Multiplexing", false);
            featList.Items.Add("Connection Pooling", true);
            featList.Items.Add("Circuit Breaker", false);
            featList.Items.Add("Rate Limiting", false);
            featList.Items.Add("Request Signing (HMAC)", false);
            featGroup.Controls.Add(featList);
            rightFlow.Controls.Add(featGroup);

            split.Panel2.Controls.Add(rightFlow);
            page.Controls.Add(split);
            return page;
        }

        // ── TAB 3: SCHEDULER ─────────────────────────────────────────────────────
        private TabPage BuildSchedulerTab()
        {
            var page = new TabPage("Scheduler") { Padding = new Padding(8) };
            var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 320 };

            var topTable = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4 };
            topTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            topTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            topTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            topTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            topTable.Controls.Add(Lbl("Job Name:"), 0, 0);
            topTable.Controls.Add(new TextBox { Text = "nightly-backup", Dock = DockStyle.Fill }, 1, 0);
            topTable.Controls.Add(Lbl("Job Type:"), 2, 0);
            var jobType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
            jobType.Items.AddRange(new object[] { "Backup", "Sync", "Report", "Cleanup", "Notification", "Health Check" });
            jobType.SelectedIndex = 0;
            topTable.Controls.Add(jobType, 3, 0);

            topTable.Controls.Add(Lbl("Frequency:"), 0, 1);
            var freq = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
            freq.Items.AddRange(new object[] { "Every Minute", "Hourly", "Daily", "Weekly", "Monthly", "Custom Cron" });
            freq.SelectedIndex = 2;
            topTable.Controls.Add(freq, 1, 1);
            topTable.Controls.Add(Lbl("Start Date:"), 2, 1);
            topTable.Controls.Add(new DateTimePicker { Format = DateTimePickerFormat.Short, Dock = DockStyle.Fill }, 3, 1);

            topTable.Controls.Add(Lbl("Start Time:"), 0, 2);
            topTable.Controls.Add(new DateTimePicker { Format = DateTimePickerFormat.Time, ShowUpDown = true, Dock = DockStyle.Fill }, 1, 2);
            topTable.Controls.Add(Lbl("End Date:"), 2, 2);
            topTable.Controls.Add(new DateTimePicker { Format = DateTimePickerFormat.Short, Dock = DockStyle.Fill }, 3, 2);

            topTable.Controls.Add(Lbl("Max Runtime (min):"), 0, 3);
            topTable.Controls.Add(new NumericUpDown { Minimum = 1, Maximum = 1440, Value = 60, Dock = DockStyle.Fill }, 1, 3);
            topTable.Controls.Add(Lbl("Priority:"), 2, 3);
            topTable.Controls.Add(new TrackBar { Minimum = 1, Maximum = 10, Value = 5, TickFrequency = 1, Dock = DockStyle.Fill }, 3, 3);

            topTable.Controls.Add(Lbl("Cron Expression:"), 0, 4);
            var cron = new MaskedTextBox { Text = "0 2 * * *", Dock = DockStyle.Fill };
            topTable.SetColumnSpan(cron, 3);
            topTable.Controls.Add(cron, 1, 4);

            var optFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
            optFlow.Controls.AddRange(new Control[]
            {
                new CheckBox { Text = "Enabled", Checked = true, AutoSize = true },
                new CheckBox { Text = "Run on Missed Schedule", AutoSize = true },
                new CheckBox { Text = "Notify on Success", Checked = true, AutoSize = true },
                new CheckBox { Text = "Notify on Failure", Checked = true, AutoSize = true },
                new CheckBox { Text = "Retry on Failure", AutoSize = true },
            });
            topTable.SetColumnSpan(optFlow, 4);
            topTable.Controls.Add(optFlow, 0, 5);

            var cal = new MonthCalendar { MaxSelectionCount = 14, ShowWeekNumbers = true, ShowTodayCircle = true };
            topTable.SetColumnSpan(cal, 2);
            topTable.Controls.Add(cal, 2, 6);

            split.Panel1.Controls.Add(topTable);

            // Bottom job list
            var lv = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true, CheckBoxes = true };
            lv.Columns.AddRange(new[]
            {
                new ColumnHeader { Text = "Job Name", Width = 160 },
                new ColumnHeader { Text = "Type", Width = 100 },
                new ColumnHeader { Text = "Frequency", Width = 100 },
                new ColumnHeader { Text = "Last Run", Width = 140 },
                new ColumnHeader { Text = "Next Run", Width = 140 },
                new ColumnHeader { Text = "Status", Width = 90 },
                new ColumnHeader { Text = "Duration", Width = 80 },
            });
            var jobData = new[]
            {
                new[] { "nightly-backup",  "Backup",       "Daily",        "2026-03-24 02:00", "2026-03-25 02:00", "Success", "14m 32s" },
                new[] { "weekly-report",   "Report",       "Weekly",       "2026-03-22 06:00", "2026-03-29 06:00", "Success", "2m 11s"  },
                new[] { "hourly-sync",     "Sync",         "Hourly",       "2026-03-25 11:00", "2026-03-25 12:00", "Running", "0m 43s"  },
                new[] { "log-cleanup",     "Cleanup",      "Daily",        "2026-03-24 03:00", "2026-03-25 03:00", "Failed",  "0m 02s"  },
                new[] { "health-check",    "Health Check", "Every Minute", "2026-03-25 11:03", "2026-03-25 11:04", "Success", "0m 01s"  },
                new[] { "monthly-invoice", "Notification", "Monthly",      "2026-03-01 08:00", "2026-04-01 08:00", "Success", "0m 48s"  },
            };
            foreach (var job in jobData)
            {
                var item = new ListViewItem(job[0]) { Checked = job[5] != "Failed" };
                for (int i = 1; i < job.Length; i++) item.SubItems.Add(job[i]);
                if (job[5] == "Failed") item.ForeColor = Color.Red;
                else if (job[5] == "Running") item.ForeColor = Color.Blue;
                lv.Items.Add(item);
            }
            split.Panel2.Controls.Add(lv);
            page.Controls.Add(split);
            return page;
        }

        // ── TAB 4: LAYOUT SHOWCASE ────────────────────────────────────────────────
        private TabPage BuildLayoutTab()
        {
            var page = new TabPage("Layout") { Padding = new Padding(4) };
            var outerSplit = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterDistance = 380 };

            // Left: FlowLayoutPanel + TableLayoutPanel
            var flowGroup = new GroupBox { Text = "FlowLayoutPanel — Action Buttons", Dock = DockStyle.Top, Height = 160 };
            var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, Padding = new Padding(4) };
            foreach (var label in new[] { "Apply", "Reset", "Preview", "Export", "Import", "Validate", "Deploy", "Rollback", "Restart", "Shutdown", "Refresh", "Cancel" })
                flow.Controls.Add(new Button { Text = label, Width = 80, Height = 30, Margin = new Padding(3) });
            flowGroup.Controls.Add(flow);

            var tableGroup = new GroupBox { Text = "TableLayoutPanel — Settings Grid", Dock = DockStyle.Fill };
            var tl = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, CellBorderStyle = TableLayoutPanelCellBorderStyle.Single };
            tl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3f));
            tl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3f));
            tl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3f));
            tl.Controls.Add(BoldLabel("Setting"), 0, 0);
            tl.Controls.Add(BoldLabel("Value"), 1, 0);
            tl.Controls.Add(BoldLabel("Action"), 2, 0);
            var settingNames = new[] { "Max Connections", "Cache Size (MB)", "Log Level", "Worker Threads" };
            Control[] valueControls =
            {
                new NumericUpDown { Value = 100, Dock = DockStyle.Fill },
                new NumericUpDown { Maximum = 9999, Value = 512, Dock = DockStyle.Fill },
                new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill },
                new TrackBar { Minimum = 1, Maximum = 32, Value = 8, Dock = DockStyle.Fill },
            };
            ((ComboBox)valueControls[2]).Items.AddRange(new object[] { "Debug", "Info", "Warn", "Error" });
            ((ComboBox)valueControls[2]).SelectedIndex = 1;
            for (int r = 0; r < settingNames.Length; r++)
            {
                tl.Controls.Add(new Label { Text = settingNames[r], Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, r + 1);
                tl.Controls.Add(valueControls[r], 1, r + 1);
                tl.Controls.Add(new Button { Text = "Apply", Dock = DockStyle.Fill }, 2, r + 1);
            }
            tableGroup.Controls.Add(tl);
            var leftPanel = new Panel { Dock = DockStyle.Fill };
            leftPanel.Controls.Add(tableGroup);
            leftPanel.Controls.Add(flowGroup);
            outerSplit.Panel1.Controls.Add(leftPanel);

            // Right: vertical sliders, scroll bars, progress bars
            var rightFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, AutoScroll = true, Padding = new Padding(4) };

            var vSliderGroup = new GroupBox { Text = "Vertical TrackBars", Width = 210, Height = 230 };
            var vFlow = new FlowLayoutPanel { Dock = DockStyle.Fill };
            var vLabels = new[] { "CPU", "RAM", "Disk", "Net" };
            var vVals = new[] { 72, 55, 88, 34 };
            for (int i = 0; i < vLabels.Length; i++)
            {
                var col = new Panel { Width = 44, Height = 195 };
                col.Controls.Add(new TrackBar { Orientation = Orientation.Vertical, Minimum = 0, Maximum = 100, Value = vVals[i], TickFrequency = 10, Width = 40, Height = 160, Location = new Point(2, 0) });
                col.Controls.Add(new Label { Text = vLabels[i], Width = 40, Height = 18, Location = new Point(2, 165), TextAlign = ContentAlignment.MiddleCenter });
                vFlow.Controls.Add(col);
            }
            vSliderGroup.Controls.Add(vFlow);
            rightFlow.Controls.Add(vSliderGroup);

            var scrollGroup = new GroupBox { Text = "Scroll Bars", Width = 230, Height = 230 };
            var scPanel = new Panel { Dock = DockStyle.Fill };
            scPanel.Controls.Add(new HScrollBar { Left = 8, Top = 20, Width = 200, Minimum = 0, Maximum = 100, Value = 25 });
            scPanel.Controls.Add(new HScrollBar { Left = 8, Top = 50, Width = 200, Minimum = 0, Maximum = 100, Value = 65 });
            scPanel.Controls.Add(new HScrollBar { Left = 8, Top = 80, Width = 200, Minimum = 0, Maximum = 200, Value = 100 });
            scPanel.Controls.Add(new VScrollBar { Left = 8, Top = 110, Width = 20, Height = 100, Minimum = 0, Maximum = 100, Value = 40 });
            scPanel.Controls.Add(new VScrollBar { Left = 38, Top = 110, Width = 20, Height = 100, Minimum = 0, Maximum = 100, Value = 80 });
            scrollGroup.Controls.Add(scPanel);
            rightFlow.Controls.Add(scrollGroup);

            var pbGroup = new GroupBox { Text = "Progress Bars", Width = 230, Height = 230 };
            var pbPanel = new Panel { Dock = DockStyle.Fill };
            var pbLabels = new[] { "CPU:", "RAM:", "Disk:", "Net:", "Sync:" };
            var pbVals = new[] { 72, 55, 88, 34, 65 };
            for (int i = 0; i < pbLabels.Length; i++)
            {
                pbPanel.Controls.Add(new Label { Text = pbLabels[i], Left = 8, Top = 20 + i * 36, Width = 40, TextAlign = ContentAlignment.MiddleRight });
                pbPanel.Controls.Add(new ProgressBar { Left = 52, Top = 24 + i * 36, Width = 160, Height = 16, Value = pbVals[i] });
            }
            pbGroup.Controls.Add(pbPanel);
            rightFlow.Controls.Add(pbGroup);

            outerSplit.Panel2.Controls.Add(rightFlow);
            page.Controls.Add(outerSplit);
            return page;
        }

        // ── TAB 5: DATA ───────────────────────────────────────────────────────────
        private TabPage BuildDataTab()
        {
            var page = new TabPage("Data") { Padding = new Padding(4) };
            var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 320 };

            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                RowHeadersWidth = 30,
                AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.AliceBlue },
            };
            grid.Columns.AddRange(new DataGridViewColumn[]
            {
                new DataGridViewTextBoxColumn   { HeaderText = "Service",     Width = 150 },
                new DataGridViewTextBoxColumn   { HeaderText = "Host",        Width = 150 },
                new DataGridViewTextBoxColumn   { HeaderText = "Port",        Width = 60  },
                new DataGridViewCheckBoxColumn  { HeaderText = "Enabled",     Width = 70  },
                new DataGridViewComboBoxColumn  { HeaderText = "Environment", Width = 110, Items = { "Dev", "Staging", "Production" } },
                new DataGridViewTextBoxColumn   { HeaderText = "Latency (ms)", Width = 100 },
                new DataGridViewTextBoxColumn   { HeaderText = "Last Seen",   Width = 140 },
                new DataGridViewButtonColumn    { HeaderText = "Action", Text = "Ping", UseColumnTextForButtonValue = true, Width = 70 },
                new DataGridViewLinkColumn      { HeaderText = "Logs",   Text = "View", UseColumnTextForLinkValue = true,   Width = 60 },
            });
            var gridRows = new object[][]
            {
                new object[] { "AuthService",      "auth.internal",    8443, true,  "Production", 12,  "2026-03-25 11:00" },
                new object[] { "UserAPI",          "users.internal",   8080, true,  "Production", 8,   "2026-03-25 11:01" },
                new object[] { "PaymentGateway",   "pay.internal",     443,  true,  "Production", 145, "2026-03-25 10:58" },
                new object[] { "NotificationSvc",  "notify.internal",  5672, false, "Staging",    22,  "2026-03-24 16:00" },
                new object[] { "ReportingEngine",  "reports.internal", 9090, true,  "Dev",        55,  "2026-03-25 09:30" },
                new object[] { "CacheService",     "cache.internal",   6379, true,  "Production", 2,   "2026-03-25 11:02" },
                new object[] { "SearchIndex",      "search.internal",  9200, true,  "Production", 18,  "2026-03-25 11:01" },
            };
            foreach (var r in gridRows)
            {
                var row = new DataGridViewRow();
                row.CreateCells(grid, r);
                grid.Rows.Add(row);
            }
            split.Panel1.Controls.Add(grid);

            var propLabel = new Label { Text = "PropertyGrid — live configuration object:", Dock = DockStyle.Top, Height = 22, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            var propGrid = new PropertyGrid { Dock = DockStyle.Fill, SelectedObject = new SampleConfig() };
            split.Panel2.Controls.Add(propGrid);
            split.Panel2.Controls.Add(propLabel);

            page.Controls.Add(split);
            return page;
        }

        // ── TAB 6: LOGS ───────────────────────────────────────────────────────────
        private TabPage BuildLogsTab()
        {
            var page = new TabPage("Logs") { Padding = new Padding(4) };
            var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterDistance = 200 };

            var tv = new TreeView { Dock = DockStyle.Fill, CheckBoxes = true };
            var root = tv.Nodes.Add("All Services");
            var auth = root.Nodes.Add("AuthService");
            auth.Nodes.Add("Login").Checked = true;
            auth.Nodes.Add("Token Refresh").Checked = true;
            auth.Nodes.Add("Logout");
            var api = root.Nodes.Add("UserAPI");
            api.Nodes.Add("GET /users").Checked = true;
            api.Nodes.Add("POST /users").Checked = true;
            api.Nodes.Add("DELETE /users");
            var pay = root.Nodes.Add("PaymentGateway");
            pay.Nodes.Add("Charge").Checked = true;
            pay.Nodes.Add("Refund");
            pay.Nodes.Add("Webhook").Checked = true;
            var sys = root.Nodes.Add("System");
            sys.Nodes.Add("Startup").Checked = true;
            sys.Nodes.Add("Shutdown").Checked = true;
            sys.Nodes.Add("Health Check").Checked = true;
            sys.Nodes.Add("GC Events");
            root.ExpandAll();
            split.Panel1.Controls.Add(tv);

            var rightPanel = new Panel { Dock = DockStyle.Fill };
            var filterFlow = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 36, Padding = new Padding(2) };
            var levelCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 };
            levelCombo.Items.AddRange(new object[] { "All Levels", "DEBUG", "INFO", "WARN", "ERROR" });
            levelCombo.SelectedIndex = 0;
            var clearBtn = new Button { Text = "Clear", Width = 60, Height = 24 };
            filterFlow.Controls.AddRange(new Control[]
            {
                levelCombo,
                new TextBox { Width = 180, PlaceholderText = "Filter text..." },
                new CheckBox { Text = "Auto-scroll", Checked = true, AutoSize = true },
                new CheckBox { Text = "Word wrap", AutoSize = true },
                clearBtn,
            });
            var logBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 8.5f),
                ReadOnly = true,
                BackColor = Color.FromArgb(18, 18, 18),
                ForeColor = Color.LightGray,
                WordWrap = false,
                Text = string.Join(Environment.NewLine, new[]
                {
                    "[2026-03-25 11:00:01.123] [INFO ] [AuthService  ] User jdoe logged in from 10.0.0.5",
                    "[2026-03-25 11:00:01.445] [DEBUG] [AuthService  ] JWT issued, exp=3600s",
                    "[2026-03-25 11:00:02.001] [INFO ] [UserAPI      ] GET /users/42 → 200 OK (8ms)",
                    "[2026-03-25 11:00:03.553] [INFO ] [PaymentGateway] Charge $149.99 → txn_abc123 SUCCESS",
                    "[2026-03-25 11:00:04.110] [WARN ] [CacheService ] Cache miss rate 42% (threshold: 20%)",
                    "[2026-03-25 11:00:05.230] [ERROR] [NotifySvc    ] AMQP connection refused: 10.0.0.99:5672",
                    "[2026-03-25 11:00:05.231] [ERROR] [NotifySvc    ] Retry 1/3 failed — backing off 5s",
                    "[2026-03-25 11:00:06.778] [INFO ] [System       ] Health check passed (5/5 services)",
                    "[2026-03-25 11:00:07.002] [INFO ] [UserAPI      ] POST /users → 201 Created (22ms)",
                    "[2026-03-25 11:00:08.400] [DEBUG] [SearchIndex  ] Indexed 128 docs, 0 errors",
                    "[2026-03-25 11:00:09.001] [INFO ] [ReportEngine ] Scheduled report queued: weekly-summary",
                    "[2026-03-25 11:00:10.339] [WARN ] [PaymentGateway] Latency spike: 445ms (avg: 145ms)",
                    "[2026-03-25 11:00:11.001] [INFO ] [AuthService  ] Token refresh for jdoe",
                    "[2026-03-25 11:00:12.550] [INFO ] [System       ] GC Gen2 collect: 128MB freed",
                }),
            };
            clearBtn.Click += (_, _) => logBox.Clear();
            rightPanel.Controls.Add(logBox);
            rightPanel.Controls.Add(filterFlow);
            split.Panel2.Controls.Add(rightPanel);

            page.Controls.Add(split);
            return page;
        }

        // ── TAB 7: DISPLAY ────────────────────────────────────────────────────────
        private TabPage BuildDisplayTab()
        {
            var page = new TabPage("Display") { AutoScroll = true, Padding = new Padding(8) };
            var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, AutoScroll = true, Padding = new Padding(4) };

            // Label styles
            var labelGroup = new GroupBox { Text = "Label Styles", Width = 320, Height = 230 };
            var lp = new Panel { Dock = DockStyle.Fill };
            int y = 16;
            var labelDefs = new (string text, Font font, Color fg)[]
            {
                ("Normal Label",  new Font("Segoe UI", 9),                          SystemColors.ControlText),
                ("Bold Label",    new Font("Segoe UI", 9, FontStyle.Bold),           SystemColors.ControlText),
                ("Italic Label",  new Font("Segoe UI", 9, FontStyle.Italic),         Color.Gray),
                ("Large Heading", new Font("Segoe UI", 14, FontStyle.Bold),          Color.DarkBlue),
                ("Warning Text",  new Font("Segoe UI", 9, FontStyle.Bold),           Color.DarkOrange),
                ("Error Text",    new Font("Segoe UI", 9, FontStyle.Bold),           Color.Red),
                ("Success Text",  new Font("Segoe UI", 9, FontStyle.Bold),           Color.Green),
                ("Strikethrough", new Font("Segoe UI", 9, FontStyle.Strikeout),      Color.Gray),
                ("Underlined",    new Font("Segoe UI", 9, FontStyle.Underline),      Color.Navy),
            };
            foreach (var (text, font, fg) in labelDefs)
            {
                lp.Controls.Add(new Label { Text = text, Font = font, ForeColor = fg, Left = 8, Top = y, Width = 290, AutoSize = false, Height = 22 });
                y += 24;
            }
            labelGroup.Controls.Add(lp);
            flow.Controls.Add(labelGroup);

            // LinkLabel
            var llGroup = new GroupBox { Text = "LinkLabel", Width = 300, Height = 150 };
            var llPanel = new Panel { Dock = DockStyle.Fill };
            var ll2 = new LinkLabel { Text = "GitHub — ApexUIBridge — MIT License", Left = 8, Top = 50, AutoSize = true };
            ll2.Links.Clear();
            ll2.Links.Add(9, 13);
            llPanel.Controls.AddRange(new Control[]
            {
                new LinkLabel { Text = "Visit our documentation portal", Left = 8, Top = 20, AutoSize = true },
                ll2,
                new LinkLabel { Text = "Support: support@company.com",   Left = 8, Top = 80, AutoSize = true },
                new LinkLabel { Text = "Disabled link example", Left = 8, Top = 110, AutoSize = true, Enabled = false },
            });
            llGroup.Controls.Add(llPanel);
            flow.Controls.Add(llGroup);

            // PictureBox SizeMode variants
            var pbGroup = new GroupBox { Text = "PictureBox SizeMode Variants", Width = 580, Height = 180 };
            var pbFlow = new FlowLayoutPanel { Dock = DockStyle.Fill };
            var bmp = new Bitmap(100, 80);
            using (var g = Graphics.FromImage(bmp))
            {
                g.FillRectangle(Brushes.SteelBlue, 0, 0, 100, 80);
                g.FillRectangle(Brushes.Gold, 20, 20, 60, 40);
                g.FillEllipse(Brushes.White, 35, 30, 30, 20);
                g.DrawString("IMG", new Font("Arial", 8), Brushes.Black, 38, 32);
            }
            foreach (var (mode, label) in new[] { (PictureBoxSizeMode.Normal, "Normal"), (PictureBoxSizeMode.StretchImage, "Stretch"), (PictureBoxSizeMode.Zoom, "Zoom"), (PictureBoxSizeMode.CenterImage, "Center") })
            {
                var wrapper = new Panel { Width = 124, Height = 155, Margin = new Padding(4) };
                wrapper.Controls.Add(new PictureBox { Width = 116, Height = 110, BorderStyle = BorderStyle.FixedSingle, SizeMode = mode, Image = bmp });
                wrapper.Controls.Add(new Label { Text = label, Width = 116, Height = 18, Top = 114, TextAlign = ContentAlignment.MiddleCenter });
                pbFlow.Controls.Add(wrapper);
            }
            pbGroup.Controls.Add(pbFlow);
            flow.Controls.Add(pbGroup);

            // Control states (disabled / readonly)
            var stateGroup = new GroupBox { Text = "Control States", Width = 400, Height = 210 };
            var stateTable = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            stateTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
            stateTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            var stateItems = new (string lbl, Control ctrl)[]
            {
                ("Disabled TextBox:",       new TextBox       { Text = "Cannot edit",     Enabled = false, Dock = DockStyle.Fill }),
                ("Read-Only TextBox:",      new TextBox       { Text = "Read only",        ReadOnly = true, Dock = DockStyle.Fill }),
                ("Disabled Button:",        new Button        { Text = "Disabled",         Enabled = false, Dock = DockStyle.Fill }),
                ("Disabled CheckBox:",      new CheckBox      { Text = "Disabled (on)",    Enabled = false, Checked = true, Dock = DockStyle.Fill }),
                ("Disabled ComboBox:",      new ComboBox      { Text = "Disabled",         Enabled = false, Dock = DockStyle.Fill }),
                ("Disabled NumericUpDown:", new NumericUpDown { Value = 42,                Enabled = false, Dock = DockStyle.Fill }),
            };
            for (int i = 0; i < stateItems.Length; i++)
            {
                stateTable.Controls.Add(Lbl(stateItems[i].lbl), 0, i);
                stateTable.Controls.Add(stateItems[i].ctrl, 1, i);
            }
            stateGroup.Controls.Add(stateTable);
            flow.Controls.Add(stateGroup);

            page.Controls.Add(flow);
            return page;
        }

        // ── TAB 8: DATES & TIMES ──────────────────────────────────────────────────
        private TabPage BuildDatesTab()
        {
            var page = new TabPage("Dates & Times") { AutoScroll = true, Padding = new Padding(8) };
            var tl = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 4 };
            tl.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            tl.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            tl.Controls.Add(Lbl("Short Date:"), 0, 0); tl.Controls.Add(new DateTimePicker { Format = DateTimePickerFormat.Short, Dock = DockStyle.Fill }, 1, 0);
            tl.Controls.Add(Lbl("Long Date:"), 2, 0); tl.Controls.Add(new DateTimePicker { Format = DateTimePickerFormat.Long, Dock = DockStyle.Fill }, 3, 0);
            tl.Controls.Add(Lbl("Time (dropdown):"), 0, 1); tl.Controls.Add(new DateTimePicker { Format = DateTimePickerFormat.Time, Dock = DockStyle.Fill }, 1, 1);
            tl.Controls.Add(Lbl("Time (spinner):"), 2, 1); tl.Controls.Add(new DateTimePicker { Format = DateTimePickerFormat.Time, ShowUpDown = true, Dock = DockStyle.Fill }, 3, 1);
            tl.Controls.Add(Lbl("Custom Format:"), 0, 2);
            var custom = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "ddd, MMM d yyyy — HH:mm", Dock = DockStyle.Fill };
            tl.SetColumnSpan(custom, 3); tl.Controls.Add(custom, 1, 2);
            tl.Controls.Add(Lbl("Range Start:"), 0, 3); tl.Controls.Add(new DateTimePicker { Format = DateTimePickerFormat.Short, Dock = DockStyle.Fill }, 1, 3);
            tl.Controls.Add(Lbl("Range End:"), 2, 3); tl.Controls.Add(new DateTimePicker { Format = DateTimePickerFormat.Short, Dock = DockStyle.Fill }, 3, 3);
            tl.Controls.Add(Lbl("Nullable Date:"), 0, 4); tl.Controls.Add(new DateTimePicker { Format = DateTimePickerFormat.Short, ShowCheckBox = true, Checked = false, Dock = DockStyle.Fill }, 1, 4);
            tl.Controls.Add(Lbl("Masked Date:"), 2, 4); tl.Controls.Add(new MaskedTextBox { Mask = "00/00/0000", Dock = DockStyle.Fill }, 3, 4);
            page.Controls.Add(tl);

            var calGroup = new GroupBox { Text = "MonthCalendar", Dock = DockStyle.Top, Height = 220, Padding = new Padding(6) };
            var calFlow = new FlowLayoutPanel { Dock = DockStyle.Fill };
            calFlow.Controls.Add(new MonthCalendar { MaxSelectionCount = 30, ShowWeekNumbers = true });
            calFlow.Controls.Add(new MonthCalendar { MaxSelectionCount = 1, ShowToday = false });
            calGroup.Controls.Add(calFlow);
            page.Controls.Add(calGroup);
            return page;
        }

        // ── TAB 9: DIALOGS & MISC ─────────────────────────────────────────────────
        private TabPage BuildDialogsTab()
        {
            var page = new TabPage("Dialogs") { AutoScroll = true, Padding = new Padding(8) };

            var dlgGroup = new GroupBox { Text = "Standard Dialog Launchers", Dock = DockStyle.Top, Height = 200, Padding = new Padding(8) };
            var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true };
            Button Btn(string label, Action onClick) { var b = new Button { Text = label, Width = 145, Height = 34, Margin = new Padding(4) }; b.Click += (_, _) => onClick(); return b; }
            flow.Controls.Add(Btn("Open File...", () => { using var d = new OpenFileDialog { Filter = "All files|*.*|Text|*.txt" }; d.ShowDialog(this); }));
            flow.Controls.Add(Btn("Save File...", () => { using var d = new SaveFileDialog { Filter = "JSON|*.json|All|*.*" }; d.ShowDialog(this); }));
            flow.Controls.Add(Btn("Browse Folder...", () => { using var d = new FolderBrowserDialog(); d.ShowDialog(this); }));
            flow.Controls.Add(Btn("Choose Font...", () => { using var d = new FontDialog(); d.ShowDialog(this); }));
            flow.Controls.Add(Btn("Choose Color...", () => { using var d = new ColorDialog { AnyColor = true }; d.ShowDialog(this); }));
            flow.Controls.Add(Btn("Print...", () => { using var d = new PrintDialog(); d.ShowDialog(this); }));
            flow.Controls.Add(Btn("MessageBox Info", () => MessageBox.Show("This is an information message.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information)));
            flow.Controls.Add(Btn("MessageBox Warning", () => MessageBox.Show("This action cannot be undone.", "Warning", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning)));
            flow.Controls.Add(Btn("MessageBox Error", () => MessageBox.Show("A critical error occurred.", "Error", MessageBoxButtons.AbortRetryIgnore, MessageBoxIcon.Error)));
            flow.Controls.Add(Btn("MessageBox Question", () => MessageBox.Show("Do you want to proceed?", "Confirm", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question)));
            dlgGroup.Controls.Add(flow);
            page.Controls.Add(dlgGroup);

            var miscGroup = new GroupBox { Text = "Miscellaneous Controls", Dock = DockStyle.Top, Height = 240, Padding = new Padding(8) };
            var miscTable = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4 };
            miscTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            miscTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            miscTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            miscTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            miscTable.Controls.Add(Lbl("Marquee Bar:"), 0, 0);
            _marqueeBar = new ProgressBar { Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 30, Dock = DockStyle.Fill };
            miscTable.Controls.Add(_marqueeBar, 1, 0);
            miscTable.Controls.Add(Lbl("Domain Spinner:"), 2, 0);
            var domSpin = new DomainUpDown { Dock = DockStyle.Fill };
            domSpin.Items.AddRange(new object[] { "Alpha", "Beta", "Gamma", "Delta", "Epsilon", "Zeta" });
            domSpin.SelectedIndex = 0;
            miscTable.Controls.Add(domSpin, 3, 0);

            miscTable.Controls.Add(Lbl("Numeric (decimal):"), 0, 1);
            miscTable.Controls.Add(new NumericUpDown { Minimum = -100, Maximum = 100, Value = 3.14m, DecimalPlaces = 4, Increment = 0.0001m, Dock = DockStyle.Fill }, 1, 1);
            miscTable.Controls.Add(Lbl("IP Address:"), 2, 1);
            miscTable.Controls.Add(new MaskedTextBox { Mask = "990.990.990.990", Dock = DockStyle.Fill }, 3, 1);

            miscTable.Controls.Add(Lbl("Zip Code:"), 0, 2);
            miscTable.Controls.Add(new MaskedTextBox { Mask = "00000-0000", Dock = DockStyle.Fill }, 1, 2);
            miscTable.Controls.Add(Lbl("3-State CheckBox:"), 2, 2);
            miscTable.Controls.Add(new CheckBox { Text = "Indeterminate state", ThreeState = true, CheckState = CheckState.Indeterminate, Dock = DockStyle.Fill }, 3, 2);

            miscTable.Controls.Add(Lbl("Simple ComboBox:"), 0, 3);
            var simpleCombo = new ComboBox { DropDownStyle = ComboBoxStyle.Simple, Dock = DockStyle.Fill, Height = 80 };
            simpleCombo.Items.AddRange(new object[] { "Option A", "Option B", "Option C", "Option D", "Option E" });
            simpleCombo.SelectedIndex = 0;
            miscTable.Controls.Add(simpleCombo, 1, 3);
            miscTable.Controls.Add(Lbl("Multi-select ListBox:"), 2, 3);
            var multiList = new ListBox { SelectionMode = SelectionMode.MultiSimple, Dock = DockStyle.Fill };
            multiList.Items.AddRange(new object[] { "Apple", "Banana", "Cherry", "Date", "Elderberry" });
            miscTable.Controls.Add(multiList, 3, 3);

            miscGroup.Controls.Add(miscTable);
            page.Controls.Add(miscGroup);
            return page;
        }

        // ── HELPERS ───────────────────────────────────────────────────────────────
        private static Label Lbl(string text) =>
            new Label { Text = text, AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(3, 6, 3, 0) };

        private static Label BoldLabel(string text) =>
            new Label { Text = text, Font = new Font("Segoe UI", 9, FontStyle.Bold), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, BackColor = SystemColors.ControlLight };

        private void InitializeComponent()
        {

        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { _toolTip?.Dispose(); }
            base.Dispose(disposing);
        }
    }

    // ── SAMPLE CONFIG OBJECT FOR PROPERTYGRID ────────────────────────────────────
    [System.ComponentModel.Description("Runtime configuration for the system service.")]
    public class SampleConfig
    {
        [System.ComponentModel.Category("General")]
        [System.ComponentModel.Description("Display name for this profile.")]
        public string ProfileName { get; set; } = "Default Profile";

        [System.ComponentModel.Category("General")]
        [System.ComponentModel.Description("Whether this profile is active.")]
        public bool IsActive { get; set; } = true;

        [System.ComponentModel.Category("General")]
        [System.ComponentModel.Description("Target deployment environment.")]
        public string Environment { get; set; } = "Production";

        [System.ComponentModel.Category("Performance")]
        [System.ComponentModel.Description("Maximum concurrent worker threads.")]
        public int MaxWorkerThreads { get; set; } = 16;

        [System.ComponentModel.Category("Performance")]
        [System.ComponentModel.Description("In-memory cache capacity in megabytes.")]
        public int CacheSizeMb { get; set; } = 512;

        [System.ComponentModel.Category("Performance")]
        [System.ComponentModel.Description("Request timeout in seconds.")]
        public double RequestTimeoutSeconds { get; set; } = 30.0;

        [System.ComponentModel.Category("Logging")]
        [System.ComponentModel.Description("Minimum log level to capture.")]
        public string LogLevel { get; set; } = "Info";

        [System.ComponentModel.Category("Logging")]
        [System.ComponentModel.Description("Max log file size in MB before rotation.")]
        public int MaxLogFileMb { get; set; } = 100;

        [System.ComponentModel.Category("Logging")]
        [System.ComponentModel.Description("Number of days to retain rotated logs.")]
        public int LogRetentionDays { get; set; } = 30;

        [System.ComponentModel.Category("Security")]
        [System.ComponentModel.Description("Enforce HTTPS for all outbound connections.")]
        public bool EnforceHttps { get; set; } = true;

        [System.ComponentModel.Category("Security")]
        [System.ComponentModel.Description("Idle session timeout in minutes.")]
        public int SessionTimeoutMinutes { get; set; } = 60;

        [System.ComponentModel.Category("Security")]
        [System.ComponentModel.Description("Restrict connections to loopback only.")]
        public bool LoopbackOnly { get; set; } = false;
    }
}