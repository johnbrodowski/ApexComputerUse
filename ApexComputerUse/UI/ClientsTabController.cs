using System.Net;
using System.Net.Sockets;

namespace ApexComputerUse
{
    internal sealed class ClientsTabController
    {
        private readonly ClientStore    _store;
        private readonly ListView       _list;
        private readonly Button         _btnAdd, _btnEdit, _btnRemove, _btnTest, _btnOpenWebUi, _btnLaunch;
        private readonly Func<int>               _getCurrentPort;
        private readonly Func<string>            _getCurrentApiKey;
        private readonly Action<string, Color>?  _updateClientsStatus;

        private System.Windows.Forms.Timer? _heartbeat;
        private bool                        _pingInFlight;

        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(3) };

        internal ClientsTabController(
            ClientStore   store,
            ListView      listViewClients,
            Button        btnAdd,
            Button        btnEdit,
            Button        btnRemove,
            Button        btnTest,
            Button        btnOpenWebUi,
            Button        btnLaunchInstance,
            Func<int>              getCurrentPort,
            Func<string>           getCurrentApiKey,
            Action<string, Color>? updateClientsStatus = null)
        {
            _store               = store;
            _list                = listViewClients;
            _btnAdd              = btnAdd;
            _btnEdit             = btnEdit;
            _btnRemove           = btnRemove;
            _btnTest             = btnTest;
            _btnOpenWebUi        = btnOpenWebUi;
            _btnLaunch           = btnLaunchInstance;
            _getCurrentPort      = getCurrentPort;
            _getCurrentApiKey    = getCurrentApiKey;
            _updateClientsStatus = updateClientsStatus;
        }

        internal void Init()
        {
            _btnAdd.Click       += (_, _) => Add();
            _btnEdit.Click      += (_, _) => Edit();
            _btnRemove.Click    += (_, _) => Remove();
            _btnTest.Click      += (_, _) => _ = TestConnectionAsync();
            _btnOpenWebUi.Click += (_, _) => OpenWebUi();
            _btnLaunch.Click    += (_, _) => LaunchInstance();
            _list.DoubleClick   += (_, _) => OpenWebUi();

            if (Program.IsClientInstance)
                _btnLaunch.Visible = false;

            if (_list.Columns.Count == 0)
            {
                _list.Columns.Add("Name",        120);
                _list.Columns.Add("Host",         90);
                _list.Columns.Add("Port",         50);
                _list.Columns.Add("OS",          120);
                _list.Columns.Add("Description", 100);
                _list.Columns.Add("Status",       87);
            }

            RefreshList();

            _heartbeat = new System.Windows.Forms.Timer { Interval = 5000 };
            _heartbeat.Tick += (_, _) => { if (_pingInFlight) return; _ = PingAllAsync(); };
            _heartbeat.Start();
        }

        internal void Add()
        {
            using var dlg = new ClientEditForm();
            if (dlg.ShowDialog(_list.FindForm()) != DialogResult.OK || dlg.Result is null) return;
            _store.Add(dlg.Result);
            RefreshList();
        }

        internal void Edit()
        {
            if (SelectedClient() is not RemoteClient existing) return;
            using var dlg = new ClientEditForm(existing);
            if (dlg.ShowDialog(_list.FindForm()) != DialogResult.OK || dlg.Result is null) return;
            _store.Update(dlg.Result);
            RefreshList();
        }

