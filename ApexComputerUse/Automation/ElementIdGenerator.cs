using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace ApexComputerUse
{
    /// <summary>
    /// Generates deterministic IDs for UI elements based on their identity.
    /// Same element always gets the same ID across scans.
    /// </summary>
    public sealed class ElementIdGenerator
    {
        public bool showDefaultDebugOutput { get; set; } = false;




        private readonly Dictionary<string, int> _hashToId = new();
        private readonly object _lock = new();
        private int _nextId = 1;

        /// <summary>
        /// When true, uses simple incremental IDs (1, 2, 3...) for debugging.
        /// When false, uses hash-based deterministic IDs (default).
        /// </summary>
        public bool UseIncrementalIds { get; set; } = false;

        public void ShowDebugOutput(bool show)
        {
            showDefaultDebugOutput = show;
        }


        /// <summary>
        /// Generates a deterministic ID from a pre-computed hash.
        /// In incremental mode, uses sequential IDs but still tracks by hash for consistency.
        /// </summary>
        public int GenerateIdFromHash(string hash, bool forceUseIncrementalIds = false)
        {


            int id;

            if (UseIncrementalIds || forceUseIncrementalIds)
            {
                // Incremental mode - use sequential IDs but track by hash
                lock (_lock)
                {
                    if (_hashToId.TryGetValue(hash, out var existingId))
                    {
                        // Hash seen before - return same ID
                        id = existingId;
                    }
                    else
                    {
                        // New hash - assign next incremental ID
                        id = _nextId++;
                        _hashToId[hash] = id;
                    }
                }
                if (showDefaultDebugOutput)
                    AppLog.Debug($"[ElementIdGenerator] GenerateIdFromHash (Incremental): Hash: {hash.Substring(0, 8)}... -> ID: {id}");
            }
            else
            {
                // Hash-based mode
                id = ConvertHashToId(hash);
                if (showDefaultDebugOutput)
                    AppLog.Debug($"[ElementIdGenerator] GenerateIdFromHash (Hash): Hash: {hash.Substring(0, 8)}... -> ID: {id}");
            }

            return id;
        }


        /// <summary>
        /// Generates a unified hash for any element type.
        /// For Window/Pane types, excludes Name (unstable due to navigation) and includes hwnd.
        /// For other elements, includes Name for disambiguation (unless using incremental IDs).
        /// When UseIncrementalIds is true, Name is excluded for all elements to maintain stable IDs during rescans.
        /// </summary>
        public string GenerateElementHash(
            AutomationElement element, int? parentId = 0, string? parentHash = null, IntPtr? hwnd = null, bool excludeName = false, int siblingIndex = 0)
        {
            var props = element.Properties;
            var sb = new StringBuilder();

            // Include parent hash for tree-based uniqueness
            if (!string.IsNullOrEmpty(parentHash))
            {
                sb.Append(parentHash);
                sb.Append("|");
            }

            // Core stable properties (always included)
            sb.Append(SafeGetProperty<string>(() => props.ControlType.ValueOrDefault.ToString(), "Unknown"));
            sb.Append("|");
            sb.Append(SafeGetProperty<string>(() => props.ClassName.ValueOrDefault, "") ?? "");
            sb.Append("|");
            sb.Append(SafeGetProperty<string>(() => props.AutomationId.ValueOrDefault, "") ?? "");
            sb.Append("|");
            sb.Append(SafeGetProperty<string>(() => props.FrameworkId.ValueOrDefault, "") ?? "");
            sb.Append("|");

            // ProcessName for cross-session stability
            var processId = SafeGetProperty<int>(() => props.ProcessId.ValueOrDefault, 0);
            if (processId > 0)
            {
                try
                {
                    var process = Process.GetProcessById(processId);
                    sb.Append(process.ProcessName);
                }
                catch (Exception ex) { AppLog.Debug($"[ElementIdGenerator] Could not resolve process name for pid={processId}: {ex.Message}"); }
            }
            // Name: Exclude when:
            // 1. excludeName is explicitly true (Window/Pane elements)
            // 2. Using incremental IDs (for stability during content rescans)
            bool shouldExcludeName = excludeName || UseIncrementalIds;

            if (!shouldExcludeName)
            {
                sb.Append("|");
                sb.Append(SafeGetProperty<string>(() => props.Name.ValueOrDefault, "") ?? "");
            }

            // Sibling index: always include for non-root elements to avoid hash collisions
            // between sibling elements that share identical ControlType/Class/AutomationId.
            // Excluded for top-level windows (parentHash == null) since window z-order can
            // change across sessions, making position-based IDs unstable.
            if (!string.IsNullOrEmpty(parentHash))
            {
                sb.Append("|idx:");
                sb.Append(siblingIndex);
            }

            return ComputeHash(sb.ToString());
        }







        /// <summary>
        /// Resets the ID generator. Use when clearing the registry.
        /// </summary>
        public void Reset()
        {
            lock (_lock)
            {
                _hashToId.Clear();
                _nextId = 1;
            }
        }

        /// <summary>
        /// Gets the current highest ID.
        /// </summary>
        public int CurrentMaxId
        {
            get
            {
                lock (_lock)
                {
                    return _nextId - 1;
                }
            }
        }

        /// <summary>
        /// Gets the number of unique hashes generated.
        /// </summary>
        public int UniqueHashCount
        {
            get
            {
                lock (_lock)
                {
                    return _hashToId.Count;
                }
            }
        }

        /// <summary>
        /// Converts a hash string directly to a numeric ID.
        /// Same hash always produces the same ID, regardless of session or discovery order.
        /// </summary>
        private int ConvertHashToId(string hash)
        {
            // Take first (8) characters of hex hash and convert to int
            // This gives us a deterministic ID from the hash itself
            var hashSubstring = hash.Substring(0, 8);
            var hashValue = Convert.ToInt32(hashSubstring, 16);

            // Use absolute value to ensure positive ID
            // Keep within reasonable int range (avoid overflow issues)
            var result = Math.Abs(hashValue);

            if (showDefaultDebugOutput) AppLog.Debug($"[ElementIdGenerator] ConvertHashToId: '{hashSubstring}' (hex) = {hashValue} (int) = {result} (abs)");

            return result;
        }

        /// <summary>
        /// Legacy method kept for backward compatibility.
        /// Now just converts hash to ID directly without tracking.
        /// </summary>
        private int GetOrCreateId(string hash)
        {
            return ConvertHashToId(hash);
        }

        private static string ComputeHash(string input)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(input);
            var hashBytes = sha256.ComputeHash(bytes);

            // Convert to hex string (first 16 bytes = 32 chars)
            var sb = new StringBuilder(32);
            for (int i = 0; i < 16; i++)
            {
                sb.Append(hashBytes[i].ToString("x2"));
            }
            return sb.ToString();
        }

        private static T SafeGetProperty<T>(Func<T> getter, T defaultValue)
        {
            try
            {
                return getter();
            }
            catch
            {
                return defaultValue;
            }
        }
    }
}
