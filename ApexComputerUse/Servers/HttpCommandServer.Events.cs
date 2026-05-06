using System.Net;
using System.Text;
using System.Text.Json;

namespace ApexComputerUse
{
    public partial class HttpCommandServer
    {
        // -- /events: SSE event stream -----------------------------------------
        //
        // Long-lived response that streams desktop events as text/event-stream frames.
        // Each event:
        //
        //   event: window-created
        //   data: {"seq":42,"id":708379645,"title":"Notepad","time":"2026-..."}
        //
        // Filtering:
        //   ?types=window-created,window-title-changed   (CSV; empty = all v1 types)
        //   ?windowId=42                                  (numeric stable ID)
        //
        // The connection stays open until the client disconnects (IOException on write) or
        // the listener stops. The broker's poll thread only runs while subscribers are
        // connected, so /events is idle-cheap when no one is listening.
        private async Task HandleEventsAsync(HttpListenerRequest req, HttpListenerResponse res)
        {
            // Set up SSE headers. Don't touch ContentLength64 - HttpListener uses chunked
            // transfer when Content-Length is omitted, which is what SSE requires.
            res.StatusCode      = 200;
            res.ContentType     = "text/event-stream; charset=utf-8";
            res.SendChunked     = true;
            res.Headers["Cache-Control"]   = "no-cache, no-transform";
            res.Headers["Connection"]      = "keep-alive";
            res.Headers["X-Accel-Buffering"] = "no";   // disable nginx-style proxy buffering

            // Parse filters.
            HashSet<string>? types = null;
            string? typesCsv = req.QueryString["types"];
            if (!string.IsNullOrWhiteSpace(typesCsv))
            {
                types = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var t in typesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    types.Add(t);
            }
            int? windowFilter = int.TryParse(req.QueryString["windowId"], out int w) ? w : null;

            // Subscribe. Disposing the subscriber removes it from the broker and stops the
            // poll thread once the last subscriber leaves.
            using var sub = _events.Subscribe(types, windowFilter);
            var stream = res.OutputStream;

            // Initial comment frame so curl/EventSource clients see the connection is live
            // before the first event arrives. SSE comments start with ':' and are ignored
            // by parsers but flush the buffer through any intermediate proxy.
            await WriteFrameAsync(stream, ":connected\n\n").ConfigureAwait(false);

            // Heartbeat keeps the connection alive through idle periods so middleboxes don't
            // time out the socket. Sent every 20 seconds when no real event is pending.
            var heartbeatInterval = TimeSpan.FromSeconds(20);
            var nextHeartbeat = DateTime.UtcNow + heartbeatInterval;

            try
            {
                while (true)
                {
                    var waitTimeout = nextHeartbeat - DateTime.UtcNow;
                    if (waitTimeout < TimeSpan.Zero) waitTimeout = TimeSpan.Zero;

                    using var cts = new CancellationTokenSource(waitTimeout);
                    EventBroker.EventEnvelope? envelope = null;
                    try
                    {
                        if (await sub.Reader.WaitToReadAsync(cts.Token).ConfigureAwait(false))
                        {
                            sub.Reader.TryRead(out envelope);
                        }
                        else
                        {
                            // Channel completed (broker disposed) - end the stream cleanly.
                            break;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // Heartbeat timeout - send keepalive and loop.
                    }

                    if (envelope != null)
                    {
                        // Payload = data dict + seq + time. Window events carry id/title in Data;
                        // monitor events carry their own keys. Frontends parse on event type.
                        var dict = new Dictionary<string, object?>(envelope.Data)
                        {
                            ["seq"]  = envelope.Seq,
                            ["time"] = envelope.Time.ToString("o")
                        };
                        string payload = JsonSerializer.Serialize(dict);
                        await WriteFrameAsync(stream, $"event: {envelope.Type}\ndata: {payload}\n\n")
                            .ConfigureAwait(false);
                    }
                    else if (DateTime.UtcNow >= nextHeartbeat)
                    {
                        await WriteFrameAsync(stream, ": keepalive\n\n").ConfigureAwait(false);
                        nextHeartbeat = DateTime.UtcNow + heartbeatInterval;
                    }
                }
            }
            catch (HttpListenerException) { /* client disconnected */ }
            catch (IOException)            { /* client disconnected */ }
            catch (ObjectDisposedException) { /* listener torn down */ }
            // Subscriber cleanup happens via `using` -> Dispose -> Unsubscribe -> StopPoller
            // when this is the last subscriber.
        }

        private static async Task WriteFrameAsync(System.IO.Stream stream, string frame)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(frame);
            await stream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
            await stream.FlushAsync().ConfigureAwait(false);
        }
    }
}
