# ApexComputerUse — AI Agent Reference

ApexComputerUse exposes the Windows UI Automation tree over a plain HTTP REST API. You drive any desktop app or browser by finding windows/elements (by title, name, AutomationId, or stable numeric ID) and executing actions against them. Default base URL: `http://localhost:8080`.

## Authentication

Every route except `GET /health` requires the API key. Pick one:

```bash
curl -H "Authorization: Bearer <key>" http://localhost:8080/ping
curl -H "X-Api-Key: <key>"            http://localhost:8080/ping
curl "http://localhost:8080/ping?apiKey=<key>"
```

Missing/invalid key → HTTP 401. Loopback callers always have full access; non-loopback callers are gated by per-client permissions.

## Response format

All endpoints return:

```json
{ "success": true, "action": "...", "data": { ... }, "error": null }
```

HTTP 200 on success, 400 on error.

## Format negotiation

Endpoints adapt output by priority: URL extension → `?format=` → `Accept` header → default HTML.

```bash
curl http://localhost:8080/status.json
curl "http://localhost:8080/ping?format=text"
curl -H "Accept: application/json" http://localhost:8080/ping
```

Valid formats: `json`, `html`, `text`, `pdf`. **For programmatic use, always request JSON** (`.json` extension is the most reliable). HTML responses also embed the full result in `<script type="application/json" id="apex-result">` if you can only fetch HTML.

## GET vs POST

Every command endpoint accepts both `POST` (JSON body) and `GET` (query string). GET parameter names match JSON field names. Examples:

```bash
curl "http://localhost:8080/find.json?window=Notepad"
curl "http://localhost:8080/exec.json?action=gettext"
```

---

## System routes

| Method | Path | Notes |
|---|---|---|
| `GET` | `/health` | Unauthenticated liveness probe |
| `GET` | `/ping` | Authenticated health check |
| `GET` | `/metrics` | Per-route request counters |
| `GET` | `/sysinfo` | OS/machine/user/CPU/CLR |
| `GET` | `/env` | All environment variables |
| `GET` | `/ls?path=<dir>` | Directory listing (defaults to CWD) |
| `GET`/`POST` | `/run?cmd=<command>` | Run shell command via `cmd.exe /c`, 30s timeout. **Disabled by default** — needs `EnableShellRun=true` or `APEX_ENABLE_SHELL_RUN=true`. Response includes `cmd`, `stdout`, `stderr`, `exit_code`. |
| `GET`/`POST` | `/winrun?target=<path>&args=<args>` | Launch any executable, file, or URI via `ShellExecute` (always enabled). Use for GUI apps, Explorer, browsers, URIs. POST body: `{"target":"...","args":"..."}`. Fire-and-forget — no stdout capture. |
| `POST` | `/run-tests` | Trigger bundled integration test runner |
| `POST` | `/shutdown` | Stop the HTTP server |
| `GET` | `/help` | Full server command reference |

---

## Discovery

```bash
# List all open windows with stable IDs
curl http://localhost:8080/windows.json
# → [{"id":42,"title":"Notepad"}, ...]

# Current selected window/element state
curl http://localhost:8080/status.json

# Element tree of current window (after a /find call)
curl http://localhost:8080/elements.json
```

### `/elements` query parameters

| Param | Meaning |
|---|---|
| `onscreen=true` | Prune offscreen subtrees at scan time. **Use this by default** — typically reduces element count by ~80%. |
| `depth=N` | Limit tree depth. Truncated nodes show `childCount` and `descendantCount` instead of `children`. |
| `id=<numericId>` | Expand a specific subtree by element ID without re-scanning the whole window. Map state is preserved between calls — IDs stay stable. |
| `type=<ControlType>` | Filter to one ControlType (e.g. `Button`, `Edit`, `ComboBox`). |
| `match=<text>` | Text search across Name, AutomationId, and Value. Returns matching branches with ancestor path + `depth` levels of descendants. With `onscreen=true`, search still scans offscreen elements; matches are tagged `"isOffscreen": true` — use `exec action=scrollinto` to bring them into view. |
| `collapseChains=true` | Collapse identity-less single-child `Pane`/`Group`/`Custom` wrapper chains. Named containers and anything with an AutomationId are preserved. Element IDs are unaffected. |
| `includePath=true` | Add ancestor breadcrumb (`"path"`) to every node. |
| `properties=extra` | Include Value pattern + HelpText (omitted by default). Useful for web inputs whose Name is empty. |