        internal void Remove()
        {
            if (SelectedClient() is not RemoteClient client) return;
            var answer = MessageBox.Show(
                $"Remove \"{client.Name}\"?", "Confirm Remove",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (answer != DialogResult.Yes) return;
            _store.Remove(client.Id);
            RefreshList();
        }

        internal async Task TestConnectionAsync()
        {
            if (SelectedClient() is not RemoteClient client) return;
            if (_list.SelectedItems.Count == 0) return;

            var item = _list.SelectedItems[0];
            SetStatus(item, "Testing…", Color.Gray);
            await PingOneAsync(item, client).ConfigureAwait(false);
        }

        private async Task PingOneAsync(ListViewItem item, RemoteClient client)
        {
            try
            {
                string url     = $"http://{client.Host}:{client.Port}/ping";
                var    request = new HttpRequestMessage(HttpMethod.Get, url);
                if (client.ApiKey.Length > 0)
                    request.Headers.Add("X-Api-Key", client.ApiKey);

                var resp = await _http.SendAsync(request).ConfigureAwait(false);
                _list.Invoke(() => SetStatus(
                    item,
                    resp.IsSuccessStatusCode ? "Online" : $"HTTP {(int)resp.StatusCode}",
                    resp.IsSuccessStatusCode ? Color.Green : Color.OrangeRed));
            }
            catch
            {
                _list.Invoke(() => SetStatus(item, "Offline", Color.Red));
            }
        }

        private async Task PingAllAsync()
        {
            try
            {
                _pingInFlight = true;
                var rows = _list.Items.Cast<ListViewItem>().ToArray();
                foreach (var item in rows)
                {
                    if (item.Tag is not string id) continue;
                    if (item.SubItems[5].Text == "Testing…") continue;
                    if (_store.Get(id) is not RemoteClient client) continue;
                    await PingOneAsync(item, client).ConfigureAwait(false);
                }
            }
            finally
            {
                _pingInFlight = false;
                if (_updateClientsStatus != null)
                    _list.Invoke(() =>
                    {
                        int tot = _list.Items.Count;
                        int on  = _list.Items.Cast<ListViewItem>()
                                      .Count(i => i.SubItems[5].Text == "Online");
                        if (tot == 0)
                            _updateClientsStatus("Clients: --", SystemColors.GrayText);
                        else
                        {
                            Color c = on == tot ? Color.Green
                                    : on  > 0   ? Color.DarkOrange
                                                : SystemColors.GrayText;
                            _updateClientsStatus($"Clients: {on}/{tot}", c);
                        }
                    });
            }
        }

        private static int FindFreeLoopbackPort(int startPort, int maxAttempts = 50)
        {
            for (int p = startPort; p < startPort + maxAttempts; p++)
            {
                try
                {
                    var l = new TcpListener(IPAddress.Loopback, p);
                    l.Start();
                    l.Stop();
                    return p;
                }
                catch (SocketException) { }
            }
            return startPort;
        }

        internal void OpenWebUi()
        {
            if (SelectedClient() is not RemoteClient client) return;
            var key = Uri.EscapeDataString(client.ApiKey);
            var url = $"http://{client.Host}:{client.Port}/chat{(key.Length > 0 ? $"?apiKey={key}" : "")}";
            try
            {
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open browser:\n{ex.Message}", "Open Web UI",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        internal void LaunchInstance()
        {
            string? exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe))
            {
                MessageBox.Show("Cannot determine the executable path.", "Launch Instance",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Probe for an actually-free loopback port starting one above this instance's
            // port; the child's HttpCommandServer.Start() still auto-increments as a fallback.
            int nextPort = FindFreeLoopbackPort(_getCurrentPort() + 1);

            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo(exe, $"--port {nextPort} --client")
                {
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);

                // Pre-add a client entry only if no entry with the same host:port already exists.
                bool exists = _store.GetAll().Any(c =>
                    c.Port == nextPort &&
                    c.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase));

                if (!exists)
                {
                    var client = new RemoteClient
                    {
                        Name        = $"Local :{nextPort}",
                        Host        = "localhost",
                        Port        = nextPort,
                        ApiKey      = _getCurrentApiKey(),
                        OsVersion   = Environment.OSVersion.VersionString,
                        Description = "Launched instance"
                    };
                    _store.Add(client);
                }
                RefreshList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not launch instance:\n{ex.Message}", "Launch Instance",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void RefreshList()
        {
            _list.BeginUpdate();
            _list.Items.Clear();
            foreach (var c in _store.GetAll())
            {
                var item = new ListViewItem(c.Name) { Tag = c.Id };
                item.SubItems.Add(c.Host);
                item.SubItems.Add(c.Port.ToString());
                item.SubItems.Add(c.OsVersion);
                item.SubItems.Add(c.Description);
                item.SubItems.Add("");
                _list.Items.Add(item);
            }
            _list.EndUpdate();
        }

        private RemoteClient? SelectedClient()
        {
            if (_list.SelectedItems.Count == 0) return null;
            if (_list.SelectedItems[0].Tag is not string id) return null;
            return _store.Get(id);
        }

        private static void SetStatus(ListViewItem item, string text, Color colour)
        {
            item.SubItems[5].Text      = text;
            item.SubItems[5].ForeColor = colour;
        }
    }
}
