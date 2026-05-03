using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;

namespace ApexComputerUse
{
    public enum FuzzyMatchStatus
    {
        NoCandidates,
        NoMatch,
        Exact,
        Accepted,
        LowConfidence,
        Ambiguous
    }

    public sealed record FuzzyMatchCandidate(
        string Name,
        string AutomationId,
        string ControlType,
        string MatchType,
        double Score,
        int Distance);

    public sealed class FuzzyMatchResult<T>
        where T : class
    {
        public FuzzyMatchStatus Status { get; init; }
        public T? Element { get; init; }
        public string MatchedValue { get; init; } = "";
        public bool IsExact => Status == FuzzyMatchStatus.Exact;
        public IReadOnlyList<FuzzyMatchCandidate> Candidates { get; init; } = Array.Empty<FuzzyMatchCandidate>();
        public bool Success => Element != null && Status is FuzzyMatchStatus.Exact or FuzzyMatchStatus.Accepted;
    }

    public partial class ApexHelper
    {
        // -- Window --------------------------------------------------------

        public Window? FindWindow(string titleContains)
        {
            var desktop = _automation.GetDesktop();
            var match = desktop
                .FindAllChildren()
                .FirstOrDefault(w => w.Name.Contains(titleContains, StringComparison.OrdinalIgnoreCase));
            return match?.AsWindow();
        }

        public void MoveWindow(Window window, int x, int y) =>
            window.Patterns.Transform.Pattern.Move(x, y);

        public void ResizeWindow(Window window, double width, double height) =>
            window.Patterns.Transform.Pattern.Resize(width, height);

        public void MinimizeWindow(Window window) =>
            window.Patterns.Window.Pattern.SetWindowVisualState(WindowVisualState.Minimized);

        public void MaximizeWindow(Window window) =>
            window.Patterns.Window.Pattern.SetWindowVisualState(WindowVisualState.Maximized);

        public void RestoreWindow(Window window) =>
            window.Patterns.Window.Pattern.SetWindowVisualState(WindowVisualState.Normal);

        public void CloseWindow(Window window) =>
            window.Close();

        /// <summary>Gets Normal / Maximized / Minimized state of the element's window.</summary>
        public string GetWindowState(AutomationElement el)
        {
            if (el.Patterns.Window.TryGetPattern(out var wp))
                return wp.WindowVisualState.ValueOrDefault.ToString();
            return "Window pattern not supported";
        }

        // -- Window fuzzy find ---------------------------------------------

        public Window? FindWindowFuzzy(string title, out string matchedTitle, out bool isExact)
        {
            var result = FindWindowFuzzyDetailed(title);
            matchedTitle = result.MatchedValue;
            isExact = result.IsExact;
            return result.Success ? result.Element : null;
        }

        public FuzzyMatchResult<Window> FindWindowFuzzyDetailed(string title)
        {
            // Snapshot Name once per window. UIA property reads can throw spuriously
            // (EVENT_E_INTERNALEXCEPTION / 0x80040201) when the desktop tree mutates
            // mid-call - reading Name 5-8 times per window across this pipeline made
            // the failure routine. One read per window, wrapped in try/catch, behaves
            // like GetDesktopWindows() (which doesn't throw in practice).
            var all = _automation.GetDesktop().FindAllChildren()
                          .Select(w =>
                          {
                              string name;
                              try { name = w.Name ?? ""; }
                              catch { name = ""; }
                              return (element: w, name);
                          })
                          .Where(t => !string.IsNullOrWhiteSpace(t.name))
                          .ToArray();

            var ranked = RankTextCandidates(
                title,
                all.Select(t => new FuzzyMatchCandidateSource<AutomationElement>(
                    t.element, t.name, "", "Window")));
            return BuildElementResult(title, ranked, t => t.Source.AsWindow());
        }

        // -- Element fuzzy find --------------------------------------------

        public AutomationElement? FindElementFuzzy(
            Window window, string searchText, ControlType? filterType, bool searchById,
            out string matchedValue, out bool isExact)
        {
            var result = FindElementFuzzyDetailed(window, searchText, filterType, searchById);
            matchedValue = result.MatchedValue;
            isExact = result.IsExact;
            return result.Success ? result.Element : null;
        }

