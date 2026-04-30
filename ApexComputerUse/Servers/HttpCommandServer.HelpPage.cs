using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ApexComputerUse
{
    public partial class HttpCommandServer
    {
        // -- Help data ------------------------------------------------------

        private sealed record HelpAction(
            string   Action,
            [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string[]? Aliases,
            [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string?   Value,
            string   Description);

        private sealed record HelpGroup(string Name, string Id, HelpAction[] Actions);

        private sealed record HelpEndpoint(
            string  Method,
            string  Route,
            [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Params,
            string  Description);

        private static readonly HelpEndpoint[] _helpEndpoints =
        [
            new("GET",  "/windows",  null,
                "List open windows with numeric IDs"),
            new("GET",  "/status",   null,
                "Current window and element state"),
            new("GET",  "/elements", "[type=ControlType] [id=mapId] [depth=n] [onscreen=true] [match=text] [collapseChains=true] [includePath=true] [properties=extra] [since=hash]",
                "Element tree for current window; returns treeHash for polling"),
            new("POST", "/find",     "window=<title|id> [id=<automationId|mapId>] [name=<name>] [type=<ControlType>]",
                "Select a window and optionally an element - sets the current element pointer"),
            new("POST", "/exec",     "action=<action> [value=<input>]",
                "Execute an action on the current element"),
            new("POST", "/ocr",      "[value=x,y,w,h]",
                "OCR the current element or a screen region"),
            new("POST", "/capture",  "[action=screen|window|element|elements] [value=id1,id2,...]",
                "Screenshot; returns base64 PNG in data.result"),
            new("GET",  "/uimap",    null,
                "Annotated screenshot with colour-coded element overlays"),
            new("GET",  "/ping",     null,
                "Liveness check"),
            new("GET",  "/health",   null,
                "Server health, uptime, and request counters"),
            new("GET",  "/sysinfo",  null,
                "OS and machine info"),
            new("GET",  "/help",     "[group=<id>] [action=<name>]",
                "This reference. Add ?group= or ?action= to filter; append .json for machine-readable output"),
        ];

        private static readonly HelpGroup[] _helpGroups =
        [
            new("Click / Mouse", "click",
            [
                new("click",         null,                 null,           "Smart click: Invoke->Toggle->SelectionItem->mouse"),
                new("mouse-click",   null,                 null,           "Force mouse left click"),
                new("right-click",   null,                 null,           "Right mouse click"),
                new("double-click",  null,                 null,           "Double left click"),
                new("middle-click",  null,                 null,           "Middle mouse click"),
                new("click-at",      null,                 "x,y",         "Click at pixel offset from element top-left"),
                new("hover",         null,                 null,           "Move mouse to element centre"),
                new("drag",          null,                 "x,y",         "Drag element to screen coordinates"),
                new("highlight",     null,                 null,           "Draw orange highlight for 1 second"),
            ]),

            new("Keyboard", "keyboard",
            [
                new("type",          ["enter"],            "text",         "Type text into element"),
                new("insert",        null,                 "text",         "Insert text at caret position"),
                new("keys",          null,                 "keys",         "Send keystrokes: {CTRL}/{ALT}/{SHIFT}/{KEY}, Ctrl+A, Enter, Tab, ..."),
                new("selectall",     null,                 null,           "Select all text (Ctrl+A)"),
                new("copy",          null,                 null,           "Copy selection to clipboard"),
                new("cut",           null,                 null,           "Cut selection to clipboard"),
                new("paste",         null,                 null,           "Paste clipboard contents"),
                new("undo",          null,                 null,           "Undo last action"),
                new("clear",         null,                 null,           "Clear element text"),
            ]),

            new("Focus / State", "focus",
            [
                new("focus",         null,                 null,           "Set keyboard focus to element"),
                new("isenabled",     null,                 null,           "Returns true/false - is element enabled"),
                new("isvisible",     null,                 null,           "Returns true/false - is element visible"),
                new("describe",      null,                 null,           "Full UIA property dump"),
                new("patterns",      null,                 null,           "List supported UIA patterns"),
                new("bounds",        null,                 null,           "Bounding rectangle"),
            ]),

            new("Text / Value", "text",
            [
                new("gettext",       null,                 null,           "Smart read: Text pattern -> Value -> Name"),
                new("getvalue",      null,                 null,           "Smart read: Value -> Text -> LegacyIAccessible -> Name"),
                new("setvalue",      null,                 "text",         "Smart set: Value pattern -> RangeValue -> keyboard"),
                new("clearvalue",    null,                 null,           "Set value to empty string"),
                new("appendvalue",   null,                 "text",         "Append text to current value"),
                new("getselectedtext", null,               null,           "Selected text via Text pattern"),
            ]),

            new("Range / Slider", "range",
            [
                new("setrange",      null,                 "num",          "Set RangeValue pattern value"),
                new("getrange",      null,                 null,           "Get current range value"),
                new("rangeinfo",     null,                 null,           "Min, max, step, large-change"),
            ]),

            new("Toggle", "toggle",
            [
                new("toggle",        null,                 null,           "Toggle current state"),
                new("toggle-on",     null,                 null,           "Set toggle state to On"),
                new("toggle-off",    null,                 null,           "Set toggle state to Off"),
                new("gettoggle",     null,                 null,           "Get current state: On / Off / Indeterminate"),
            ]),

            new("Expand / Collapse", "expand",
            [
                new("expand",        null,                 null,           "Expand via ExpandCollapse pattern"),
                new("collapse",      null,                 null,           "Collapse via ExpandCollapse pattern"),
                new("expandstate",   null,                 null,           "Get current expand/collapse state"),
            ]),

            new("Selection", "selection",
            [
                new("select-item",   null,                 null,           "Select via SelectionItem pattern"),
                new("addselect",     null,                 null,           "Add to multi-selection"),
                new("removeselect",  null,                 null,           "Remove from selection"),
                new("isselected",    null,                 null,           "Check if currently selected"),
                new("getselection",  null,                 null,           "Get selected items from container"),
            ]),

            new("ComboBox / ListBox", "combo",
            [
                new("select",        null,                 "text",         "Select item by text (multi-strategy)"),
                new("select-index",  null,                 "n",            "Select by zero-based index"),
                new("getitems",      null,                 null,           "List all items"),
                new("getselecteditem", null,               null,           "Get currently selected item text"),
            ]),

            new("Window", "window",
            [
                new("minimize",      null,                 null,           "Minimize window"),
                new("maximize",      null,                 null,           "Maximize window"),
                new("restore",       null,                 null,           "Restore window to normal size"),
                new("windowstate",   null,                 null,           "Normal / Maximized / Minimized"),
            ]),

            new("Transform", "transform",
            [
                new("move",          null,                 "x,y",         "Move element via Transform pattern"),
                new("resize",        null,                 "w,h",         "Resize element via Transform pattern"),
            ]),

            new("Scroll", "scroll",
            [
                new("scroll-down",   null,                 "n[,visual]",  "Scroll down n steps (default 3); uses UIA Scroll pattern (invisible); add 'visual' to force mouse cursor"),
                new("scroll-up",     null,                 "n[,visual]",  "Scroll up n steps; UIA pattern by default, 'visual' forces mouse cursor"),
                new("scroll-left",   null,                 "n[,visual]",  "Horizontal scroll left"),
                new("scroll-right",  null,                 "n[,visual]",  "Horizontal scroll right"),
                new("scrollinto",    null,                 null,           "Scroll element into view via ScrollItem pattern"),
                new("scrollpercent", null,                 "h,v",         "Scroll to horizontal/vertical percent (0-100)"),
                new("getscrollinfo", null,                 null,           "Current scroll position, range, and scrollability flags"),
            ]),

            new("Grid / Table", "grid",
            [
                new("griditem",      null,                 "row,col",     "Get item description at grid row, col (zero-based)"),
                new("gridinfo",      null,                 null,           "Row and column counts"),
                new("griditeminfo",  null,                 null,           "Row, column, row-span, column-span for a grid item"),
            ]),

            new("Screenshot / OCR", "capture",
            [
                new("screenshot",    ["capture"],          null,           "Capture current element to PNG (base64 in data.result)"),
                new("screen",        null,                 null,           "Capture full screen to PNG"),
                new("wait-page-load",["waitpageload"],     "seconds",     "Poll window title until page finishes loading (default 10s); returns title on success"),
            ]),

            new("Wait", "wait",
            [
                new("wait",          null,                 "automationId", "Wait up to timeout for element with this AutomationId to appear"),
            ]),
        ];

        // -- /help JSON handler ---------------------------------------------

        private static ApexResult HandleHelp(HttpListenerRequest req)
        {
            string? groupFilter  = req.QueryString["group"];
            string? actionFilter = req.QueryString["action"];

            var opts = FormatAdapter.s_indentedCamel;

            object payload;
            string message;

            if (!string.IsNullOrWhiteSpace(actionFilter))
            {
                var af = actionFilter.Trim();
                var hit = _helpGroups
                    .SelectMany(g => g.Actions.Select(a => new { groupName = g.Name, groupId = g.Id, action = a }))
                    .FirstOrDefault(x =>
                        string.Equals(x.action.Action, af, StringComparison.OrdinalIgnoreCase) ||
                        (x.action.Aliases?.Any(al => string.Equals(al, af, StringComparison.OrdinalIgnoreCase)) ?? false));

                if (hit == null)
                {
                    return new ApexResult
                    {
                        Success = false,
                        Action  = "help",
                        Error   = $"Action '{af}' not found. Try /help.json for a full list."
                    };
                }

                payload = new { group = hit.groupName, groupId = hit.groupId, action = hit.action };
                message = $"Action reference: {hit.action.Action}";
            }
            else if (!string.IsNullOrWhiteSpace(groupFilter))
            {
                var gf = groupFilter.Trim();
                var group = _helpGroups.FirstOrDefault(g =>
                    string.Equals(g.Name, gf, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(g.Id,   gf, StringComparison.OrdinalIgnoreCase));

                if (group == null)
                {
                    return new ApexResult
                    {
                        Success = false,
                        Action  = "help",
                        Error   = $"Group '{gf}' not found. Available groups: {string.Join(", ", _helpGroups.Select(g => g.Id))}"
                    };
                }

                payload = group;
                message = $"Group reference: {group.Name} ({group.Actions.Length} actions)";
            }
            else
            {
                payload = new { endpoints = _helpEndpoints, groups = _helpGroups };
                message = $"{_helpEndpoints.Length} endpoints, {_helpGroups.Sum(g => g.Actions.Length)} actions in {_helpGroups.Length} groups";
            }

            return new ApexResult
            {
                Success = true,
                Action  = "help",
                Data    = new Dictionary<string, string>
                {
                    ["result"]  = JsonSerializer.Serialize(payload, opts),
                    ["message"] = message
                }
            };
        }

        // -- /help HTML page ------------------------------------------------

        private static async Task ServeHelpPage(HttpListenerResponse res)
        {
            var sb = new StringBuilder(65536);

            sb.Append("""
                <!DOCTYPE html>
                <html lang="en">
                <head>
                <meta charset="utf-8">
                <title>ApexComputerUse - API Reference</title>
                <style>
                  *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }
                  body { font-family: monospace; font-size: 13px; background: #1e1e1e; color: #d4d4d4;
                         display: flex; flex-direction: column; height: 100vh; overflow: hidden; }

                  /* -- Header -- */
                  header { background: #252526; border-bottom: 1px solid #3c3c3c;
                           padding: .35em 1em; display: flex; align-items: center; gap: .8em; flex-shrink: 0; }
                  .brand  { color: #9cdcfe; font-size: .82em; }
                  .htitle { color: #c8c8c8; flex: 1; font-size: .85em; }
                  #search { background: #3c3c3c; border: 1px solid #555; color: #d4d4d4;
                            padding: .22em .5em; font: inherit; border-radius: 3px; width: 180px; }
                  #search:focus { outline: none; border-color: #4ec94e; }
                  #searchCount { font-size: .75em; color: #666; min-width: 60px; }
                  .json-link { color: #9cdcfe; font-size: .78em; text-decoration: none;
                               padding: .18em .5em; border: 1px solid #3c3c3c; border-radius: 3px; }
                  .json-link:hover { background: #2d2d30; }

                  /* -- Layout -- */
                  .layout { display: grid; grid-template-columns: 160px 1fr; flex: 1; overflow: hidden; min-height: 0; }

                  /* -- Sidebar nav -- */
                  nav { background: #252526; border-right: 1px solid #3c3c3c; overflow-y: auto; padding: .4em 0; }
                  .nav-item { display: block; padding: .28em .8em; color: #c8c8c8; text-decoration: none;
                              font-size: .78em; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
                  .nav-item:hover { background: #2a2d2e; color: #fff; }
                  .nav-item.active { color: #4ec94e; background: #1e2a1e; }
                  .nav-sep { height: 1px; background: #3c3c3c; margin: .3em 0; }

                  /* -- Main content -- */
                  main { overflow-y: auto; padding: 1em 1.5em 3em; scroll-behavior: smooth; }
                  section { margin-bottom: 2.2em; }
                  section.hidden { display: none; }
                  h2 { color: #4ec94e; font-size: .8em; text-transform: uppercase; letter-spacing: .07em;
                       margin-bottom: .5em; padding-bottom: .25em; border-bottom: 1px solid #3c3c3c;
                       display: flex; align-items: baseline; gap: .5em; }
                  h2 .count { color: #555; font-size: .85em; font-weight: normal; letter-spacing: 0; text-transform: none; }

                  /* -- Tables -- */
                  table { width: 100%; border-collapse: collapse; font-size: .82em; }
                  th    { text-align: left; color: #666; font-weight: normal; font-size: .85em;
                          padding: .15em .6em .3em; border-bottom: 1px solid #3c3c3c; }
                  td    { padding: .2em .6em; vertical-align: top; border-bottom: 1px solid #252526; }
                  tr:hover td { background: #252526; }
                  tr.hidden { display: none; }

                  td.method     { font-weight: bold; white-space: nowrap; padding-right: .4em; }
                  td.method.get  { color: #4ec94e; }
                  td.method.post { color: #9cdcfe; }
                  td.route      { color: #dcdcaa; white-space: nowrap; }
                  td.params     { color: #ce9178; font-size: .88em; }
                  td.action     { color: #9cdcfe; white-space: nowrap; }
                  td.aliases    { color: #666; white-space: nowrap; font-size: .9em; }
                  td.value      { color: #ce9178; white-space: nowrap; }
                  td.desc       { color: #d4d4d4; }

                  /* -- Highlight on search match -- */
                  mark { background: #2d4a1e; color: inherit; border-radius: 2px; }
                </style>
                </head>
                <body>
                <header>
                  <span class="brand">ApexComputerUse</span>
                  <span class="htitle">API Reference</span>
                  <input id="search" type="text" placeholder="filter..." autocomplete="off" spellcheck="false">
                  <span id="searchCount"></span>
                  <a id="jsonLink" href="/help.json" class="json-link" title="Machine-readable JSON">JSON ?</a>
                </header>
                <div class="layout">
                <nav id="nav">
                  <a href="#endpoints" class="nav-item">Endpoints</a>
                  <div class="nav-sep"></div>
                """);

            foreach (var g in _helpGroups)
                sb.Append($"  <a href=\"#{HtmlEncode(g.Id)}\" class=\"nav-item\">{HtmlEncode(g.Name)}</a>\n");

            sb.Append("""
                </nav>
                <main id="main">
                """);

            // -- Endpoints section --
            sb.Append("""
                <section id="endpoints">
                <h2>Endpoints <span class="count">HTTP routes</span></h2>
                <table>
                <thead><tr><th>Method</th><th>Route</th><th>Params / body</th><th>Description</th></tr></thead>
                <tbody>
                """);

            foreach (var ep in _helpEndpoints)
            {
                sb.Append($"""
                    <tr>
                      <td class="method {HtmlEncode(ep.Method.ToLowerInvariant())}">{HtmlEncode(ep.Method)}</td>
                      <td class="route">{HtmlEncode(ep.Route)}</td>
                      <td class="params">{HtmlEncode(ep.Params ?? "")}</td>
                      <td class="desc">{HtmlEncode(ep.Description)}</td>
                    </tr>
                    """);
            }

            sb.Append("</tbody></table></section>\n");

            // -- Action groups --
            foreach (var g in _helpGroups)
            {
                sb.Append($"""
                    <section id="{HtmlEncode(g.Id)}">
                    <h2>{HtmlEncode(g.Name)} <span class="count">{g.Actions.Length} actions</span></h2>
                    <table>
                    <thead><tr><th>Action</th><th>Aliases</th><th>Value</th><th>Description</th></tr></thead>
                    <tbody>
                    """);

                foreach (var a in g.Actions)
                {
                    string aliases = a.Aliases != null ? string.Join(", ", a.Aliases) : "";
                    sb.Append($"""
                        <tr data-action="{HtmlEncode(a.Action)}">
                          <td class="action">{HtmlEncode(a.Action)}</td>
                          <td class="aliases">{HtmlEncode(aliases)}</td>
                          <td class="value">{HtmlEncode(a.Value ?? "")}</td>
                          <td class="desc">{HtmlEncode(a.Description)}</td>
                        </tr>
                        """);
                }

                sb.Append("</tbody></table></section>\n");
            }

            sb.Append("""
                </main>
                </div>
                <script>
                (function () {
                  const search   = document.getElementById('search');
                  const countEl  = document.getElementById('searchCount');
                  const jsonLink = document.getElementById('jsonLink');
                  const sections = Array.from(document.querySelectorAll('section'));
                  const navItems = Array.from(document.querySelectorAll('#nav .nav-item'));

                  // -- Active nav on scroll --
                  const observer = new IntersectionObserver(entries => {
                    entries.forEach(e => {
                      if (e.isIntersecting) {
                        navItems.forEach(n => n.classList.remove('active'));
                        const a = document.querySelector(`#nav a[href="#${e.target.id}"]`);
                        if (a) a.classList.add('active');
                      }
                    });
                  }, { root: document.getElementById('main'), threshold: 0.1 });
                  sections.forEach(s => observer.observe(s));

                  // -- Filter --
                  search.addEventListener('input', function () {
                    const q = this.value.trim().toLowerCase();
                    jsonLink.href = q ? '/help.json?action=' + encodeURIComponent(q) : '/help.json';

                    let totalVisible = 0;

                    sections.forEach(sec => {
                      if (sec.id === 'endpoints') { sec.classList.remove('hidden'); return; }

                      const rows = Array.from(sec.querySelectorAll('tbody tr'));
                      let secVisible = 0;

                      rows.forEach(row => {
                        if (!q) { row.classList.remove('hidden'); secVisible++; return; }
                        const txt = row.textContent.toLowerCase();
                        if (txt.includes(q)) { row.classList.remove('hidden'); secVisible++; }
                        else                 { row.classList.add('hidden'); }
                      });

                      sec.classList.toggle('hidden', q !== '' && secVisible === 0);
                      totalVisible += secVisible;
                    });

                    countEl.textContent = q ? totalVisible + ' match' + (totalVisible === 1 ? '' : 'es') : '';
                  });

                  // Focus search on '/'
                  document.addEventListener('keydown', e => {
                    if (e.key === '/' && document.activeElement !== search) {
                      e.preventDefault(); search.focus(); search.select();
                    }
                    if (e.key === 'Escape') { search.value = ''; search.dispatchEvent(new Event('input')); search.blur(); }
                  });
                })();
                </script>
                </body></html>
                """);

            byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());
            res.ContentType     = "text/html; charset=utf-8";
            res.ContentLength64 = bytes.Length;
            try   { await res.OutputStream.WriteAsync(bytes); }
            finally { res.Close(); }
        }

        private static string HtmlEncode(string s) =>
            s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
    }
}

