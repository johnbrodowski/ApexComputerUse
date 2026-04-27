namespace ApexComputerUse
{
    internal sealed class ClientsTabController
    {
        private readonly ClientStore   _store;
        private readonly ListView      _list;
        private readonly Button        _btnAdd, _btnEdit, _btnRemove, _btnTest, _btnOpenWebUi, _btnLaunch;
        private readonly Func<int>     _getCurrentPort;

        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(3) };

        internal ClientsTabController(
            ClientStore store,
            ListView    listViewClients,
            Button      btnAdd,
            Button      btnEdit,
            Button      btnRemove,
            Button      btnTest,
            Button      btnOpenWebUi,
            Button      btnLaunchInstance,
            Func<int>   getCurrentPort)
        {
            _store          = store;
            _list           = listViewClients;
            _btnAdd         = btnAdd;
            _btnEdit        = btnEdit;
            _btnRemove      = btnRemove;
            _btnTest        = btnTest;
            _btnOpenWebUi   = btnOpenWebUi;
            _btnLaunch      = btnLaunchInstance;
            _getCurrentPort = getCurrentPort;
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

            RefreshList();
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

            try
            {
                string url = $"http://{client.Host}:{client.Port}/ping";
                var headers = new HttpRequestMessage(HttpMethod.Get, url);
                if (client.ApiKey.Length > 0)
                    headers.Headers.Add("X-Api-Key", client.ApiKey);

                var resp = await _http.SendAsync(headers).ConfigureAwait(false);
                _list.Invoke(() => SetStatus(item, resp.IsSuccessStatusCode ? "Online" : $"HTTP {(int)resp.StatusCode}", resp.IsSuccessStatusCode ? Color.Green : Color.OrangeRed));
            }
            catch
            {
                _list.Invoke(() => SetStatus(item, "Offline", Color.Red));
            }
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

            // Suggest the port after the one this instance is using; the new process will
            // auto-increment further if that port is also taken.
            int nextPort = _getCurrentPort() + 1;

            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo(exe, $"--port {nextPort} --client")
                {
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);

                // Pre-add a client entry for the new instance so it appears in the list.
                var client = new RemoteClient
                {
                    Name        = $"Local :{nextPort}",
                    Host        = "localhost",
                    Port        = nextPort,
                    OsVersion   = Environment.OSVersion.VersionString,
                    Description = "Launched instance"
                };
                _store.Add(client);
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
