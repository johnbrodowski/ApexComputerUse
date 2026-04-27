using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace ApexComputerUse
{
    public partial class CommandProcessor
    {
        // ── Action runner (remote) ────────────────────────────────────────

        private string RunAction(AutomationElement el, Window? win, string action, string input)
        {
            return action.ToLowerInvariant() switch
            {
                // ── Click / Mouse ─────────────────────────────────────────
                "click"                              => Do(() => _helper.ClickElement(el)),
                "mouse-click" or "mouseclick"        => Do(() => _helper.MouseClickElement(el)),
                "invoke"                             => Do(() => _helper.InvokeButton(el)),
                "right-click" or "rightclick"        => Do(() => _helper.RightClickElement(el)),
                "double-click" or "doubleclick"      => Do(() => _helper.DoubleClickElement(el)),
                "middle-click" or "middleclick"      => Do(() => _helper.MiddleClickElement(el)),
                "click-at"    or "clickat"           => Do(() => { var p = ParsePair(input); _helper.ClickAtOffset(el, p.a, p.b); }),
                "hover"                              => Do(() => _helper.HoverElement(el)),
                "drag"                               => Do(() => { var p = ParsePair(input); _helper.DragAndDropToPoint(el, p.a, p.b); }),
                "highlight"                          => Do(() => _helper.HighlightElement(el)),

                // ── Focus / State ─────────────────────────────────────────
                "focus"                              => Do(() => _helper.SetFocus(el)),
                "isenabled"                          => _helper.IsElementEnabled(el),
                "isvisible"                          => _helper.IsElementVisible(el),
                "describe"                           => _helper.Describe(el),
                "patterns"                           => _helper.GetSupportedPatterns(el),
                "bounds"                             => _helper.GetBoundingRect(el),

                // ── Text / Value ──────────────────────────────────────────
                "gettext"  or "text"                 => _helper.GetText(el),
                "getvalue" or "value"                => _helper.GetValue(el),
                "getselectedtext"                    => _helper.GetSelectedText(el),
                "type"     or "enter"                => Do(() => _helper.EnterText(el, input)),
                "insert"                             => Do(() => _helper.InsertTextAtCaret(el, input)),
                "setvalue"                           => Do(() => _helper.SetValue(el, input)),
                "clearvalue"                         => Do(() => _helper.ClearValue(el)),
                "appendvalue"                        => Do(() => _helper.AppendValue(el, input)),
                "selectall"                          => Do(() => _helper.SelectAllText(el)),
                "copy"                               => Do(() => _helper.CopyText(el)),
                "cut"                                => Do(() => _helper.CutText(el)),
                "paste"                              => Do(() => _helper.PasteText(el)),
                "undo"                               => Do(() => _helper.UndoText(el)),
                "clear"                              => Do(() => _helper.ClearText(el)),

                // ── Keyboard ──────────────────────────────────────────────
                "keys"                               => Do(() => _helper.SendKeysEnhanced(el, input)),

                // ── Range / Slider ────────────────────────────────────────
                "setrange"                           => Do(() => _helper.SetRangeValue(el, ParseDoubleOr(input, 0))),
                "getrange"                           => _helper.GetRangeValue(el),
                "rangeinfo"                          => _helper.GetRangeInfo(el),

                // ── Toggle ────────────────────────────────────────────────
                "toggle"                             => Do(() => _helper.ToggleCheckBox(el)),
                "toggle-on"  or "toggleon"           => Do(() => _helper.SetToggleState(el, true)),
                "toggle-off" or "toggleoff"          => Do(() => _helper.SetToggleState(el, false)),
                "gettoggle"                          => _helper.GetToggleState(el),

                // ── ExpandCollapse ────────────────────────────────────────
                "expand"                             => Do(() => el.Patterns.ExpandCollapse.Pattern.Expand()),
                "collapse"                           => Do(() => el.Patterns.ExpandCollapse.Pattern.Collapse()),
                "expandstate"                        => _helper.GetExpandCollapseState(el),

                // ── Selection (SelectionItem) ─────────────────────────────
                "select-item" or "selectitem"        => Do(() => _helper.SelectItem(el)),
                "addselect"                          => Do(() => _helper.AddToSelection(el)),
                "removeselect"                       => Do(() => _helper.RemoveFromSelection(el)),
                "isselected"                         => _helper.IsSelected(el),
                "getselection"                       => _helper.GetSelectionInfo(el),

                // ── ComboBox / ListBox ────────────────────────────────────
                "select"                             => Do(() => _helper.SelectComboBoxItem(el, input)),
                "select-index" or "selectindex"      => Do(() => _helper.SelectByIndex(el, ParseIntOr(input, 0))),
                "getitems"                           => string.Join("\n", _helper.GetComboBoxItems(el)),
                "getselecteditem"                    => _helper.GetComboBoxSelected(el),

                // ── Window ────────────────────────────────────────────────
                "minimize"   => Do(() => { if (win != null) _helper.MinimizeWindow(win); }),
                "maximize"   => Do(() => { if (win != null) _helper.MaximizeWindow(win); }),
                "restore"    => Do(() => { if (win != null) _helper.RestoreWindow(win); }),
                "windowstate"                        => _helper.GetWindowState(el),

                // ── Transform ────────────────────────────────────────────
                "move"                               => Do(() => { var p = ParsePair(input); _helper.MoveElement(el, p.a, p.b); }),
                "resize"                             => Do(() => { var p = ParsePair(input); _helper.ResizeElement(el, p.a, p.b); }),

                // ── Scroll ────────────────────────────────────────────────
                "scroll-up"   or "scrollup"          => Do(() => _helper.ScrollUp(ParseIntOr(input, 3))),
                "scroll-down" or "scrolldown"        => Do(() => _helper.ScrollDown(ParseIntOr(input, 3))),
                "scroll-left" or "scrollleft"        => Do(() => _helper.HorizontalScroll(-ParseIntOr(input, 3))),
                "scroll-right" or "scrollright"      => Do(() => _helper.HorizontalScroll(ParseIntOr(input, 3))),
                "scrollinto"  or "scrollintoview"    => Do(() => _helper.ScrollIntoView(el)),
                "scrollpercent"                      => Do(() => { var p = ParsePairD(input); _helper.ScrollByPercent(el, p.a, p.b); }),
                "getscrollinfo"                      => _helper.GetScrollInfo(el),

                // ── Grid / Table ──────────────────────────────────────────
                "griditem"                           => _helper.GetGridItem(el, ParsePair(input).a, ParsePair(input).b),
                "gridinfo"                           => _helper.GetGridInfo(el),
                "griditeminfo"                       => _helper.GetGridItemInfo(el),

                // ── Screenshot ────────────────────────────────────────────
                "screenshot" or "capture"            => _helper.CaptureElement(el, CaptureFolder()),

                // ── Wait ──────────────────────────────────────────────────
                "wait"                               => WaitFor(input),

                _ => $"Unknown action '{action}'. Send 'help' for a list."
            };
        }

        private string WaitFor(string automationId)
        {
            if (CurrentWindow == null) return "No window selected.";
            var el = _helper.WaitForElement(CurrentWindow, automationId);
            if (el == null) return $"'{automationId}' did not appear within timeout.";
            CurrentElement = el;
            _elementDesc   = _helper.Describe(el);
            return _elementDesc;
        }

        private const int ScanMaxDepth    = 25;
        private const int ScanChildTimeout = 2000; // ms per FindAllChildren call

        /// <summary>
        /// Bundle of options threaded through <see cref="ScanElementsIntoMap"/>. Kept as a
        /// struct so the recursion signature stays readable as more opt-in features land.
        /// </summary>
        private readonly struct ScanOptions
        {
            public bool    OnscreenOnly    { get; init; }
            public int?    MaxDepth        { get; init; }
            public bool    IncludePath     { get; init; }
            public bool    IncludeExtra    { get; init; }  // properties=extra → value + helpText
        }

        private ElementNode? ScanElementsIntoMap(
            AutomationElement el, string? parentHash, int? parentId,
            ScanOptions options,
            int siblingIndex = 0, int depth = 0,
            string? parentPath = null,
            string? overrideHash = null, int? overrideId = null)
        {
            if (depth > ScanMaxDepth) return null;

            // Onscreen filter — skip element and its entire subtree if off-viewport.
            // depth == 0 is always the scan root (window or expansion target); never filter it out.
            if (options.OnscreenOnly && depth > 0 && el.Properties.IsOffscreen.ValueOrDefault)
                return null;

            try
            {
                var ct = el.Properties.ControlType.ValueOrDefault;
                bool isWindowOrPane = ct == ControlType.Window || ct == ControlType.Pane;

                // If the caller supplied an override (lazy-expansion of a previously-mapped node),
                // reuse its stored hash/id so descendants hash identically to the original scan.
                string hash;
                int    id;
                if (overrideHash != null && overrideId.HasValue)
                {
                    hash = overrideHash;
                    id   = overrideId.Value;
                }
                else
                {
                    hash = _idGen.GenerateElementHash(el, parentId, parentHash,
                              excludeName: isWindowOrPane, siblingIndex: siblingIndex);
                    id = _idGen.GenerateIdFromHash(hash);
                }
                if (_elementMap.Count >= 50_000)
                {
                    _elementMap.Clear();
                    _elementHashes.Clear();
                    OnLog?.Invoke("[Warn] Element map cleared (50k cap reached).");
                }
                _elementMap[id]    = el;
                _elementHashes[id] = hash;

                bool truncate = options.MaxDepth.HasValue && depth >= options.MaxDepth.Value;

                string nameProp = el.Properties.Name.ValueOrDefault ?? "";

                // Build the ancestor breadcrumb — each level is "ControlType" if Name is empty,
                // otherwise "Name". Root is just the root's label. Only materialised when the
                // caller asked for IncludePath, so we don't burn string allocations otherwise.
                string? path = null;
                if (options.IncludePath)
                {
                    string segment = string.IsNullOrEmpty(nameProp) ? ct.ToString() : nameProp;
                    path = string.IsNullOrEmpty(parentPath) ? segment : $"{parentPath} > {segment}";
                }

                // Fetch children on a background thread with a timeout to avoid
                // hanging on UWP-hosted elements that block UIA traversal indefinitely.
                // We fetch even at the truncation boundary so we can report childCount.
                AutomationElement[]? children = null;
                try
                {
                    var fetchTask = Task.Run(() => el.FindAllChildren());
                    children = fetchTask.Wait(ScanChildTimeout) ? fetchTask.Result : null;
                }
                catch (Exception ex) { AppLog.Debug($"[Scan] FindAllChildren failed — {ex.Message}"); children = null; }

                List<ElementNode>? childNodes        = null;
                int?               childCountOut     = null;
                int?               descendantCountOut = null;

                if (truncate)
                {
                    // Depth limit hit — omit children, report their counts so callers know to drill in.
                    if (children != null && children.Length > 0)
                    {
                        childCountOut      = children.Length;
                        // Count all descendants below the cutoff so the caller can decide whether
                        // drilling in is cheap or expensive. Respects the onscreen filter so the
                        // count matches what a follow-up /elements?id=<id>&onscreen=true would emit.
                        descendantCountOut = CountDescendantsBelow(el, options.OnscreenOnly);
                    }
                }
                else if (children != null)
                {
                    for (int i = 0; i < children.Length; i++)
                    {
                        var child = ScanElementsIntoMap(
                            children[i], hash, id, options,
                            siblingIndex: i, depth: depth + 1,
                            parentPath: path);
                        if (child != null)
                        {
                            childNodes ??= new List<ElementNode>();
                            childNodes.Add(child);
                        }
                    }
                }

                var bounds = el.BoundingRectangle;
                var boundingRect = (bounds.Width > 0 || bounds.Height > 0)
                    ? new BoundingRect
                    {
                        X      = (int)bounds.X,
                        Y      = (int)bounds.Y,
                        Width  = (int)bounds.Width,
                        Height = (int)bounds.Height
                    }
                    : null;

                // Opt-in properties — read only when the caller asked for them so default scans
                // keep their existing 4-property budget.
                string? valueOut    = options.IncludeExtra ? _helper.ReadValuePattern(el) : null;
                string? helpTextOut = options.IncludeExtra ? _helper.ReadHelpText(el)     : null;
                if (string.IsNullOrEmpty(valueOut))    valueOut    = null;
                if (string.IsNullOrEmpty(helpTextOut)) helpTextOut = null;

                return new ElementNode
                {
                    Id                = id,
                    ControlType       = ct.ToString(),
                    Name              = nameProp,
                    AutomationId      = el.Properties.AutomationId.ValueOrDefault ?? "",
                    BoundingRectangle = boundingRect,
                    Children          = childNodes,
                    ChildCount        = childCountOut,
                    DescendantCount   = descendantCountOut,
                    Path              = path,
                    Value             = valueOut,
                    HelpText          = helpTextOut
                };
            }
            catch { return null; } // element became stale mid-scan — skip silently
        }

        /// <summary>
        /// Cheap count of all live descendants under <paramref name="el"/>, respecting the
        /// onscreen filter so the number matches what the caller would see if they drilled in
        /// via <c>/elements?id=&lt;id&gt;</c>. No ID hashing, no node construction, no mapping —
        /// just walks children to produce an integer so the caller can estimate cost.
        /// </summary>
        private static int CountDescendantsBelow(AutomationElement el, bool onscreenOnly)
        {
            AutomationElement[]? children;
            try
            {
                var fetchTask = Task.Run(() => el.FindAllChildren());
                children = fetchTask.Wait(ScanChildTimeout) ? fetchTask.Result : null;
            }
            catch { return 0; }
            if (children == null || children.Length == 0) return 0;

            int total = 0;
            foreach (var c in children)
            {
                try
                {
                    if (onscreenOnly && c.Properties.IsOffscreen.ValueOrDefault) continue;
                    total += 1 + CountDescendantsBelow(c, onscreenOnly);
                }
                catch { /* stale child — skip */ }
            }
            return total;
        }

        // Internal (not private) so the test project (InternalsVisibleTo) can construct
        // synthetic trees to verify FilterTreeByMatch / CollapseSingleChildChains / descendant counts.
        internal sealed class ElementNode
        {
            public int                 Id                { get; init; }
            public string              ControlType       { get; init; } = "";
            public string              Name              { get; init; } = "";
            public string              AutomationId      { get; init; } = "";
            public BoundingRect?       BoundingRectangle { get; init; }
            public List<ElementNode>?  Children          { get; init; }
            public int?                ChildCount        { get; init; }  // set only when children are omitted due to a depth limit — tells the caller it can expand this node via /elements?id=<Id>

            // ── Opt-in fields (emitted only when populated; JsonIgnoreCondition.WhenWritingNull keeps payloads small) ──
            public int?                DescendantCount   { get; init; }  // set alongside ChildCount on truncated nodes — total transitive descendants the caller could still drill into
            public string?             Path              { get; init; }  // ancestor breadcrumb, e.g. "Chrome > Document > Form" — set only when the caller requested IncludePath
            public string?             Value             { get; init; }  // Value pattern content — set only when the caller requested properties=extra
            public string?             HelpText          { get; init; }  // HelpText property — set only when the caller requested properties=extra
        }

        internal sealed class BoundingRect
        {
            public int X      { get; init; }
            public int Y      { get; init; }
            public int Width  { get; init; }
            public int Height { get; init; }
        }

        // ── Helpers ───────────────────────────────────────────────────────

        /// <summary>
        /// Recursively prunes an element tree to nodes whose ControlType matches
        /// <paramref name="typeFilter"/>, retaining structural ancestors (Window/Pane/Group)
        /// that lead to matching descendants so that spatial context is preserved.
        /// </summary>
        private static ElementNode? FilterTreeByType(ElementNode node, ControlType typeFilter, bool isRoot)
        {
            List<ElementNode>? filteredChildren = null;
            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    var filtered = FilterTreeByType(child, typeFilter, isRoot: false);
                    if (filtered != null)
                    {
                        filteredChildren ??= new List<ElementNode>();
                        filteredChildren.Add(filtered);
                    }
                }
            }

            bool keep = isRoot
                || node.ControlType.Equals(typeFilter.ToString(), StringComparison.OrdinalIgnoreCase)
                || filteredChildren != null;

            if (!keep) return null;

            return CloneWithChildren(node, filteredChildren);
        }

        /// <summary>
        /// Prunes the tree to branches whose Name, AutomationId, or Value (when populated)
        /// contains <paramref name="needle"/> (case-insensitive). Matching nodes keep their
        /// full subtree; non-matching ancestors are preserved only when they lie on the path
        /// to a match, giving the caller a breadcrumb without siblings.
        /// </summary>
        internal static ElementNode? FilterTreeByMatch(ElementNode node, string needle, bool isRoot)
        {
            if (string.IsNullOrEmpty(needle)) return node;

            bool selfMatches = MatchesNode(node, needle);

            // If this node matches, keep its entire subtree — the agent probably wants
            // context below the match. Non-matching siblings beneath a match are fine;
            // they're the siblings of a match, not unrelated noise.
            if (selfMatches) return node;

            List<ElementNode>? keptChildren = null;
            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    var kept = FilterTreeByMatch(child, needle, isRoot: false);
                    if (kept != null)
                    {
                        keptChildren ??= new List<ElementNode>();
                        keptChildren.Add(kept);
                    }
                }
            }

            // Keep the root so callers always get a valid tree skeleton, even if zero matches.
            // Keep intermediate nodes only when a descendant matched (they serve as breadcrumbs).
            if (!isRoot && keptChildren == null) return null;

            return CloneWithChildren(node, keptChildren);
        }

        private static bool MatchesNode(ElementNode node, string needle)
        {
            if (!string.IsNullOrEmpty(node.Name)         && node.Name.Contains(needle,         StringComparison.OrdinalIgnoreCase)) return true;
            if (!string.IsNullOrEmpty(node.AutomationId) && node.AutomationId.Contains(needle, StringComparison.OrdinalIgnoreCase)) return true;
            if (!string.IsNullOrEmpty(node.Value)        && node.Value.Contains(needle,        StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>
        /// Folds chains of identity-less single-child wrappers — e.g. the "1-in-1-in-1" Pane/Group
        /// chains browsers emit around every piece of web content. A node is collapsed when it has
        /// exactly one child AND has no Name AND has no AutomationId AND its ControlType is one of
        /// the generic containers (Pane/Group/Custom). IDs are never rewritten — the hoisted child
        /// keeps its original ID so follow-up /elements?id=&lt;id&gt; calls still resolve.
        /// </summary>
        internal static ElementNode? CollapseSingleChildChains(ElementNode? node)
        {
            if (node == null) return null;

            // Recurse first so we collapse bottom-up.
            List<ElementNode>? newChildren = null;
            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    var reduced = CollapseSingleChildChains(child);
                    if (reduced != null)
                    {
                        newChildren ??= new List<ElementNode>();
                        newChildren.Add(reduced);
                    }
                }
            }

            // A node qualifies for collapse if it's an identity-less wrapper with exactly one
            // surviving child. The child (possibly already collapsed) is hoisted into our place.
            if (IsCollapsibleWrapper(node) && newChildren != null && newChildren.Count == 1)
                return newChildren[0];

            return CloneWithChildren(node, newChildren);
        }

        private static bool IsCollapsibleWrapper(ElementNode node)
        {
            if (!string.IsNullOrEmpty(node.Name))         return false;
            if (!string.IsNullOrEmpty(node.AutomationId)) return false;
            return node.ControlType.Equals("Pane",   StringComparison.OrdinalIgnoreCase)
                || node.ControlType.Equals("Group",  StringComparison.OrdinalIgnoreCase)
                || node.ControlType.Equals("Custom", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Clones a node, substituting a new children list but preserving every other field.</summary>
        private static ElementNode CloneWithChildren(ElementNode node, List<ElementNode>? newChildren) =>
            new()
            {
                Id                = node.Id,
                ControlType       = node.ControlType,
                Name              = node.Name,
                AutomationId      = node.AutomationId,
                BoundingRectangle = node.BoundingRectangle,
                Children          = newChildren,
                ChildCount        = node.ChildCount,
                DescendantCount   = node.DescendantCount,
                Path              = node.Path,
                Value             = node.Value,
                HelpText          = node.HelpText
            };

        private static int CountNodes(ElementNode? node)
        {
            if (node == null) return 0;
            int count = 1;
            if (node.Children != null)
                foreach (var child in node.Children)
                    count += CountNodes(child);
            return count;
        }
    }
}
