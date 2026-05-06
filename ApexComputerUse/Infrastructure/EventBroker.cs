using System.Collections.Concurrent;
using System.Threading.Channels;

namespace ApexComputerUse
{
    /// <summary>
    /// Pub/sub broker for desktop events streamed to /events SSE subscribers.
    /// V1 events: window-created, window-closed, window-title-changed.
    ///
    /// The broker only spins its poll thread when at least one subscriber is connected, so
    /// there is no idle CPU cost when no client is listening. Window enumeration is done by
    /// a snapshotter delegate supplied by the caller (typically wired to
    /// CommandProcessor.SnapshotDesktopWindows so IDs match /windows).
    /// </summary>
    public sealed class EventBroker : IDisposable
    {
        public sealed record EventEnvelope(
            long           Seq,
            string         Type,
            DateTimeOffset Time,
            int?           WindowId,
            IReadOnlyDictionary<string, object?> Data);

        public sealed class Subscriber : IDisposable
        {
            internal Channel<EventEnvelope> Channel { get; } =
                System.Threading.Channels.Channel.CreateBounded<EventEnvelope>(
                    new BoundedChannelOptions(1024)
                    {
                        FullMode  = BoundedChannelFullMode.DropOldest,
                        SingleReader = true,
                        SingleWriter = false
                    });

            internal HashSet<string>? TypeFilter { get; init; }
            internal int?             WindowFilter { get; init; }
            private readonly EventBroker _broker;

            public ChannelReader<EventEnvelope> Reader => Channel.Reader;

            internal Subscriber(EventBroker broker, HashSet<string>? types, int? windowFilter)
            {
                _broker      = broker;
                TypeFilter   = types;
                WindowFilter = windowFilter;
            }

            public void Dispose() => _broker.Unsubscribe(this);
        }

        private readonly Func<List<(int id, string title)>> _snapshot;
        private readonly TimeSpan        _pollInterval;
        private readonly object          _gate = new();
        private readonly List<Subscriber> _subscribers = new();
        private long  _seq;
        private CancellationTokenSource? _cts;
        private Task? _pollTask;
        private Dictionary<int, string>? _lastSnapshot;

        public EventBroker(Func<List<(int id, string title)>> snapshot, TimeSpan? pollInterval = null)
        {
            _snapshot     = snapshot;
            _pollInterval = pollInterval ?? TimeSpan.FromSeconds(1);
        }

        public Subscriber Subscribe(HashSet<string>? types = null, int? windowFilter = null)
        {
            var sub = new Subscriber(this, types, windowFilter);
            lock (_gate)
            {
                _subscribers.Add(sub);
                if (_subscribers.Count == 1) StartPoller();
            }
            return sub;
        }

        internal void Unsubscribe(Subscriber sub)
        {
            lock (_gate)
            {
                if (_subscribers.Remove(sub))
                    sub.Channel.Writer.TryComplete();
                if (_subscribers.Count == 0) StopPoller();
            }
        }

        private void StartPoller()
        {
            // Caller already holds _gate.
            _cts          = new CancellationTokenSource();
            _lastSnapshot = null;   // first tick establishes a baseline without firing events
            var token     = _cts.Token;
            _pollTask     = Task.Run(() => PollLoopAsync(token), token);
        }

        private void StopPoller()
        {
            // Caller already holds _gate.
            try { _cts?.Cancel(); } catch { }
            _cts          = null;
            _pollTask     = null;
            _lastSnapshot = null;
        }

        private async Task PollLoopAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    List<(int id, string title)> current;
                    try { current = _snapshot(); }
                    catch (Exception ex)
                    {
                        AppLog.Debug($"[EventBroker] snapshot failed: {ex.Message}");
                        current = new List<(int, string)>();
                    }

                    var curMap = new Dictionary<int, string>(current.Count);
                    foreach (var w in current) curMap[w.id] = w.title;

                    if (_lastSnapshot != null)
                    {
                        // window-created: in current, not in previous.
                        foreach (var kv in curMap)
                            if (!_lastSnapshot.ContainsKey(kv.Key))
                                Emit("window-created", kv.Key, kv.Value);

                        // window-closed: in previous, not in current.
                        foreach (var kv in _lastSnapshot)
                            if (!curMap.ContainsKey(kv.Key))
                                Emit("window-closed", kv.Key, kv.Value);

                        // window-title-changed: present in both, title differs.
                        foreach (var kv in curMap)
                            if (_lastSnapshot.TryGetValue(kv.Key, out var oldTitle) && oldTitle != kv.Value)
                                Emit("window-title-changed", kv.Key, kv.Value);
                    }

                    _lastSnapshot = curMap;

                    try { await Task.Delay(_pollInterval, token).ConfigureAwait(false); }
                    catch (TaskCanceledException) { break; }
                }
            }
            catch (Exception ex)
            {
                AppLog.Debug($"[EventBroker] poll loop crashed: {ex.Message}");
            }
        }

        private void Emit(string type, int windowId, string title)
        {
            var data = new Dictionary<string, object?>
            {
                ["id"]    = windowId,
                ["title"] = title
            };
            EmitInternal(type, data, windowId);
        }

        /// <summary>
        /// Emit a custom event for non-window subsystems (region monitors, etc.).
        /// The data dictionary is serialized as the SSE payload alongside seq/time.
        /// </summary>
        public void Emit(string type, IDictionary<string, object?> data, int? windowId = null)
        {
            // Defensive copy so the caller can't mutate after dispatch.
            var snapshot = new Dictionary<string, object?>(data);
            EmitInternal(type, snapshot, windowId);
        }

        private void EmitInternal(string type, IReadOnlyDictionary<string, object?> data, int? windowId)
        {
            long seq = Interlocked.Increment(ref _seq);
            var env = new EventEnvelope(seq, type, DateTimeOffset.UtcNow, windowId, data);

            // Snapshot subscribers under the lock so we don't hold it while writing to channels
            // (channel writes can block under back-pressure if FullMode were Wait; we use DropOldest).
            List<Subscriber> snapshot;
            lock (_gate) { snapshot = _subscribers.ToList(); }
            foreach (var s in snapshot)
            {
                if (s.TypeFilter != null && !s.TypeFilter.Contains(type)) continue;
                if (s.WindowFilter.HasValue
                    && (!windowId.HasValue || s.WindowFilter.Value != windowId.Value)) continue;
                s.Channel.Writer.TryWrite(env);
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                foreach (var s in _subscribers) s.Channel.Writer.TryComplete();
                _subscribers.Clear();
                StopPoller();
            }
        }
    }
}