### Recommended workflow for large pages

```bash
# 1. Shallow overview
curl "http://localhost:8080/elements.json?depth=2&onscreen=true"

# 2. Drill into a specific subtree by ID
curl "http://localhost:8080/elements.json?id=708379645&depth=2&onscreen=true"

# 3. Or text-search for what you need directly
curl "http://localhost:8080/elements.json?match=submit&onscreen=true&depth=1"
```

Element nodes include: `id`, `controlType`, `name`, `automationId`, `className`, `frameworkId`, `isEnabled`, `isOffscreen`, `boundingRectangle` (`x`, `y`, `width`, `height`), `children` (or `childCount`+`descendantCount` if truncated), and with `properties=extra`: `value`, `helpText`.

---

## Find

```bash
# By window title (partial/fuzzy match) + AutomationId
curl -X POST http://localhost:8080/find \
  -H "Content-Type: application/json" \
  -d '{"window":"Notepad","id":"15"}'

# By element name + ControlType filter
curl -X POST http://localhost:8080/find \
  -d '{"window":"Notepad","name":"Text Editor","type":"Edit"}'

# By numeric IDs (fastest — direct map lookup, no fuzzy logic)
curl -X POST http://localhost:8080/find \
  -d '{"window":42,"id":105}'
```

Every `/find` call auto-focuses the matched window. The response's `element` object contains `id`, `controlType`, `name`, `automationId`, `className`, `frameworkId`, `isEnabled`, `isOffscreen`, `boundingRectangle`, plus `value`/`helpText` when `properties=extra` is set. Low-confidence or ambiguous fuzzy searches return `success:false` with `error_data.candidates` instead of guessing; choose a candidate or use numeric IDs.

### Request fields (and aliases)

| Field | Aliases | Description |
|---|---|---|
| `window` | — | Window title (partial match) or numeric ID |
| `automationId` | `id` | AutomationId string or numeric element ID |
| `elementName` | `name` | Element Name property (fallback if `id` blank) |
| `searchType` | `type` | ControlType filter (`All` or specific type) |
| `action` | — | Action name (for `/execute`) |
| `value` | — | Value/input for the action |
| `prompt` | — | AI: question/instruction text |
| `model` | `modelPath` | AI: path to LLM `.gguf` |
| `proj` | `mmProjPath` | AI: path to multimodal projector `.gguf` |

---

## Execute

After `/find`, run actions against the selected element:

```bash
curl -X POST http://localhost:8080/execute \
  -H "Content-Type: application/json" \
  -d '{"action":"type","value":"Hello World"}'

curl -X POST http://localhost:8080/execute -d '{"action":"click"}'
curl -X POST http://localhost:8080/execute -d '{"action":"gettext"}'
```

`/exec` is an alias for `/execute`.

### Action reference