        public FuzzyMatchResult<AutomationElement> FindElementFuzzyDetailed(
            Window window, string searchText, ControlType? filterType, bool searchById)
        {
            AutomationElement[] all;
            try
            {
                var fetchTask = filterType.HasValue
                    ? Task.Run(() => window.FindAllDescendants(cf => cf.ByControlType(filterType.Value)))
                    : Task.Run(() => window.FindAllDescendants());
                if (!fetchTask.Wait(5000) || fetchTask.Result == null)
                    return new FuzzyMatchResult<AutomationElement> { Status = FuzzyMatchStatus.NoCandidates };
                all = fetchTask.Result;
            }
            catch { return new FuzzyMatchResult<AutomationElement> { Status = FuzzyMatchStatus.NoCandidates }; }

            if (all.Length == 0)
                return new FuzzyMatchResult<AutomationElement> { Status = FuzzyMatchStatus.NoCandidates };

            var sources = new List<FuzzyMatchCandidateSource<AutomationElement>>();
            foreach (var el in all)
            {
                string name = SafeRead(() => el.Properties.Name.ValueOrDefault ?? "");
                string automationId = SafeRead(() => el.Properties.AutomationId.ValueOrDefault ?? "");
                string controlType = SafeRead(() => el.Properties.ControlType.ValueOrDefault.ToString(), "Unknown");
                string primary = searchById ? automationId : name;
                if (string.IsNullOrWhiteSpace(primary)) continue;
                sources.Add(new FuzzyMatchCandidateSource<AutomationElement>(el, primary, automationId, controlType, name));
            }

            var ranked = RankTextCandidates(searchText, sources);
            return BuildElementResult(searchText, ranked, t => t.Source);
        }

        public static IReadOnlyList<FuzzyMatchCandidate> RankTextCandidates(
            string query,
            IEnumerable<(string Value, string AutomationId, string ControlType)> candidates)
        {
            return RankTextCandidates(
                    query,
                    candidates.Select(c => new FuzzyMatchCandidateSource<object>(
                        new object(), c.Value, c.AutomationId, c.ControlType)))
                .Select(t => t.Candidate)
                .ToArray();
        }

        public static FuzzyMatchStatus ClassifyRankedCandidates(IReadOnlyList<FuzzyMatchCandidate> ranked)
        {
            if (ranked.Count == 0) return FuzzyMatchStatus.NoCandidates;
            var best = ranked[0];
            if (best.MatchType == "exact") return FuzzyMatchStatus.Exact;
            if (best.Score < 0.78) return FuzzyMatchStatus.LowConfidence;
            if (ranked.Count > 1 && best.Score - ranked[1].Score < 0.08)
                return FuzzyMatchStatus.Ambiguous;
            if (best.MatchType == "prefix" && best.Score >= 0.84) return FuzzyMatchStatus.Accepted;
            if (best.MatchType == "substring" && best.Score >= 0.86) return FuzzyMatchStatus.Accepted;
            return FuzzyMatchStatus.LowConfidence;
        }

        private sealed record FuzzyMatchCandidateSource<T>(
            T Source,
            string Value,
            string AutomationId,
            string ControlType,
            string? DisplayName = null);

        private sealed record RankedFuzzyMatch<T>(
            T Source,
            string Value,
            FuzzyMatchCandidate Candidate);

        private static FuzzyMatchResult<TOut> BuildElementResult<TIn, TOut>(
            string query,
            IReadOnlyList<RankedFuzzyMatch<TIn>> ranked,
            Func<RankedFuzzyMatch<TIn>, TOut> convert)
            where TOut : class
        {
            var candidates = ranked.Select(t => t.Candidate).ToArray();
            var status = ClassifyRankedCandidates(candidates);
            if (status is FuzzyMatchStatus.Exact or FuzzyMatchStatus.Accepted)
            {
                var best = ranked[0];
                return new FuzzyMatchResult<TOut>
                {
                    Status = status,
                    Element = convert(best),
                    MatchedValue = best.Value,
                    Candidates = candidates
                };
            }

            return new FuzzyMatchResult<TOut>
            {
                Status = status == FuzzyMatchStatus.NoCandidates ? FuzzyMatchStatus.NoCandidates :
                    (candidates.Length == 0 ? FuzzyMatchStatus.NoMatch : status),
                MatchedValue = "",
                Candidates = candidates
            };
        }

