namespace ApexUIBridge.TestRunner;

using System.Net;

/// <summary>
/// Minimal static file server backed by HttpListener. Serves files from a root
/// folder on a given base URL. Only used when the runner launches the local web
/// test target — remote servers are configured by pointing WebBaseUrl at them
/// and leaving WebRootPath empty.
/// </summary>
public sealed class WebServer : IAsyncDisposable
{
    private readonly HttpListener _listener = new();
    private readonly string _rootFolder;
    private readonly string _prefix;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public WebServer(string baseUrl, string rootFolder)
    {
        _rootFolder = Path.GetFullPath(rootFolder);
        _prefix = baseUrl.TrimEnd('/') + "/";
        _listener.Prefixes.Add(_prefix);
    }

    public void Start()
    {
        if (!Directory.Exists(_rootFolder))
            throw new DirectoryNotFoundException($"WebRootPath not found: {_rootFolder}");

        _listener.Start();
        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => AcceptLoop(_cts.Token));
        Console.WriteLine($"[WebServer] Serving {_rootFolder} at {_prefix}");
    }

    private async Task AcceptLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener.IsListening)
        {
            HttpListenerContext ctx;
            try { ctx = await _listener.GetContextAsync().ConfigureAwait(false); }
            catch (HttpListenerException) { return; }
            catch (ObjectDisposedException) { return; }

            _ = Task.Run(() => Handle(ctx));
        }
    }

    private void Handle(HttpListenerContext ctx)
    {
        try
        {
            var relative = Uri.UnescapeDataString(ctx.Request.Url!.AbsolutePath.TrimStart('/'));
            if (string.IsNullOrEmpty(relative)) relative = "index.html";

            var full = Path.GetFullPath(Path.Combine(_rootFolder, relative));
            if (!full.StartsWith(_rootFolder, StringComparison.OrdinalIgnoreCase) || !File.Exists(full))
            {
                ctx.Response.StatusCode = 404;
                ctx.Response.Close();
                return;
            }

            ctx.Response.ContentType = MimeFor(Path.GetExtension(full));
            using var fs = File.OpenRead(full);
            ctx.Response.ContentLength64 = fs.Length;
            fs.CopyTo(ctx.Response.OutputStream);
            ctx.Response.OutputStream.Close();
        }
        catch
        {
            try { ctx.Response.StatusCode = 500; ctx.Response.Close(); } catch { }
        }
    }

    private static string MimeFor(string ext) => ext.ToLowerInvariant() switch
    {
        ".html" or ".htm" => "text/html; charset=utf-8",
        ".js" => "application/javascript; charset=utf-8",
        ".css" => "text/css; charset=utf-8",
        ".json" => "application/json; charset=utf-8",
        ".svg" => "image/svg+xml",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".ico" => "image/x-icon",
        _ => "application/octet-stream"
    };

    public async ValueTask DisposeAsync()
    {
        try { _cts?.Cancel(); } catch { }
        try { _listener.Stop(); } catch { }
        if (_loop is not null)
        {
            try { await _loop.ConfigureAwait(false); } catch { }
        }
        _listener.Close();
        Console.WriteLine("[WebServer] Stopped.");
    }
}