#### General
| Action | Aliases | Value | Description |
|---|---|---|---|
| `click` | — | — | Smart: Invoke → Toggle → SelectionItem → mouse fallback |
| `mouse-click` | `mouseclick` | — | Force mouse left-click |
| `middle-click` | `middleclick` | — | Middle-button click |
| `right-click` | `rightclick` | — | Right-click |
| `double-click` | `doubleclick` | — | Double-click |
| `invoke` | — | — | Invoke pattern directly |
| `click-at` | `clickat` | `x,y` | Click at offset from element top-left |
| `drag` | — | `x,y` | Drag to screen coordinates |
| `hover` | — | — | Move mouse over element |
| `highlight` | — | — | Orange highlight for 1s |
| `focus` | — | — | Set keyboard focus |
| `keys` | — | text | Send keystrokes; supports `{CTRL}`, `{ALT}`, `{SHIFT}`, `{F5}`, `Ctrl+A`, `Alt+F4`, etc. |
| `screenshot` | `capture` | — | Save element image to `Desktop\Apex_Captures` |
| `describe` | — | — | Full UIA property description (not AI vision) |
| `patterns` | — | — | List supported automation patterns |
| `bounds` | — | — | Bounding rectangle |
| `isenabled` | — | — | `True`/`False` |
| `isvisible` | — | — | `True`/`False` |
| `wait` | — | automationId | Wait for element to appear |
| `wait-page-load` | `waitpageload` | seconds (default 10) | Poll browser title until page finishes loading |

**Visual Studio run handoff:** F5/debug targets `name="Debug Target"` with `type="SplitButton"`. Ctrl+F5/no-debug targets `name="Start Without Debugging"` with `type="Button"`. After an `/elements` scan, prefer those numeric IDs to bypass fuzzy matching.

#### Text / Value
| Action | Aliases | Value | Description |
|---|---|---|---|
| `type` | `enter` | text | Smart: Value pattern → keyboard |
| `insert` | — | text | Type at caret |
| `gettext` | `text` | — | Smart read: Text → Value → LegacyIAccessible → Name |
| `getvalue` | `value` | — | Smart read: Value → Text → LegacyIAccessible → Name |
| `setvalue` | — | text | Smart set: Value → RangeValue → keyboard |
| `clearvalue` | — | — | Set value to empty |
| `appendvalue` | — | text | Append text |
| `getselectedtext` | — | — | Selected text via Text pattern |
| `selectall` | — | — | Ctrl+A |
| `copy` / `cut` / `paste` / `undo` | — | — | Standard clipboard ops |
| `clear` | — | — | Select all + delete |

#### Range / Slider
| Action | Value | Description |
|---|---|---|
| `setrange` | number | Set RangeValue |
| `getrange` | — | Read RangeValue |
| `rangeinfo` | — | min/max/smallChange/largeChange |

#### Toggle
| Action | Aliases | Description |
|---|---|---|
| `toggle` | — | Cycle CheckBox state |
| `toggle-on` | `toggleon` | Set to On |
| `toggle-off` | `toggleoff` | Set to Off |
| `gettoggle` | — | Read state (`On`/`Off`/`Indeterminate`) |

#### Expand / Collapse
| Action | Description |
|---|---|
| `expand` | ExpandCollapse pattern expand |
| `collapse` | ExpandCollapse pattern collapse |
| `expandstate` | Read current state |

#### Selection
| Action | Aliases | Value | Description |
|---|---|---|---|
| `select` | — | item text | ComboBox/ListBox by text |
| `select-item` | `selectitem` | — | Select via SelectionItem pattern |
| `addselect` | — | — | Add to multi-selection |
| `removeselect` | — | — | Remove from selection |
| `isselected` | — | — | `True`/`False` |
| `getselection` | — | — | Selected items in container |
| `select-index` | `selectindex` | n | Zero-based index |
| `getitems` | — | — | All items (newline-separated) |
| `getselecteditem` | — | — | Currently selected item text |

#### Window State
| Action | Description |
|---|---|
| `minimize` / `maximize` / `restore` | Standard window controls |
| `windowstate` | Read state (`Normal`/`Maximized`/`Minimized`) |

#### Transform
| Action | Value | Description |
|---|---|---|
| `move` | `x,y` | Transform pattern move |
| `resize` | `w,h` | Transform pattern resize |

#### Scroll
Mouse-scroll actions move the cursor to the element centre first, so scrolling lands in the right area.

