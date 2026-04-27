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
    public partial class ApexHelper
    {
        // ── Window ────────────────────────────────────────────────────────

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

        // ── Window fuzzy find ─────────────────────────────────────────────

        public Window? FindWindowFuzzy(string title, out string matchedTitle, out bool isExact)
        {
            var all = _automation.GetDesktop().FindAllChildren()
                          .Where(w => !string.IsNullOrWhiteSpace(w.Name))
                          .ToArray();

            // 1. Exact (case-insensitive)
            var exact = all.FirstOrDefault(w => w.Name.Equals(title, StringComparison.OrdinalIgnoreCase));
            if (exact != null) { matchedTitle = exact.Name; isExact = true; return exact.AsWindow(); }

            // 2. Contains — if multiple match, prefer StartsWith over mid-string, then shortest name.
            var contains = all.Where(w => w.Name.Contains(title, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (contains.Length >= 1)
            {
                var best = contains
                    .OrderByDescending(w => w.Name.StartsWith(title, StringComparison.OrdinalIgnoreCase))
                    .ThenBy(w => w.Name.Length)
                    .First();
                matchedTitle = best.Name; isExact = false;
                return best.AsWindow();
            }

            // 3. Closest Levenshtein (with distance threshold)
            var closest = all.MinBy(w => Levenshtein(title.ToLower(), w.Name.ToLower()));
            if (closest == null) { matchedTitle = ""; isExact = false; return null; }
            int bestDist = Levenshtein(title.ToLower(), closest.Name.ToLower());
            if (bestDist > (int)(title.Length * 0.6)) { matchedTitle = ""; isExact = false; return null; }
            matchedTitle = closest.Name;
            isExact = false;
            return closest.AsWindow();
        }

        // ── Element fuzzy find ────────────────────────────────────────────

        public AutomationElement? FindElementFuzzy(
            Window window, string searchText, ControlType? filterType, bool searchById,
            out string matchedValue, out bool isExact)
        {
            AutomationElement[] all;
            try
            {
                var fetchTask = filterType.HasValue
                    ? Task.Run(() => window.FindAllDescendants(cf => cf.ByControlType(filterType.Value)))
                    : Task.Run(() => window.FindAllDescendants());
                if (!fetchTask.Wait(5000) || fetchTask.Result == null)
                    { matchedValue = ""; isExact = false; return null; }
                all = fetchTask.Result;
            }
            catch { matchedValue = ""; isExact = false; return null; }

            if (all.Length == 0) { matchedValue = ""; isExact = false; return null; }

            Func<AutomationElement, string> getValue = searchById
                ? el => el.Properties.AutomationId.ValueOrDefault ?? ""
                : el => el.Properties.Name.ValueOrDefault ?? "";

            var exactEl = all.FirstOrDefault(el =>
                getValue(el).Equals(searchText, StringComparison.OrdinalIgnoreCase));
            if (exactEl != null) { matchedValue = getValue(exactEl); isExact = true; return exactEl; }

            var cont = all.Where(el =>
                getValue(el).Contains(searchText, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrEmpty(getValue(el))).ToArray();
            if (cont.Length == 1) { matchedValue = getValue(cont[0]); isExact = false; return cont[0]; }

            var cands = all.Where(el => !string.IsNullOrEmpty(getValue(el))).ToArray();
            if (cands.Length == 0) { matchedValue = ""; isExact = false; return null; }

            var closest2 = cands.MinBy(el => Levenshtein(searchText.ToLower(), getValue(el).ToLower()))!;
            int bestDist2 = Levenshtein(searchText.ToLower(), getValue(closest2).ToLower());
            if (bestDist2 > (int)(searchText.Length * 0.6))
                { matchedValue = ""; isExact = false; return null; }
            matchedValue = getValue(closest2);
            isExact = false;
            return closest2;
        }

        // ── Levenshtein ───────────────────────────────────────────────────

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

        // ── Element lookup ────────────────────────────────────────────────

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

        // ── Discovery ─────────────────────────────────────────────────────

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
