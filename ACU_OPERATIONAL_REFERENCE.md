# ApexComputerUse — Operational Reference

> **Auto-start:** the HTTP server starts automatically when the app launches (`HttpAutoStart = true`). By default it binds to localhost only (`HttpBindAll = false`). On first launch a UAC prompt configures the URL ACL and firewall rule once.

## Authentication

API key: `<key>` (resolves from `appsettings.json` → `APEX_API_KEY` env var → `%APPDATA%\ApexComputerUse\settings.json`; auto-generated on first run if absent).

Every request requires the API key from the **Remote Control** tab. Three equivalent methods:

```bash
curl -H "X-Api-Key: <key>" http://localhost:8080/ping
curl -H "Authorization: Bearer <key>" http://localhost:8080/ping
curl "http://localhost:8080/ping?apiKey=<key>"
```

Missing or invalid key returns **HTTP 401**. The interactive console at `GET /` pre-fills the key automatically.

---

## Core Loop

```
ping → windows → find → exec → find → exec → ...
```

**Critical state rule:** every action always targets the **last found element**. If context may have changed, re-run `/find` before `/exec`.

```bash
# 1. Confirm server is up
curl -H "X-Api-Key: <key>" http://localhost:8080/ping

# 2. List open windows (get IDs and titles)
curl -H "X-Api-Key: <key>" http://localhost:8080/windows

# 3. Find a window and element
curl -H "X-Api-Key: <key>" -X POST http://localhost:8080/find \
  -H "Content-Type: application/json" \
  -d '{"window":"Notepad","name":"Text Editor","type":"Edit"}'

# 4. Execute an action on the found element
curl -H "X-Api-Key: <key>" -X POST http://localhost:8080/exec \
  -H "Content-Type: application/json" \
  -d '{"action":"type","value":"Hello World"}'

# 5. Current selection state
curl -H "X-Api-Key: <key>" http://localhost:8080/status
```

---

## Windows & Elements

```bash
# List all open windows with stable IDs
curl -H "X-Api-Key: <key>" http://localhost:8080/windows

# All elements in the last found window (nested JSON with IDs + bounding boxes)
curl -H "X-Api-Key: <key>" http://localhost:8080/elements

# Onscreen elements only — prunes offscreen subtrees (~80% fewer elements on browser pages)
curl -H "X-Api-Key: <key>" "http://localhost:8080/elements?onscreen=true"

# Filter by ControlType
curl -H "X-Api-Key: <key>" "http://localhost:8080/elements?onscreen=true&type=Button"

# UI map — returns colour-coded PNG of element tree (base64)
curl -H "X-Api-Key: <key>" http://localhost:8080/uimap
```

Element JSON shape:
```json
{
  "id": 105,
  "controlType": "Edit",
  "name": "Text Editor",
  "automationId": "15",
  "boundingRectangle": { "x": 0, "y": 30, "width": 800, "height": 600 },
  "children": [...]
}
```

---

## Find (`/find`)

Locates a window and optionally an element. Sets the current context for all subsequent `/exec` calls. Also auto-focuses the matched window.

```bash
# By window title only (fuzzy match)
curl -H "X-Api-Key: <key>" -X POST http://localhost:8080/find \
  -d '{"window":"Notepad"}'

# By window + element name + ControlType filter
curl -H "X-Api-Key: <key>" -X POST http://localhost:8080/find \
  -H "Content-Type: application/json" \
  -d '{"window":"Calculator","name":"Equals","type":"Button"}'

# By window + AutomationId
curl -H "X-Api-Key: <key>" -X POST http://localhost:8080/find \
  -H "Content-Type: application/json" \
  -d '{"window":"Calculator","id":"equalButton"}'

# By numeric IDs (fastest — direct map lookup, no fuzzy search)
curl -H "X-Api-Key: <key>" -X POST http://localhost:8080/find \
  -H "Content-Type: application/json" \
  -d '{"window":1466489411,"id":105}'

# Via GET (no body required)
curl -H "X-Api-Key: <key>" "http://localhost:8080/find?window=Notepad&name=Text+Editor&type=Edit"
```