| Action | Aliases | Value | Description |
|---|---|---|---|
| `scroll-up` | `scrollup` | n (default 3) | Scroll up n clicks |
| `scroll-down` | `scrolldown` | n (default 3) | Scroll down n clicks |
| `scroll-left` | `scrollleft` | n (default 3) | Horizontal scroll left |
| `scroll-right` | `scrollright` | n (default 3) | Horizontal scroll right |
| `scrollinto` | `scrollintoview` | — | Scroll element into view |
| `scrollpercent` | — | `h,v` | Scroll to %/% (0–100) |
| `getscrollinfo` | — | — | Position + scrollable flags |

#### Grid / Table
| Action | Value | Description |
|---|---|---|
| `griditem` | `row,col` | Element at grid cell |
| `gridinfo` | — | Row/column counts |
| `griditeminfo` | — | Row/column/span for GridItem |

---

## Capture

Returns base64 PNG in `data` field.

```bash
# Current element (default)
curl -X POST http://localhost:8080/capture

# Full screen
curl -X POST http://localhost:8080/capture -d '{"action":"screen"}'

# Current window
curl -X POST http://localhost:8080/capture -d '{"action":"window"}'

# Multiple elements stitched vertically
curl -X POST http://localhost:8080/capture -d '{"action":"elements","value":"42,105,106"}'
```

`elements` target requires comma-separated numeric IDs from a prior `/elements` scan.

Distinct from the `screenshot` exec action, which saves to disk and returns a file path.

---

## OCR

Uses Tesseract. Requires `tessdata\eng.traineddata` next to the executable.

```bash
# OCR the current element
curl -X POST http://localhost:8080/ocr

# OCR a region within the element (x,y,width,height)
curl -X POST http://localhost:8080/ocr -d '{"value":"0,0,300,50"}'
```

---

## AI Vision (multimodal)

Local LLamaSharp MTMD. Requires a vision GGUF + projector. Every call is stateless — no chat history.

```bash
# Status — is a model loaded?
curl http://localhost:8080/ai/status

# Init — load model + projector
curl -X POST http://localhost:8080/ai/init \
  -d '{"model":"C:\\models\\vision.gguf","proj":"C:\\models\\mmproj.gguf"}'

# Describe current selected element (captures it as an image first)
curl -X POST http://localhost:8080/ai/describe
curl -X POST http://localhost:8080/ai/describe -d '{"prompt":"List every button you can see."}'

# Ask a specific question about the current element
curl -X POST http://localhost:8080/ai/ask -d '{"prompt":"Is there an error visible?"}'

# Describe an image/audio file on disk
curl -X POST http://localhost:8080/ai/file \
  -d '{"value":"C:\\screen.png","prompt":"What dialog is shown?"}'
```

`describe`, `ask`, and `file` require a prior `/find` to select an element. `init` must be called once per server lifetime.

---

## UI Map

```bash
# Render current window's element tree as a colour-coded PNG (base64)
curl http://localhost:8080/uimap
```

Requires a prior `/find`. Same response format as `/capture`.

---

## Drawing

```bash
# Render shapes to base64 PNG
curl -X POST http://localhost:8080/draw -d '{
  "value": "{\"canvas\":\"blank\",\"width\":400,\"height\":300,\"shapes\":[
    {\"type\":\"circle\",\"x\":200,\"y\":150,\"r\":80,\"color\":\"royalblue\",\"fill\":true},
    {\"type\":\"text\",\"x\":200,\"y\":140,\"text\":\"Hello!\",\"color\":\"white\",\"font_size\":20,\"align\":\"center\"}
  ]}"
}'

# Built-in demo
curl http://localhost:8080/draw/demo

# Show as click-through screen overlay
curl "http://localhost:8080/draw/demo?overlay=true&ms=6000"
```