        private static IReadOnlyList<RankedFuzzyMatch<T>> RankTextCandidates<T>(
            string query,
            IEnumerable<FuzzyMatchCandidateSource<T>> candidates)
        {
            string q = (query ?? "").Trim();
            if (string.IsNullOrEmpty(q)) return Array.Empty<RankedFuzzyMatch<T>>();
            string qLower = q.ToLowerInvariant();

            return candidates
                .Select(c =>
                {
                    string value = c.Value.Trim();
                    string valueLower = value.ToLowerInvariant();
                    int distance = Levenshtein(qLower, valueLower);
                    string matchType;
                    double score;

                    if (value.Equals(q, StringComparison.OrdinalIgnoreCase))
                    {
                        matchType = "exact";
                        score = 1.0;
                    }
                    else if (value.StartsWith(q, StringComparison.OrdinalIgnoreCase))
                    {
                        matchType = "prefix";
                        score = 0.94 - Math.Min(0.12, (value.Length - q.Length) / Math.Max(value.Length, 1d) * 0.12);
                    }
                    else
                    {
                        int index = valueLower.IndexOf(qLower, StringComparison.Ordinal);
                        if (index >= 0)
                        {
                            matchType = "substring";
                            score = 0.87 - Math.Min(0.12, index / Math.Max(value.Length, 1d) * 0.12);
                        }
                        else
                        {
                            matchType = "fuzzy";
                            score = 1.0 - (distance / (double)Math.Max(qLower.Length, valueLower.Length));
                        }
                    }

                    score = Math.Clamp(score, 0, 1);
                    var candidate = new FuzzyMatchCandidate(
                        c.DisplayName ?? value,
                        c.AutomationId,
                        c.ControlType,
                        matchType,
                        Math.Round(score, 3),
                        distance);
                    return new RankedFuzzyMatch<T>(c.Source, value, candidate);
                })
                .Where(t => t.Candidate.Score >= 0.35)
                .OrderByDescending(t => t.Candidate.Score)
                .ThenBy(t => t.Candidate.Name.Length)
                .Take(5)
                .ToArray();
        }

        private static string SafeRead(Func<string> read, string fallback = "")
        {
            try { return read() ?? fallback; }
            catch { return fallback; }
        }

        // -- Levenshtein ---------------------------------------------------

        public static int Levenshtein(string a, string b)
        {
            if (a.Length == 0) return b.Length;
            if (b.Length == 0) return a.Length;
            var d = new int[a.Length + 1, b.Length + 1];
            for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
            for (int j = 0; j <= b.Length; j++) d[0, j] = j;
            for (int i = 1; i <= a.Length; i++)
                for (int j = 1; j <= b.Length; j++)
                    d[i, j] = a[i - 1] == b[j - 1]
                        ? d[i - 1, j - 1]
                        : 1 + Math.Min(d[i - 1, j - 1], Math.Min(d[i - 1, j], d[i, j - 1]));
            return d[a.Length, b.Length];
        }

        // -- Element lookup ------------------------------------------------

        public AutomationElement? FindByAutomationId(Window window, string automationId) =>
            window.FindFirstDescendant(cf => cf.ByAutomationId(automationId));

        public AutomationElement? FindByName(Window window, string name) =>
            window.FindFirstDescendant(cf => cf.ByName(name));

        public AutomationElement? WaitForElement(Window window, string automationId, int timeoutMs = 5000) =>
            Retry.WhileNull(
                () => window.FindFirstDescendant(cf => cf.ByAutomationId(automationId)),
                TimeSpan.FromMilliseconds(timeoutMs),
                TimeSpan.FromMilliseconds(200)
            ).Result;

        public string WaitPageLoad(Window window, int timeoutSeconds = 10)
        {
            var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
            string? lastTitle = null;
            DateTime? stableAt = null;

            while (DateTime.UtcNow < deadline)
            {
                Thread.Sleep(200);
                string title = window.Properties.Name.ValueOrDefault ?? "";
                if (title.Contains("Loading", StringComparison.OrdinalIgnoreCase))
                { lastTitle = null; stableAt = null; continue; }
                if (title != lastTitle)
                { lastTitle = title; stableAt = DateTime.UtcNow.AddMilliseconds(500); }
                else if (stableAt.HasValue && DateTime.UtcNow >= stableAt.Value && !string.IsNullOrEmpty(title))
                    return $"Page loaded: {title}";
            }
            return $"Timeout waiting for page load (last title: {lastTitle ?? "unknown"})";
        }

        // -- Discovery -----------------------------------------------------

        public AutomationElement[] GetDesktopWindows() =>
            _automation.GetDesktop()
                .FindAllChildren()
                .Where(w => !string.IsNullOrWhiteSpace(w.Name))
                .ToArray();

        public string[] ListWindowTitles() =>
            GetDesktopWindows().Select(w => w.Name).OrderBy(x => x).ToArray();

        public string[] ListElements(Window window, ControlType? filterType = null)
        {
            var all = filterType.HasValue
                ? window.FindAllDescendants(cf => cf.ByControlType(filterType.Value))
                : window.FindAllDescendants();
            return all
                .Where(e => !string.IsNullOrWhiteSpace(e.Name) || !string.IsNullOrWhiteSpace(e.AutomationId))
                .Select(e => $"[{e.ControlType,-16}] Name='{e.Name}'  Id='{e.AutomationId}'")
                .ToArray();
        }

    }
}

