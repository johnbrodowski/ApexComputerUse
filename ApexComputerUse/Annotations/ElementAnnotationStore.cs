using System.Text.Json;

namespace ApexComputerUse
{
    /// <summary>
    /// Thread-safe, hash-keyed store for per-element notes and exclusion flags.
    /// Modeled on <see cref="SceneStore"/> (single lock, JSON-on-disk). Persists
    /// the entire map as one flat file because annotations are low-cardinality
    /// (typically tens to a few hundred per machine) and each write touches the
    /// whole record set anyway.
    ///
    /// Construct once and inject into <see cref="CommandProcessor"/>; the element-tree
    /// scan reads from it (skip excluded, attach Note) and the new annotation
    /// HTTP routes mutate it.
    /// </summary>
    public sealed class ElementAnnotationStore
    {
        private static readonly string DefaultFile =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                         "ApexComputerUse", "annotations", "elements.json");

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            WriteIndented               = true,
            PropertyNameCaseInsensitive = true
        };

        private readonly string _file;
        private readonly Dictionary<string, ElementAnnotation> _byHash = new(StringComparer.Ordinal);
        private readonly object _lock = new();

        public ElementAnnotationStore(string? file = null)
        {
            _file = file ?? DefaultFile;
            Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
            LoadFromDisk();
        }

        // -- Reads (called inside the element-scan hot path) ----------------

        public bool IsExcluded(string hash)
        {
            if (string.IsNullOrEmpty(hash)) return false;
            lock (_lock)
                return _byHash.TryGetValue(hash, out var a) && a.Excluded;
        }

        public bool TryGetNote(string hash, out string note)
        {
            note = "";
            if (string.IsNullOrEmpty(hash)) return false;
            lock (_lock)
            {
                if (_byHash.TryGetValue(hash, out var a) && !string.IsNullOrEmpty(a.Note))
                {
                    note = a.Note!;
                    return true;
                }
            }
            return false;
        }

        public ElementAnnotation? Get(string hash)
        {
            lock (_lock)
                return _byHash.TryGetValue(hash, out var a) ? Clone(a) : null;
        }

        public ElementAnnotation[] ListAll()
        {
            lock (_lock)
                return [.. _byHash.Values.Select(Clone).OrderBy(a => a.UpdatedUtc)];
        }

        public ElementAnnotation[] ListExcluded()
        {
            lock (_lock)
                return [.. _byHash.Values.Where(a => a.Excluded).Select(Clone).OrderBy(a => a.UpdatedUtc)];
        }

        // -- Writes ---------------------------------------------------------

        /// <summary>
        /// Sets the note for <paramref name="hash"/>. Pass null/empty to clear the
        /// note; the record is deleted iff Note is cleared and Excluded is false.
        /// Optional descriptor fields are stored for human readability.
        /// </summary>
        public ElementAnnotation SetNote(string hash, string? note,
                                          string? controlType = null,
                                          string? name = null,
                                          string? automationId = null)
        {
            if (string.IsNullOrEmpty(hash)) throw new ArgumentException("hash required", nameof(hash));
            lock (_lock)
            {
                var a = Upsert(hash, controlType, name, automationId);
                a.Note       = string.IsNullOrEmpty(note) ? null : note;
                a.UpdatedUtc = DateTime.UtcNow;
                FinaliseAndPersist(a);
                return Clone(a);
            }
        }

        public ElementAnnotation SetExcluded(string hash, bool excluded,
                                              string? controlType = null,
                                              string? name = null,
                                              string? automationId = null)
        {
            if (string.IsNullOrEmpty(hash)) throw new ArgumentException("hash required", nameof(hash));
            lock (_lock)
            {
                var a = Upsert(hash, controlType, name, automationId);
                a.Excluded   = excluded;
                a.UpdatedUtc = DateTime.UtcNow;
                FinaliseAndPersist(a);
                return Clone(a);
            }
        }

        public bool Remove(string hash)
        {
            if (string.IsNullOrEmpty(hash)) return false;
            lock (_lock)
            {
                if (!_byHash.Remove(hash)) return false;
                SaveToDisk();
                return true;
            }
        }

        // -- Internal -------------------------------------------------------

        private ElementAnnotation Upsert(string hash, string? ct, string? name, string? aid)
        {
            if (!_byHash.TryGetValue(hash, out var a))
            {
                a = new ElementAnnotation { Hash = hash };
                _byHash[hash] = a;
            }
            // Refresh descriptors only when callers supply them — old values stick otherwise.
            if (ct   != null) a.ControlType  = ct;
            if (name != null) a.Name         = name;
            if (aid  != null) a.AutomationId = aid;
            return a;
        }

        private void FinaliseAndPersist(ElementAnnotation a)
        {
            // Garbage-collect empty records so the on-disk file doesn't accumulate dead entries
            // when a user un-excludes and then clears their note.
            if (string.IsNullOrEmpty(a.Note) && !a.Excluded)
                _byHash.Remove(a.Hash);
            SaveToDisk();
        }

        private void LoadFromDisk()
        {
            if (!File.Exists(_file)) return;
            try
            {
                string json = File.ReadAllText(_file);
                var arr = JsonSerializer.Deserialize<ElementAnnotation[]>(json, JsonOpts);
                if (arr == null) return;
                foreach (var a in arr)
                {
                    if (!string.IsNullOrEmpty(a.Hash))
                        _byHash[a.Hash] = a;
                }
            }
            catch (Exception ex)
            {
                AppLog.Warning($"ElementAnnotationStore: failed to load '{_file}' - {ex.Message}");
            }
        }

        private void SaveToDisk()
        {
            try
            {
                File.WriteAllText(_file,
                    JsonSerializer.Serialize(_byHash.Values, JsonOpts));
            }
            catch (Exception ex)
            {
                AppLog.Warning($"ElementAnnotationStore: failed to persist - {ex.Message}");
            }
        }

        private static ElementAnnotation Clone(ElementAnnotation a) => new()
        {
            Hash         = a.Hash,
            Note         = a.Note,
            Excluded     = a.Excluded,
            ControlType  = a.ControlType,
            Name         = a.Name,
            AutomationId = a.AutomationId,
            UpdatedUtc   = a.UpdatedUtc
        };
    }
}