**Find fields:**

| Field | Aliases | Description |
|---|---|---|
| `window` | — | Window title (partial/fuzzy) or numeric ID from `/windows` |
| `id` | `automationId` | Element AutomationId string or numeric ID from `/elements` |
| `name` | `elementName` | Element Name property (used if `id` not given) |
| `type` | `searchType` | ControlType filter — `Button`, `Edit`, `Text`, `CheckBox`, etc. |

**Tips:**
- Prefer numeric IDs when available — faster, unambiguous, no fuzzy matching
- Window title/name matching is conservative: exact and strong matches succeed, but low-confidence or ambiguous fuzzy matches return `success:false` with `error_data.candidates` instead of guessing
- `/find` with only a `window` field selects the window itself as the current element

---

## Exec (`/exec`)

Runs an action on the last found element.

```bash
curl -H "X-Api-Key: <key>" -X POST http://localhost:8080/exec \
  -H "Content-Type: application/json" \
  -d '{"action":"click"}'

# Via GET shorthand
curl -H "X-Api-Key: <key>" "http://localhost:8080/exec?action=gettext"
```

---

## All Actions

### Click / Mouse

| Action | Value | Description |
|---|---|---|
| `click` | — | Smart click: Invoke → Toggle → SelectionItem → mouse fallback |
| `mouse-click` | — | Force mouse left-click |
| `middle-click` | — | Middle mouse button click |
| `right-click` | — | Right-click |
| `double-click` | — | Double-click |
| `click-at` | `x,y` | Click at pixel offset from element top-left |
| `drag` | `x,y` | Drag element to screen coordinates |
| `hover` | — | Move mouse over element |
| `invoke` | — | Invoke pattern directly |

### Keyboard

| Action | Value | Description |
|---|---|---|
| `keys` | text | Send keystrokes. Supports `{ENTER}`, `{ESC}`, `{TAB}`, `{F5}`, `{CTRL}`, `{ALT}`, `{SHIFT}`, `Ctrl+A`, `Alt+F4`, `Ctrl+{ENTER}`, etc. |
| `focus` | — | Set keyboard focus on the element |

**Visual Studio run handoff:** F5/debug targets `name="Debug Target"` with `type="SplitButton"`. Ctrl+F5/no-debug targets `name="Start Without Debugging"` with `type="Button"`. Prefer numeric IDs from `/elements` once discovered.

**`keys` examples:**
```bash
# Press Enter
-d '{"action":"keys","value":"{ENTER}"}'

# Ctrl+Enter (e.g. send Gmail)
-d '{"action":"keys","value":"Ctrl+{ENTER}"}'

# Type keystrokes (e.g. calculator input)
-d '{"action":"keys","value":"5.26+9.62="}' 

# Select all and copy
-d '{"action":"keys","value":"Ctrl+A"}'
```

### Text / Value

| Action | Aliases | Value | Description |
|---|---|---|---|
| `type` | `enter` | text | Smart type: Value pattern → keyboard |
| `insert` | — | text | Type at current caret position |
| `gettext` | `text` | — | Smart read: Text pattern → Value → Name |
| `getvalue` | `value` | — | Read via Value pattern |
| `setvalue` | — | text | Smart set: Value pattern → RangeValue → keyboard |
| `clearvalue` | — | — | Set value to empty string |
| `appendvalue` | — | text | Append text to current value |
| `clear` | — | — | Select all and delete |
| `selectall` | — | — | Ctrl+A |
| `copy` | — | — | Ctrl+C |
| `cut` | — | — | Ctrl+X |
| `paste` | — | — | Ctrl+V |
| `undo` | — | — | Ctrl+Z |
| `getselectedtext` | — | — | Get currently selected text |

### State / Info

| Action | Value | Description |
|---|---|---|
| `highlight` | — | Draw orange highlight around element for 1 second |
| `describe` | — | Return full UIA property description |
| `patterns` | — | List automation patterns supported by element |
| `bounds` | — | Return bounding rectangle |
| `isenabled` | — | Returns `True` or `False` |
| `isvisible` | — | Returns `True` or `False` |
| `wait` | automationId | Wait for element with given AutomationId to appear |
| `screenshot` | — | Save element image to `Desktop\Apex_Captures` |

