using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;

namespace ApexComputerUse
{
    /// <summary>
    /// Named-pipe server exposing the full Apex command set.
    ///
    /// Each client connection is handled on its own task; the shared
    /// CommandProcessor serialises access via its internal lock.
    ///
    /// Protocol: newline-delimited JSON — send one JSON object per line,
    /// receive one JSON object per line. Send {"command":"exit"} to close
    /// the connection gracefully.
    ///
    /// PowerShell quick-start:
    ///   $p = [System.IO.Pipes.NamedPipeClientStream]::new('.','ApexComputerUse','InOut')
    ///   $p.Connect(5000)
    ///   $r = [System.IO.StreamReader]::new($p)
    ///   $w = [System.IO.StreamWriter]::new($p); $w.AutoFlush = $true
    ///   $w.WriteLine('{"command":"windows"}')
    ///   $r.ReadLine() | ConvertFrom-Json
    ///
    /// Or use the bundled ApexComputerUse.psm1 module:
    ///   Import-Module .\Scripts\ApexComputerUse.psm1
    ///   Connect-FlaUI
    ///   Get-FlaUIWindows
    /// </summary>
    public class PipeCommandServer : IDisposable
    {
        private readonly string            _pipeName;
        private readonly CommandProcessor  _processor;
        private CancellationTokenSource?   _cts;
        private Task?                      _listenTask;

        private readonly List<Task> _clientTasks     = new();
        private readonly object     _clientTasksLock = new();

        public bool   IsRunning { get; private set; }
        public string PipeName  => _pipeName;
        public event  Action<string>? OnLog;

        public PipeCommandServer(string pipeName, CommandProcessor processor)
        {
            _pipeName  = pipeName;
            _processor = processor;
        }

        // ── Lifecycle ─────────────────────────────────────────────────────

        public void Start()
        {
            if (IsRunning) return;
            IsRunning   = true;
            _cts        = new CancellationTokenSource();
            _listenTask = Task.Run(() => AcceptLoop(_cts.Token));
            OnLog?.Invoke($"Pipe server listening on \\\\.\\pipe\\{_pipeName}");
        }

        public void Stop()
        {
            if (!IsRunning) return;
            _cts?.Cancel();
            IsRunning = false;

            // Wait up to 3 s for active client handlers to finish.
            Task[] pending;
            lock (_clientTasksLock) pending = _clientTasks.ToArray();
            if (pending.Length > 0)
                Task.WaitAll(pending, TimeSpan.FromSeconds(3));

            OnLog?.Invoke("Pipe server stopped.");
        }

        // ── Accept loop ───────────────────────────────────────────────────

        private async Task AcceptLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                NamedPipeServerStream? pipe = null;
                try
                {
                    pipe = NamedPipeServerStreamAcl.Create(
                        _pipeName,
                        PipeDirection.InOut,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous,
                        inBufferSize:  0,
                        outBufferSize: 0,
                        BuildPipeSecurity());

                    await pipe.WaitForConnectionAsync(ct);
                    OnLog?.Invoke("[Pipe] Client connected.");

                    var clientTask = Task.Run(() => HandleClientAsync(pipe, ct), ct);
                    lock (_clientTasksLock) _clientTasks.Add(clientTask);
                    _ = clientTask.ContinueWith(t =>
                    {
                        lock (_clientTasksLock) _clientTasks.Remove(t);
                    }, TaskScheduler.Default);
                }
                catch (OperationCanceledException) { pipe?.Dispose(); break; }
                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    OnLog?.Invoke($"[Pipe] Accept error: {ex.Message}");
                    pipe?.Dispose();
                }
            }
        }

        // ── Per-client handler ────────────────────────────────────────────

        private async Task HandleClientAsync(NamedPipeServerStream pipe, CancellationToken ct)
        {
            using (pipe)
            {
                var reader = new StreamReader(pipe, Encoding.UTF8, detectEncodingFromByteOrderMarks: false,
                                              bufferSize: 4096, leaveOpen: true);
                var writer = new StreamWriter(pipe, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                                              bufferSize: 4096, leaveOpen: true)
                             { AutoFlush = true, NewLine = "\n" };
                try
                {
                    while (!ct.IsCancellationRequested && pipe.IsConnected)
                    {
                        string? line = await reader.ReadLineAsync(ct);
                        if (line == null) break;          // client closed connection
                        line = line.Trim();
                        if (line.Length == 0) continue;

                        CommandResponse response;
                        try
                        {
                            var req = ParseJson(line);
                            if (req.Command.Equals("exit", StringComparison.OrdinalIgnoreCase))
                                break;
                            response = _processor.Process(req);
                        }
                        catch (Exception ex)
                        {
                            response = new CommandResponse { Success = false, Message = ex.Message };
                        }

                        await writer.WriteLineAsync(response.ToJson());
                    }
                }
                catch (OperationCanceledException) { /* shutting down */ }
                catch (IOException) { /* client disconnected abruptly */ }
                finally
                {
                    OnLog?.Invoke("[Pipe] Client disconnected.");
                }
            }
        }

        // ── Security ──────────────────────────────────────────────────────

        /// <summary>
        /// Builds a PipeSecurity that grants full access only to the current Windows user.
        /// All other principals are denied read/write access.
        /// </summary>
        private static PipeSecurity BuildPipeSecurity()
        {
            var security   = new PipeSecurity();
            var currentSid = WindowsIdentity.GetCurrent().User;

            if (currentSid != null)
            {
                security.AddAccessRule(new PipeAccessRule(
                    currentSid,
                    PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance,
                    AccessControlType.Allow));
            }

            // Deny everyone else (world SID) read and write access.
            security.AddAccessRule(new PipeAccessRule(
                new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                PipeAccessRights.ReadWrite,
                AccessControlType.Deny));

            return security;
        }

        // ── JSON parser ───────────────────────────────────────────────────

        private static CommandRequest ParseJson(string json)
        {
            var r = new CommandRequest();
            try
            {
                using var doc  = JsonDocument.Parse(json);
                var root = doc.RootElement;
                r.Command      = root.Str("command")      ?? "";
                r.Window       = root.Str("window");
                r.AutomationId = root.Str("automationId") ?? root.Str("id");
                r.ElementName  = root.Str("elementName")  ?? root.Str("name");
                r.SearchType   = root.Str("searchType")   ?? root.Str("type");
                r.Action       = root.Str("action");
                r.Value        = root.Str("value");
                r.ModelPath    = root.Str("model")        ?? root.Str("modelPath");
                r.MmProjPath   = root.Str("proj")         ?? root.Str("mmProjPath");
                r.Prompt       = root.Str("prompt");
            }
            catch { r.Command = "help"; }
            return r;
        }

        public void Dispose() => Stop();
    }
}
