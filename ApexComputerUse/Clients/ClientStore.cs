using System.Text.Json;

namespace ApexComputerUse
{
    public sealed class ClientStore
    {
        private static readonly string ClientsDir =
            Path.Combine(AppContext.BaseDirectory, "clients");

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            WriteIndented               = true,
            PropertyNameCaseInsensitive = true
        };

        private readonly Dictionary<string, RemoteClient> _clients = new();
        private readonly object _lock = new();

        public ClientStore()
        {
            Directory.CreateDirectory(ClientsDir);
            LoadAllFromDisk();
        }

        public RemoteClient Add(RemoteClient client)
        {
            lock (_lock)
            {
                _clients[client.Id] = client;
                SaveToDisk(client);
                return client;
            }
        }

        public RemoteClient? Get(string id)
        {
            lock (_lock)
                return _clients.TryGetValue(id, out var c) ? c : null;
        }

        public RemoteClient[] GetAll()
        {
            lock (_lock)
                return [.. _clients.Values.OrderBy(c => c.CreatedAt)];
        }

        /// <summary>
        /// Returns the first client whose Host matches <paramref name="host"/> (case-insensitive).
        /// Used by the HTTP server to resolve per-client permissions from the caller's IP.
        /// </summary>
        public RemoteClient? FindByHost(string host)
        {
            if (string.IsNullOrWhiteSpace(host)) return null;
            lock (_lock)
                return _clients.Values.FirstOrDefault(
                    c => c.Host.Equals(host, StringComparison.OrdinalIgnoreCase));
        }

        public void Update(RemoteClient client)
        {
            lock (_lock)
            {
                _clients[client.Id] = client;
                SaveToDisk(client);
            }
        }

        public void Remove(string id)
        {
            lock (_lock)
            {
                if (!_clients.Remove(id)) return;
                string path = ClientFilePath(id);
                if (File.Exists(path)) try { File.Delete(path); } catch { }
            }
        }

        private void SaveToDisk(RemoteClient client)
        {
            try
            {
                File.WriteAllText(ClientFilePath(client.Id),
                    JsonSerializer.Serialize(client, JsonOpts));
            }
            catch (Exception ex) { AppLog.Warning($"ClientStore: failed to persist client '{client.Id}' — {ex.Message}"); }
        }

        private void LoadAllFromDisk()
        {
            foreach (string file in Directory.GetFiles(ClientsDir, "*.json"))
            {
                try
                {
                    string json   = File.ReadAllText(file);
                    var    client = JsonSerializer.Deserialize<RemoteClient>(json, JsonOpts);
                    if (client != null)
                        _clients[client.Id] = client;
                }
                catch (Exception ex) { AppLog.Warning($"ClientStore: skipping corrupt file '{Path.GetFileName(file)}' — {ex.Message}"); }
            }
        }

        private static string ClientFilePath(string id) =>
            Path.Combine(ClientsDir, $"{id}.json");
    }
}