### Toggle / CheckBox

| Action | Description |
|---|---|
| `toggle` | Cycle toggle state |
| `toggle-on` | Set to On |
| `toggle-off` | Set to Off |
| `gettoggle` | Read state: `On` / `Off` / `Indeterminate` |

### Expand / Collapse

| Action | Description |
|---|---|
| `expand` | Expand via ExpandCollapse pattern |
| `collapse` | Collapse via ExpandCollapse pattern |
| `expandstate` | Read current state |

### Selection (ComboBox / ListBox)

| Action | Value | Description |
|---|---|---|
| `select` | item text | Select item by text |
| `select-index` | n | Select item by zero-based index |
| `select-item` | — | Select current element via SelectionItem pattern |
| `addselect` | — | Add to multi-selection |
| `removeselect` | — | Remove from selection |
| `isselected` | — | Returns `True` or `False` |
| `getselection` | — | Get selected items |
| `getitems` | — | List all items (newline-separated) |
| `getselecteditem` | — | Get currently selected item text |

### Scroll

| Action | Value | Description |
|---|---|---|
| `scroll-up` | n (optional) | Scroll up n clicks (default 3) |
| `scroll-down` | n (optional) | Scroll down n clicks (default 3) |
| `scroll-left` | n (optional) | Horizontal scroll left (default 3) |
| `scroll-right` | n (optional) | Horizontal scroll right (default 3) |
| `scrollinto` | — | Scroll element into view |
| `scrollpercent` | `h,v` | Scroll to h%/v% position (0–100) |
| `getscrollinfo` | — | Scroll position and scrollable flags |

### Window State

| Action | Description |
|---|---|
| `minimize` | Minimize window |
| `maximize` | Maximize window |
| `restore` | Restore to normal |
| `windowstate` | Read state: `Normal` / `Maximized` / `Minimized` |
| `move` | Move window — value: `x,y` |
| `resize` | Resize window — value: `w,h` |

### Grid / Table

| Action | Value | Description |
|---|---|---|
| `griditem` | `row,col` | Get element at cell |
| `gridinfo` | — | Row and column counts |
| `griditeminfo` | — | Row / column / span for a GridItem element |

### Range / Slider

| Action | Value | Description |
|---|---|---|
| `setrange` | number | Set RangeValue |
| `getrange` | — | Read current RangeValue |
| `rangeinfo` | — | Min / max / smallChange / largeChange |

---

## Capture (`/capture`)

Returns a base64-encoded PNG in the `data.result` field.

```bash
# Current element (requires prior find)
curl -H "X-Api-Key: <key>" -X POST http://localhost:8080/capture

# Full screen
curl -H "X-Api-Key: <key>" -X POST http://localhost:8080/capture \
  -H "Content-Type: application/json" \
  -d '{"action":"screen"}'

# Current window
curl -H "X-Api-Key: <key>" -X POST http://localhost:8080/capture \
  -H "Content-Type: application/json" \
  -d '{"action":"window"}'

# Multiple elements stitched vertically (comma-separated numeric IDs)
curl -H "X-Api-Key: <key>" -X POST http://localhost:8080/capture \
  -H "Content-Type: application/json" \
  -d '{"action":"elements","value":"42,105,106"}'
```

---

## OCR (`/ocr`)