### Shape types
| Type | Key fields |
|---|---|
| `rect` | `x y w h corner_radius` |
| `ellipse` | `x y w h` |
| `circle` | `x y r` (x,y is centre) |
| `line` | `x y x2 y2` |
| `arrow` | `x y x2 y2` (head at x2,y2) |
| `polygon` | `points[]` (flat x,y array) |
| `triangle` | `x y w h` |
| `arc` | `x y w h start_angle sweep_angle` (degrees, clockwise from 3 o'clock) |
| `text` | `x y text font_size font_bold align background` |

Common fields on all shapes: `color`, `fill` (bool), `stroke_width`, `opacity` (0–1), `dashed` (bool), `rotation` (degrees, centre-origin).

Canvas values: `blank` (transparent), `white`, `black`, `screen` (live screenshot), `window` (current window), `element` (current element).

---

## Scenes (persistent layered drawings)

Stable shape IDs — generate a composition, read it back, refine it incrementally.

| Method | Route | Description |
|---|---|---|
| `GET` / `POST` | `/scenes` | List / create |
| `GET` / `PUT` / `PATCH` / `DELETE` | `/scenes/{id}` | Get / update meta / delete |
| `GET` | `/scenes/{id}/render` | Render → base64 PNG |
| `GET` / `POST` | `/scenes/{id}/layers` | List / add layer |
| `GET` / `PUT` / `PATCH` / `DELETE` | `/scenes/{id}/layers/{lid}` | Layer ops |
| `GET` / `POST` | `/scenes/{id}/layers/{lid}/shapes` | List / add shape |
| `GET` / `PUT` / `PATCH` / `DELETE` | `/scenes/{id}/layers/{lid}/shapes/{sid}` | Shape ops (PATCH = geometry only, never clobbers color/style) |
| `POST` | `/scenes/{id}/shapes/{sid}/move` | Move shape to a different layer |

```bash
# Create
curl -X POST http://localhost:8080/scenes -d '{"name":"My Scene","width":800,"height":600,"background":"#1a1a2e"}'

# Add layer + shape
curl -X POST http://localhost:8080/scenes/{id}/layers -d '{"name":"Background"}'
curl -X POST http://localhost:8080/scenes/{id}/layers/{lid}/shapes \
  -d '{"shape":{"type":"circle","x":400,"y":300,"r":80,"color":"royalblue","fill":true},"name":"Planet"}'

# Render
curl http://localhost:8080/scenes/{id}/render
```

---

## Workflow tips

1. **Start cheap.** `GET /windows.json` → pick window → `POST /find` with the numeric window ID.
2. **Scan small.** `GET /elements.json?onscreen=true&depth=2&collapseChains=true` first. Drill in by ID only when needed.
3. **Search instead of browse.** `?match=<text>` is usually faster than reading the whole tree.
4. **Use numeric IDs after the first scan.** They're SHA-256-stable across the session and skip all fuzzy matching.
5. **Treat fuzzy candidate failures as a stop sign.** If `/find` returns `error_data.candidates`, pick a candidate by ID instead of retrying the same broad name.
6. **Verify state, don't assume.** After an action, `gettext`/`getvalue`/`isselected`/`gettoggle` confirms it landed.
7. **Offscreen matches:** `match=` always searches offscreen too. If a match has `"isOffscreen": true`, run `exec action=scrollinto` against its ID before interacting.
8. **`.json` everywhere.** Saves you parsing HTML wrappers.

---

## Alternate transports

If HTTP is unavailable, the same command surface is reachable via:

- **Named pipe** (default name `ApexComputerUse`): JSON request/response, ACL-restricted to the current Windows user. PowerShell module `Scripts\ApexComputerUse.psm1` wraps it (`Connect-FlaUI`, `Find-FlaUIElement`, `Invoke-FlaUIAction`, etc.). Pipe sessions preserve window/element state across calls.
- **`Scripts\apex.cmd`**: cmd.exe wrapper around the HTTP server. Syntax: `apex find Notepad`, `apex exec click`, `apex exec type value=Hello`, `apex capture action=screen`.
- **Telegram bot**: `/find window=Notepad id=15`, `/exec action=click`, `/capture`, etc. Key=value with quoted values for spaces.
