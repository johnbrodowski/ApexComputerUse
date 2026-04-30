namespace ApexComputerUse
{
    internal sealed class ModelTabController
    {
        private readonly CommandProcessor _processor;
        private readonly DownloadManager _downloader;
        private readonly Action _saveSettings;
        private readonly Action<string> _log;

        private readonly TextBox _txtModelPath, _txtProjPath, _txtDownloadUrl;
        private readonly Button _btnDownloadAll, _btnDownload, _btnLoadModel;
        private readonly Label _lblDownloadStatus, _lblModelStatus;
        private readonly ProgressBar _pbarDownload;
        private readonly TabControl _tabMain;
        private readonly TabPage _tabPageModel;

        internal ModelTabController(
            CommandProcessor processor, DownloadManager downloader,
            Action saveSettings, Action<string> log,
            TextBox txtModelPath, TextBox txtProjPath, TextBox txtDownloadUrl,
            Button btnDownloadAll, Button btnDownload, Button btnLoadModel,
            Label lblDownloadStatus, Label lblModelStatus,
            ProgressBar pbarDownload, TabControl tabMain, TabPage tabPageModel)
        {
            _processor = processor; _downloader = downloader;
            _saveSettings = saveSettings; _log = log;
            _txtModelPath = txtModelPath; _txtProjPath = txtProjPath; _txtDownloadUrl = txtDownloadUrl;
            _btnDownloadAll = btnDownloadAll; _btnDownload = btnDownload; _btnLoadModel = btnLoadModel;
            _lblDownloadStatus = lblDownloadStatus; _lblModelStatus = lblModelStatus;
            _pbarDownload = pbarDownload; _tabMain = tabMain; _tabPageModel = tabPageModel;
        }

        internal void WireDownloader()
        {
            _downloader.Status += (msg, col) => _lblDownloadStatus.BeginInvoke(() =>
            {
                _lblDownloadStatus.Text = msg;
                _lblDownloadStatus.ForeColor = col;
            });
            _downloader.Progress += pct => _pbarDownload.BeginInvoke(() => _pbarDownload.Value = pct);
        }

        internal void CheckFirstLaunch()
        {
            if (!DownloadManager.SetupFiles.Any(f => !File.Exists(f.RelPath))) return;
            _tabMain.SelectedTab = _tabPageModel;
            _lblDownloadStatus.ForeColor = Color.DarkBlue;
            _lblDownloadStatus.Text = "First launch - click \"Download All\" to set up models and tessdata.";
        }

        internal void BrowseModel()
        {
            using var dlg = new OpenFileDialog
            { Filter = "GGUF Model|*.gguf|All Files|*.*", Title = "Select LLM Model (.gguf)" };
            if (dlg.ShowDialog() == DialogResult.OK)
                _txtModelPath.Text = dlg.FileName;
        }

        internal void BrowseProj()
        {
            using var dlg = new OpenFileDialog
            { Filter = "GGUF Projector|*.gguf|All Files|*.*", Title = "Select Multimodal Projector (.gguf)" };
            if (dlg.ShowDialog() == DialogResult.OK)
                _txtProjPath.Text = dlg.FileName;
        }

        internal async Task DownloadAll()
        {
            if (_downloader.IsRunning) { _downloader.Cancel(); return; }

            _btnDownloadAll.Text = "Cancel";
            _btnDownload.Enabled = false;
            _pbarDownload.Value = 0;

            try
            {
                bool ok = await _downloader.RunSetupAsync();
                if (ok)
                {
                    var files = DownloadManager.SetupFiles;
                    _txtModelPath.Text = files[0].RelPath;
                    _txtProjPath.Text  = files[1].RelPath;
                    _saveSettings();
                    _log($"Setup complete. Model: {files[0].RelPath}");
                    _log($"Projector: {files[1].RelPath}");
                    _log($"Tessdata:  {files[2].RelPath}");
                }
            }
            finally
            {
                _btnDownloadAll.Text = "Download All  (LFM2.5-VL model + projector + tessdata)";
                _btnDownload.Enabled = true;
            }
        }

        internal async Task LoadModel()
        {
            string model = _txtModelPath.Text.Trim();
            string proj  = _txtProjPath.Text.Trim();
            if (string.IsNullOrWhiteSpace(model)) { _log("Enter a model path."); return; }
            if (string.IsNullOrWhiteSpace(proj))  { _log("Enter a projector path."); return; }

            _btnLoadModel.Enabled = false;
            _lblModelStatus.Text = "Loading...";
            _lblModelStatus.ForeColor = Color.DarkOrange;

            try
            {
                var resp = await _processor.InitModelAsync(model, proj);
                _log(resp.ToText());
                if (resp.Success)
                {
                    _lblModelStatus.Text = "Loaded ?";
                    _lblModelStatus.ForeColor = Color.Green;
                    _saveSettings();
                }
                else
                {
                    _lblModelStatus.Text = "Failed - see Console tab";
                    _lblModelStatus.ForeColor = Color.Red;
                }
            }
            catch (Exception ex)
            {
                _log($"[Load Model] Unhandled: {ex}");
                _lblModelStatus.Text = "Failed - see Console tab";
                _lblModelStatus.ForeColor = Color.Red;
            }
            finally
            {
                _btnLoadModel.Enabled = true;
            }
        }

        internal async Task Download()
        {
            if (_downloader.IsRunning) { _downloader.Cancel(); return; }

            string url = _txtDownloadUrl.Text.Trim();
            if (string.IsNullOrWhiteSpace(url)) { _log("Enter a download URL."); return; }

            string defaultDir = string.IsNullOrWhiteSpace(_txtProjPath.Text)
                ? Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
                : Path.GetDirectoryName(_txtProjPath.Text) ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

            string fileName = Path.GetFileName(new Uri(url).LocalPath);
            if (string.IsNullOrWhiteSpace(fileName)) fileName = "model.gguf";

            using var saveDlg = new SaveFileDialog
            {
                Title            = "Save Vision Model As",
                Filter           = "GGUF Model|*.gguf|All Files|*.*",
                FileName         = fileName,
                InitialDirectory = defaultDir
            };
            if (saveDlg.ShowDialog() != DialogResult.OK) return;

            string destPath = saveDlg.FileName;
            _btnDownload.Text  = "Cancel";
            _pbarDownload.Value = 0;

            try
            {
                bool ok = await _downloader.DownloadAsync(url, destPath);
                if (ok)
                {
                    _txtProjPath.Text = destPath;
                    _log($"Vision model downloaded to: {destPath}");
                }
            }
            finally
            {
                _btnDownload.Text = "Download";
            }
        }
    }
}

