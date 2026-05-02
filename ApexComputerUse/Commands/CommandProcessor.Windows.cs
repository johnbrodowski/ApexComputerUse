using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace ApexComputerUse
{
    public partial class CommandProcessor
    {
        private CommandResponse CmdStatus() =>
            Ok("Current state",
               $"Window : {_windowDesc}\nElement: {_elementDesc}");

        private CommandResponse CmdListWindows()
        {
            _windowMap.Clear();
            var windows = _helper.GetDesktopWindows();
            var entries = windows.Select(w =>
            {
                var hwnd = w.Properties.NativeWindowHandle.ValueOrDefault;
                string hash = _idGen.GenerateElementHash(w, null, null, hwnd: hwnd, excludeName: true);
                int id = _idGen.GenerateIdFromHash(hash);
                _windowMap[id] = w.AsWindow();
                return new { id, title = w.Properties.Name.ValueOrDefault ?? "" };
            }).ToList();

            string json = System.Text.Json.JsonSerializer.Serialize(entries, FormatAdapter.s_indented);

            return Ok($"{entries.Count} open window(s)", json);
        }

        private CommandResponse CmdListElements(CommandRequest req)
        {
            if (CurrentWindow == null) return Fail("No window selected. Use 'find window=X' first.");

            var hwnd = CurrentWindow.Properties.NativeWindowHandle.ValueOrDefault;

            // Optional: start the scan at a previously-mapped element instead of the window root.
            // Lets callers progressively drill into a subtree without re-scanning the whole window.
            // When provided, the map is preserved (IDs remain stable across calls).
            int? startId = null;
            if (!string.IsNullOrWhiteSpace(req.AutomationId)
                && int.TryParse(req.AutomationId, out int parsedId))
            {
                startId = parsedId;
            }

            AutomationElement scanRoot;
            string? rootHashOverride = null;
            int?    rootIdOverride   = null;

            if (startId.HasValue)
            {
                if (hwnd != _mappedWindowHandle)
                    return Fail($"Element ID {startId} is stale (current window differs from the one that was scanned). Run /elements first.");
                if (!_elementMap.TryGetValue(startId.Value, out var startEl))
                    return Fail($"Element ID {startId} not in map. Run /elements first.");
                if (!_elementHashes.TryGetValue(startId.Value, out var startHash))
                    return Fail($"Element hash for ID {startId} missing. Run /elements first.");

                scanRoot         = startEl;
                rootHashOverride = startHash;
                rootIdOverride   = startId.Value;
                // Do NOT clear the map - we want to preserve existing IDs so callers can keep referencing them.
            }
            else
            {
                // Full-tree scan from the window root - clear the map and start fresh.
                if (hwnd != _mappedWindowHandle)
                {
                    _elementMap.Clear();
                    _elementHashes.Clear();
                    _elementReverse.Clear();
                    _elementInsertOrder.Clear();
                    _idGen.Reset();
                    _mappedWindowHandle = hwnd;
                }
                else
                {
                    _elementMap.Clear();
                    _elementHashes.Clear();
                    _elementReverse.Clear();
                    _elementInsertOrder.Clear();
                }
                scanRoot = CurrentWindow;
            }

            int? maxDepth = (req.Depth.HasValue && req.Depth.Value >= 0) ? req.Depth : null;

            var options = new ScanOptions
            {
                OnscreenOnly = req.OnscreenOnly,
                MatchAll     = !string.IsNullOrEmpty(req.Match),
                MaxDepth     = maxDepth,
                IncludePath  = req.IncludePath,
                IncludeExtra = string.Equals(req.Properties, "extra", StringComparison.OrdinalIgnoreCase)
            };

            var root = ScanElementsIntoMap(
                scanRoot, null, null,
                options,
                overrideHash: rootHashOverride,
                overrideId:   rootIdOverride);

            // Apply ControlType filter: prune tree to matching nodes (plus structural ancestors).
            ControlType? typeFilter = ResolveControlType(req.SearchType);
            if (typeFilter.HasValue && root != null)
                root = FilterTreeByType(root, typeFilter.Value, isRoot: true);

            // Text-search filter: prune to branches containing matches on Name/AutomationId/Value.
            if (!string.IsNullOrWhiteSpace(req.Match) && root != null)
                root = FilterTreeByMatch(root, req.Match!.Trim(), isRoot: true);

            // Single-child wrapper collapse - run last so IDs, paths, and descendant counts are
            // already set before we start hoisting children up through the tree.
            if (req.CollapseChains && root != null)
                root = CollapseSingleChildChains(root);

            int count = CountNodes(root);
            string treeHash = ComputeTreeHash(root);

            // Short-circuit for pollers that passed their last-seen hash: skip the expensive
            // JSON serialization entirely when the tree hasn't changed structurally.
            if (!string.IsNullOrEmpty(req.ChangedSince) &&
                string.Equals(req.ChangedSince, treeHash, StringComparison.Ordinal))
            {
                string shortJson = System.Text.Json.JsonSerializer.Serialize(
                    new { treeHash, notModified = true }, FormatAdapter.s_indented);
                return Ok($"{count} element(s) (unchanged)", shortJson);
            }

            string json = System.Text.Json.JsonSerializer.Serialize(
                new { treeHash, root }, FormatAdapter.s_indentedCamel);

            return Ok($"{count} element(s)", json);
        }

        /// <summary>
        /// Deterministic structural hash of the emitted tree. Only identity fields
        /// (id, controlType, automationId, name, rectangle, descendant counts) participate -
        /// the caller can safely pass this back as <see cref="CommandRequest.ChangedSince"/> to
        /// short-circuit unchanged polls. Uses SHA-256 so collisions aren't a concern for
        /// polling-level change detection.
        /// </summary>
        private static string ComputeTreeHash(ElementNode? root)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            using var ms  = new System.IO.MemoryStream();
            using (var writer = new System.IO.BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
                WriteNodeForHash(writer, root);
            ms.Position = 0;
            var bytes = sha.ComputeHash(ms);
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        private static void WriteNodeForHash(System.IO.BinaryWriter w, ElementNode? node)
        {
            if (node == null) { w.Write((byte)0); return; }
            w.Write((byte)1);
            w.Write(node.Id);
            w.Write(node.ControlType ?? "");
            w.Write(node.AutomationId ?? "");
            w.Write(node.Name ?? "");
            if (node.BoundingRectangle is { } r)
            {
                w.Write((byte)1);
                w.Write(r.X); w.Write(r.Y); w.Write(r.Width); w.Write(r.Height);
            }
            else w.Write((byte)0);
            w.Write(node.ChildCount ?? -1);
            w.Write(node.DescendantCount ?? -1);
            int childCount = node.Children?.Count ?? 0;
            w.Write(childCount);
            if (node.Children != null)
                foreach (var c in node.Children) WriteNodeForHash(w, c);
        }

    }
}