Requires `tessdata\eng.traineddata` next to the executable. Download from [github.com/tesseract-ocr/tessdata](https://github.com/tesseract-ocr/tessdata).

```bash
# OCR the current element
curl -H "X-Api-Key: <key>" -X POST http://localhost:8080/ocr

# OCR a region (x,y,width,height) within the element
curl -H "X-Api-Key: <key>" -X POST http://localhost:8080/ocr \
  -H "Content-Type: application/json" \
  -d '{"value":"0,0,300,50"}'
```

---

## AI Vision (`/ai/*`)

Local multimodal LLM (LFM2.5-VL). No cloud API required. Use **Download All** on the Model tab to fetch models automatically. Every inference call is stateless — no chat history retained.

```bash
# Check model status
curl -H "X-Api-Key: <key>" http://localhost:8080/ai/status

# Load model (run once per session)
curl -H "X-Api-Key: <key>" -X POST http://localhost:8080/ai/init \
  -H "Content-Type: application/json" \
  -d '{"model":"C:\\models\\LFM2.5-VL.gguf","proj":"C:\\models\\mmproj.gguf"}'

# Describe current element (captures it as image, sends to model)
curl -H "X-Api-Key: <key>" -X POST http://localhost:8080/ai/describe

# Describe with a custom prompt
curl -H "X-Api-Key: <key>" -X POST http://localhost:8080/ai/describe \
  -H "Content-Type: application/json" \
  -d '{"prompt":"List every button you can see."}'

# Ask a specific question about the current element
curl -H "X-Api-Key: <key>" -X POST http://localhost:8080/ai/ask \
  -H "Content-Type: application/json" \
  -d '{"prompt":"What number is shown on the display?"}'

# Describe an image file on disk
curl -H "X-Api-Key: <key>" -X POST http://localhost:8080/ai/file \
  -H "Content-Type: application/json" \
  -d '{"value":"C:\\screenshots\\app.png","prompt":"What dialog is shown?"}'
```

> `describe`, `ask`, and `file` require a prior `/find` to select a window/element. The model must be initialized with `init` before any inference.

---

## AI Chat (`/chat/*`)

Streaming chat over HTTP — same 8 providers as the desktop AI Chat window (OpenAI, Anthropic, DeepSeek, Grok, Groq, Duck, LM Studio, LlamaSharp). Configure keys in `ai-settings.json` next to the executable.

```bash
# Browser chat UI
curl -H "X-Api-Key: <key>" http://localhost:8080/chat

# Provider + session status
curl -H "X-Api-Key: <key>" http://localhost:8080/chat/status

# Send a message (response streams back)
curl -H "X-Api-Key: <key>" -X POST http://localhost:8080/chat/send \
  -H "Content-Type: application/json" \
  -d '{"message":"Hello"}'

# Clear the conversation
curl -H "X-Api-Key: <key>" -X POST http://localhost:8080/chat/reset
```

---

## AI Drawing (`/draw`)

Renders GDI+ shapes to a base64 PNG on demand.

```bash
# Draw shapes — value is a JSON string
curl -H "X-Api-Key: <key>" -X POST http://localhost:8080/draw \
  -H "Content-Type: application/json" \
  -d '{
    "value": "{\"canvas\":\"blank\",\"width\":400,\"height\":300,\"shapes\":[
      {\"type\":\"circle\",\"x\":200,\"y\":150,\"r\":80,\"color\":\"royalblue\",\"fill\":true},
      {\"type\":\"text\",\"x\":200,\"y\":140,\"text\":\"Hello!\",\"color\":\"white\",\"font_size\":20,\"font_bold\":true,\"align\":\"center\"}
    ]}"
  }'

# Built-in demo scene
curl -H "X-Api-Key: <key>" http://localhost:8080/draw/demo

# Show as full-screen overlay for N milliseconds
curl -H "X-Api-Key: <key>" "http://localhost:8080/draw/demo?overlay=true&ms=6000"
```

Result is in `data.result` as base64 PNG.

### Shape types

| Type | Key fields | Notes |
|---|---|---|
| `rect` | `x y w h corner_radius` | Rounded if `corner_radius > 0` |
| `ellipse` | `x y w h` | Bounding-box anchored |
| `circle` | `x y r` | x,y is centre |
| `line` | `x y x2 y2` | Straight line |
| `arrow` | `x y x2 y2` | Arrowhead at (x2,y2) |
| `polygon` | `points[]` | Flat array of x,y pairs |
| `triangle` | `x y w h` | Top-centre apex |
| `arc` | `x y w h start_angle sweep_angle` | Degrees, clockwise from 3 o'clock |
| `text` | `x y text font_size font_bold align background` | |

**Common fields on every shape:** `color`, `fill` (bool), `stroke_width`, `opacity` (0–1), `dashed` (bool), `rotation` (degrees).

**Canvas values:** `blank` (transparent), `white`, `black`, `screen` (live screenshot), `window` (current window), `element` (current element).

---

## Scene Editor (`/scenes/*`)

Persistent layered drawing canvas. Every shape has a stable ID. AI can generate and the user can drag-to-refine — coordinates are always accurate and readable back via API.

Browser editor: `GET /editor`  
Desktop editor: **Tools → Scene Editor**

```bash
# Create a scene
curl -H "X-Api-Key: <key>" -X POST http://localhost:8080/scenes \
  -H "Content-Type: application/json" \
  -d '{"name":"My Scene","width":800,"height":600,"background":"#1a1a2e"}'

# List scenes
curl -H "X-Api-Key: <key>" http://localhost:8080/scenes

# Get a scene
curl -H "X-Api-Key: <key>" http://localhost:8080/scenes/{id}

# Add a layer
curl -H "X-Api-Key: <key>" -X POST http://localhost:8080/scenes/{id}/layers \
  -H "Content-Type: application/json" \
  -d '{"name":"Background"}'

# Add a shape to a layer
curl -H "X-Api-Key: <key>" -X POST http://localhost:8080/scenes/{id}/layers/{lid}/shapes \
  -H "Content-Type: application/json" \
  -d '{"shape":{"type":"circle","x":400,"y":300,"r":80,"color":"royalblue","fill":true},"name":"Planet"}'

# Render scene → base64 PNG
curl -H "X-Api-Key: <key>" http://localhost:8080/scenes/{id}/render

# Patch shape geometry (after user drags — never clobbers color/style)
curl -H "X-Api-Key: <key>" -X PATCH http://localhost:8080/scenes/{id}/layers/{lid}/shapes/{sid} \
  -H "Content-Type: application/json" \
  -d '{"x":420,"y":310}'

# Move shape to different layer
curl -H "X-Api-Key: <key>" -X POST http://localhost:8080/scenes/{id}/shapes/{sid}/move \
  -H "Content-Type: application/json" \
  -d '{"target_layer_id":"{newLayerId}"}'

# Delete shape / layer / scene
curl -H "X-Api-Key: <key>" -X DELETE http://localhost:8080/scenes/{id}/layers/{lid}/shapes/{sid}
curl -H "X-Api-Key: <key>" -X DELETE http://localhost:8080/scenes/{id}/layers/{lid}
curl -H "X-Api-Key: <key>" -X DELETE http://localhost:8080/scenes/{id}
```

### Scene route reference

| Method | Route | Description |
|---|---|---|
| `GET` / `POST` | `/scenes` | List / create scene |
| `GET` / `PUT` / `DELETE` | `/scenes/{id}` | Get / update / delete scene |
| `GET` | `/scenes/{id}/render` | Render → base64 PNG |
| `GET` / `POST` | `/scenes/{id}/layers` | List / add layer |
| `GET` / `PUT` / `DELETE` | `/scenes/{id}/layers/{lid}` | Get / update / delete layer |
| `GET` / `POST` | `/scenes/{id}/layers/{lid}/shapes` | List / add shape |
| `GET` / `PUT` / `PATCH` / `DELETE` | `/scenes/{id}/layers/{lid}/shapes/{sid}` | Get / replace / patch geometry / delete |
| `POST` | `/scenes/{id}/shapes/{sid}/move` | Move shape to different layer |

Scenes are persisted to `<exe>/scenes/{id}.json`.

---

## System Routes

```bash
curl http://localhost:8080/health                            # unauthenticated liveness (no API key required)
curl -H "X-Api-Key: <key>" http://localhost:8080/ping        # authenticated health check
curl -H "X-Api-Key: <key>" http://localhost:8080/metrics     # per-route request counters
curl -H "X-Api-Key: <key>" http://localhost:8080/sysinfo     # OS, machine, user, CPU, CLR
curl -H "X-Api-Key: <key>" http://localhost:8080/env         # all environment variables
curl -H "X-Api-Key: <key>" http://localhost:8080/ls          # directory listing (cwd)
curl -H "X-Api-Key: <key>" "http://localhost:8080/ls?path=C:\\Users"
curl -H "X-Api-Key: <key>" http://localhost:8080/help        # full endpoint reference

# Trigger the integration test runner (TestApplications/TestRunner)
curl -H "X-Api-Key: <key>" -X POST http://localhost:8080/run-tests

# Graceful server shutdown
curl -H "X-Api-Key: <key>" -X POST http://localhost:8080/shutdown

# Shell execution — disabled by default
# Enable: set EnableShellRun=true in appsettings.json or APEX_ENABLE_SHELL_RUN=true
# Primary query parameter
curl -H "X-Api-Key: <key>" "http://localhost:8080/run?command=whoami"
# Backward-compatible alias
curl -H "X-Api-Key: <key>" "http://localhost:8080/run?cmd=whoami"
# JSON body form
curl -H "X-Api-Key: <key>" -X POST http://localhost:8080/run \
     -H "Content-Type: application/json" \
     -d '{"command":"whoami"}'
```

`/health` is the only route that does not require the API key — safe to use from external monitoring.

---

## Response Format

Every endpoint returns the same structure:

```json
{
  "success": true,
  "action": "find",
  "data": { ... },
  "error": null
}
```

HTTP **200** on success, **400** on error.

### Format options

Append to any URL or use `?format=`:

```bash
curl http://localhost:8080/status.json     # JSON only
curl http://localhost:8080/status.txt      # plain text
curl http://localhost:8080/status.html     # HTML with embedded JSON (default)
curl http://localhost:8080/status.pdf      # PDF document
curl "http://localhost:8080/ping?format=json"
```

HTML responses embed `<script type="application/json" id="apex-result">` — any agent that can fetch a webpage gets structured JSON without a vision model.

---

## Configuration (`appsettings.json`)

```json
{
  "HttpPort":       8080,
  "HttpBindAll":    false,
  "PipeName":       "ApexComputerUse",
  "LogLevel":       "Information",
  "EnableShellRun": false,
  "ApiKey":         "",
  "ModelPath":      "",
  "MmProjPath":     ""
}
```

**Environment variable overrides** (prefix `APEX_`):

| Variable | Default | Description |
|---|---|---|
| `APEX_HTTP_PORT` | `8080` | HTTP listen port |
| `APEX_HTTP_BIND_ALL` | `false` | `true` to bind all interfaces (network access) |
| `APEX_API_KEY` | auto-generated | Override the API key |
| `APEX_ENABLE_SHELL_RUN` | `false` | Enable `/run` endpoint |
| `APEX_LOG_LEVEL` | `Information` | `Debug` / `Information` / `Warning` / `Error` |
| `APEX_MODEL_PATH` | — | Path to vision `.gguf` file |
| `APEX_MMPROJ_PATH` | — | Path to multimodal projector `.gguf` file |

Logs: `%LOCALAPPDATA%\ApexComputerUse\Logs\apex-YYYYMMDD.log` (daily rotation, 7-day retention).

---

## Key Rules

1. **Actions target the last found element** — always `/find` before `/exec` if context changed
2. **Prefer numeric IDs over names** — faster, no fuzzy matching, no ambiguity
3. **Use `?onscreen=true`** — dramatically reduces element count on browser pages
4. **`keys` for navigation** — use `{ENTER}`, `{TAB}`, `Ctrl+{ENTER}` etc. to navigate UI where no button exists
5. **`setvalue` + `keys {ENTER}`** — the correct pattern for navigating a browser address bar
6. **`type` vs `setvalue`** — `type` simulates keyboard; `setvalue` uses the Value automation pattern (faster for inputs, required for address bars)
7. **Window scope** — always pass `"window"` in `/find` to scope the search; avoids finding elements in wrong windows
8. **Re-check window title after navigation** — browser window titles change; use `/windows` to get the updated title/ID
