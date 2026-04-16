<img width="1536" height="1024" alt="robot_07_59_17 PM" src="https://github.com/user-attachments/assets/05324e78-7e5c-4137-a936-437df7d1ab1c" />


# ApexComputerUse

> **Give AI agents control of any Windows app — no vision model, no screenshots, no cloud.**

![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet) ![Platform](https://img.shields.io/badge/platform-Windows-0078D4?logo=windows) ![License](https://img.shields.io/badge/license-MIT-green)

ApexComputerUse reads the **Windows accessibility tree** (the same data the OS exposes to screen readers) and serves it over a plain **HTTP REST API**. Any AI agent — in any language, on any machine — can find, inspect, and control any desktop app or browser by making simple HTTP requests. No screenshots. No pixel coordinates. No cloud dependency. In recent tests, excluding 3D games, accuracy averages **99%** on web and desktop tasks.

**5–20 tokens per action** instead of 1,000–3,500 for a screenshot. A full browser page in onscreen-only mode is ~126 elements of compact JSON — less than the cost of a single screenshot of the same page.

Works on Win32, WPF, UWP, WinForms, and browsers. Controlled via **HTTP REST**, **named pipes**, **cmd.exe**, and **Telegram**.

---

## Screenshots

### Main Desktop UI

<img width="584" height="423" alt="main_ui" src="https://github.com/user-attachments/assets/57ac0e09-2a9f-4c54-92ca-b9f2c2cee958" />

---

### Interactive Web Console (`GET /`)
 
<img width="1366" height="730" alt="web_console" src="https://github.com/user-attachments/assets/e5918c23-6e97-4638-ab21-43108878b8d2" />

---

### Scene Editor — WinForms
 
<img width="1366" height="729" alt="scene_editor" src="https://github.com/user-attachments/assets/4a833d5c-fe9b-410e-b9f7-365130a4f5e6" />

---

### Scene Editor — Browser (`GET /editor`)
 
<img width="1366" height="730" alt="scene_editor_web" src="https://github.com/user-attachments/assets/c61e376b-f767-476f-8655-129d6b862aba" />

---

### AI-Generated Drawing!
 
<img width="797" height="647" alt="space_scene" src="https://github.com/user-attachments/assets/9656a01d-d02c-4ce5-af78-17178458903c" />

---

### UI Map Overlay

<img width="287" height="343" alt="image" src="https://github.com/user-attachments/assets/50c5b80b-b6b3-45a8-8dce-8194d34888dc" />

---

## Quickstart

**Requirements:** Windows 10/11 · [.NET 10 SDK](https://dotnet.microsoft.com/download)

```bash
git clone https://github.com/your-org/ApexComputerUse
cd ApexComputerUse
dotnet build
dotnet run --project ApexComputerUse
```

1. The app opens. In the **Remote Control** tab, click **Start HTTP**.
2. An API key is generated automatically and shown in the **API Key** field — copy it.
3. Open `http://localhost:8081/` in a browser — the interactive console appears (the browser console pre-fills the key).
4. Pick any open window from the **Windows** panel on the left.
5. Browse its element tree, click an action button, see the result.

Or go straight to curl (replace `<key>` with the API key from the Remote Control tab):

```bash
# Confirm the server is up
curl -H "X-Api-Key: <key>" http://localhost:8081/ping

# Find Notepad and read its text editor content
curl -H "X-Api-Key: <key>" -X POST http://localhost:8081/find \
     -H "Content-Type: application/json" -d '{"window":"Notepad"}'
curl -H "X-Api-Key: <key>" http://localhost:8081/exec?action=gettext
```

> **OCR:** requires `eng.traineddata` — download from [github.com/tesseract-ocr/tessdata](https://github.com/tesseract-ocr/tessdata) and place it in `tessdata\` next to the executable.
>
> **AI Vision:** requires a GGUF vision model and projector — see [Usage — AI](#usage--ai-multimodal).

---

## Why ApexComputerUse

### The problem with screenshot-based automation

Most AI computer-use tools — Claude Computer Use, OpenAI CUA, UI-TARS, OmniParser — work by sending a screenshot to a vision model and guessing pixel coordinates to click. This approach has compounding costs:

- Screenshot token costs scale with resolution and vary by provider. A 1024×768 image runs ~765 tokens (OpenAI) to ~1,050 tokens (Anthropic). At 1920×1080 that rises to ~1,840 tokens (Anthropic) or ~2,125 tokens (OpenAI). At 2048×2048, OpenAI charges ~2,765 tokens and Anthropic ~2,500–3,500 tokens. Gemini is the exception, typically staying under 1,000 tokens even for ~4K images. And this cost is paid on every single step.
- Screenshots stack in conversation history — a 20-step task accumulates 20+ images in context.
- Coordinate grounding is fragile: it breaks on window resize, DPI scaling, and multi-monitor setups.
- Published benchmarks confirm the accuracy ceiling: even specialist 7B vision models score only **18.9%** on real professional UIs (ScreenSpot-Pro, 2025). GPT-4o scores **below 2%** on unscaled professional screens.

### The structured-tree approach

ApexComputerUse reads the **accessibility tree** the OS already maintains — the same tree used by screen readers and test automation. This gives every element a name, control type, and AutomationId, without rendering a pixel.

Interacting with an element by name costs **5–20 tokens**. The element map for a full browser page in onscreen-only mode is typically **100–200 elements** of compact JSON — compared to ~1,050 tokens for a single screenshot of the same page, with none of the coordinate fragility.

This is the same direction taken by the most efficient browser-only tools: [browser-use](https://github.com/browser-use/browser-use) claims 50% fewer tokens than screenshot alternatives; Vercel's agent-browser returns 200–400 tokens per page snapshot and uses 82–93% fewer tokens than Playwright MCP. ApexComputerUse brings the same approach to the entire Windows desktop.

### How it compares

| Tool | Coverage | HTTP API | Stable element IDs | Onscreen filter | Status |
|---|---|---|---|---|---|
| **ApexComputerUse** | Windows desktop + browsers | ✅ REST | ✅ SHA-256 hash | ✅ `?onscreen=true` | Active |
| UFO2 (Microsoft) | Windows desktop + browsers | ❌ research agent | ❌ bounding-box | Partial | Research only |
| UI Automata | Windows desktop + browsers | MCP only | Selector-based | Shadow DOM cache | Active |
| Windows-Use | Windows desktop | ❌ Python lib | ❌ | Partial | Active |
| WinAppDriver | Windows desktop | WebDriver | XPath / selectors | ❌ | Paused by Microsoft |
| browser-use | Browser only | ❌ Python lib | Element hash | ✅ | Active |
| Playwright MCP | Browser only | MCP | Session-scoped refs | Partial | Active |
| Claude Computer Use | Any (screenshot) | Cloud API | ❌ coordinates | ❌ | Active |

**No other tool combines:** Windows UIA3 coverage, SHA-256 stable element IDs, a language-agnostic HTTP REST API, and an onscreen visibility filter — in a single deployable binary.







## Compatible AI Agents

ApexComputerUse exposes a plain HTTP REST API, which means any AI agent that can execute shell commands or fetch a URL can use it. No SDK, no plugin, no special integration required — if the agent can run `curl`, it can drive any Windows app or browser through this server.

### Access paths

There are three ways an agent can interact with ApexComputerUse:

**1. Shell / terminal access (curl or any HTTP client)**
Any agent that can run shell commands can call the API directly with `curl`, Python `requests`, or PowerShell `Invoke-RestMethod`. This covers the widest range of tools and requires no configuration beyond starting the HTTP server.

**2. URL fetch / WebFetch tool**
Some agents have a dedicated tool for fetching URLs rather than running shell commands. ApexComputerUse's HTML responses embed a full `<script type="application/json" id="apex-result">` block, so any agent that can fetch a webpage gets structured JSON data back without needing a vision model.

**3. MCP server (optional wrapper)**
Several agents support the Model Context Protocol. If you prefer a tighter integration, the REST API can be wrapped as an MCP server so the agent sees your actions as named tools rather than raw HTTP calls.

---

### Agent compatibility table

| Agent | Type | Shell access | URL fetch | MCP | Notes |
|---|---|---|---|---|---|
| **Claude Code** | CLI | ✅ Bash tool | ✅ WebFetch tool | ✅ | `curl` is blocked by default but Claude Code automatically falls back to Python `requests` for the same result |
| **Cline** | VS Code extension | ✅ Terminal | ✅ Via shell | ✅ | Full agentic loop; browser control; human-in-the-loop approval for each command |
| **Aider** | CLI | ✅ Shell | ✅ Via shell | ❌ | Oldest and most widely deployed open-source coding CLI; works with any model via Ollama or API key |
| **Goose** (Block) | CLI + Desktop | ✅ Shell | ✅ Via shell | ✅ | Apache 2.0; model-agnostic; native MCP support |
| **Cursor** (Agent Mode) | IDE | ✅ Terminal | ✅ Via shell | ✅ | Agent mode can run terminal commands; MCP support available |
| **Windsurf** (Cascade) | IDE | ✅ Terminal | ✅ Via shell | ✅ | Cascade runs commands automatically; MCP support with admin controls |
| **GitHub Copilot** (Agent Mode) | VS Code extension | ✅ Terminal | ✅ Via shell | ✅ | VS Code Agent mode handles terminal commands and iteration |
| **OpenHands / Devin** | Cloud agent | ✅ Shell | ✅ Via shell | Varies | Requires network path from the cloud sandbox to your Windows machine |
| **Roo Code / Continue** | VS Code extension | ✅ Terminal | ✅ Via shell | ✅ | Open-source; BYOK; shell access via VS Code terminal integration |
| **Autocomplete-only tools** | Extension | ❌ | ❌ | ❌ | Tabnine, Supermaven, etc. generate code only — no agentic shell or HTTP access |

> **Local model users:** any agent backed by a local model via Ollama (Qwen Coder, DeepSeek Coder, CodeLlama, etc.) that also has shell access works the same way. The model itself doesn't need internet access — the agent runtime executes the curl commands.

---

### Quickest agent integration (Claude Code example)

Start the HTTP server, then drop this into your Claude Code session:

```
The ApexComputerUse REST API is running at http://localhost:8081.
Use curl (or Python requests if curl is blocked) to control Windows apps.
Start with: curl http://localhost:8081/ping
Then: curl http://localhost:8081/windows  (to see what's open)
Then find and interact with any element using /find and /exec.
```

Claude Code will handle the rest — finding windows, reading the element tree, clicking, typing, and verifying results across turns using its stable element IDs.

---
 

### Stable element IDs

Every element is assigned a **SHA-256 hash-based numeric ID** derived from its control type, name, AutomationId, and position in the tree. These IDs are stable across sessions — an agent can reference the same element in turn 1 and turn 20 without re-querying the tree. No other tool in the Windows desktop automation space publishes this property.

### The onscreen filter

`GET /elements?onscreen=true` prunes any element where `IsOffscreen = true` during the tree scan, skipping entire offscreen subtrees. On a live Chewy.com product page this reduces **634 elements to 126** — an 80% reduction — putting token cost per step in the same range as the best browser-only tools while covering all desktop apps too.

The filter composes with the type filter and the new depth/expansion params: `?onscreen=true&type=Button`.

### Progressive tree expansion

For deep pages, fetch a shallow overview first, then drill into only the branches you care about:

```bash
# Step 1 — shallow overview (fast, small response)
curl "http://localhost:8081/elements?depth=2&onscreen=true"
# Nodes that have children beyond the depth limit show "childCount": N instead of "children"

# Step 2 — expand a specific node by its ID (IDs are stable between calls)
curl "http://localhost:8081/elements?id=708379645&depth=2&onscreen=true"
# Returns only that subtree, 2 levels deep — existing map entries are preserved
```

This lets an AI agent navigate to the relevant section of a large page without fetching the whole tree on every step.

---

## Features

- Find any window and element by name or AutomationId (exact or fuzzy match)
- Filter element search by ControlType
- Persistent, hash-based stable element and window IDs (survive app restarts)
- Onscreen-only element map (`?onscreen=true`) — prunes offscreen subtrees at scan time
- Progressive tree expansion (`?depth=N` + `?id=<elementId>`) — fetch a shallow overview then drill into only the branches you need, without re-scanning the whole window
- Element nodes include `boundingRectangle` (x, y, width, height) for spatial context and visual rendering
- Execute all common UI actions: click, type, select, toggle, scroll, drag & drop, etc.
- OCR any UI element using Tesseract
- Multimodal AI: describe UI elements, ask questions about them, analyse image/audio files using a local vision LLM (LLamaSharp MTMD)
- Remote control via HTTP REST API (curl-friendly JSON)
- Remote control via named pipe (PowerShell module included)
- Remote control via cmd.exe batch helper (`apex.cmd`)
- Remote control via Telegram bot
- Screenshot capture of elements, windows, and full screen (returned as base64 PNG)
- **Interactive HTTP test console** — served at `GET /`, includes live windows list, element tree browser, grouped command builder covering every action, inline capture/OCR/AI vision/UI map buttons, format selector (JSON/HTML/Text/PDF), format demo links, and a response log
- **AI Drawing** — `POST /draw` renders any combination of shapes (rect, ellipse, circle, line, arrow, polygon, text) to a base64 PNG; `GET /draw/demo` renders a built-in multi-colour space scene; `?overlay=true` shows the result as a click-through screen overlay
- **Layered Scene Editor** — persistent, structured drawing canvas with stable shape IDs so AI can generate a composition and the user can refine it; full REST API at `/scenes/*`; interactive WinForms editor (Tools → Scene Editor) and browser editor (`GET /editor`)
- **UI Map Renderer** — renders the element tree as a colour-coded overlay drawn directly on screen, and optionally exports a PNG image; accessible via Tools → Render UI Map or `GET /uimap`
- **Format-adaptive responses** — every endpoint serves HTML, plain text, JSON, or **PDF** via URL extension (`.json`, `.html`, `.txt`, `.pdf`), `?format=` parameter, or `Accept` header; default is an HTML page with embedded JSON readable by any AI that can fetch a URL
- **System utility routes** — `/ping`, `/sysinfo`, `/env`, `/ls`, `/run` for AI agents that need OS-level context without a separate tool
- **Auto-download setup** — Model tab "Download All" button fetches the LFM2.5-VL model, projector, and Tesseract data to fixed local paths on first launch

---

## Setup

### 1. Build and run

```bash
git clone https://github.com/your-org/ApexComputerUse
cd ApexComputerUse
dotnet run --project ApexComputerUse
```

### 2. Models and OCR data (optional — auto-download available)

Open the **Model** tab and click **Download All** to automatically fetch:

- `LFM2.5-VL-450M-Q4_0.gguf` — vision LLM (450 M parameters, quantized)
- `mmproj-LFM2.5-VL-450m-F16.gguf` — multimodal projector
- `eng.traineddata` — Tesseract English OCR data

Files are saved to `models\` and `tessdata\` next to the executable. On first launch the app detects missing files and switches to the Model tab automatically.

To download manually: copy `eng.traineddata` from [github.com/tesseract-ocr/tessdata](https://github.com/tesseract-ocr/tessdata) into `tessdata\`, and place both `.gguf` files in `models\`.

### 3. Telegram Bot (optional)

1. Message [@BotFather](https://t.me/BotFather) on Telegram and create a bot with `/newbot`.
2. Copy the token (format: `123456789:ABC-DEF...`).
3. Paste it into the **Bot Token** field in the app and click **Start Telegram**.
4. Add your Telegram chat ID to the **Allowed Chat IDs** field to restrict who can send commands.

---

## Security & Configuration

### HTTP API Authentication

Every HTTP request must include the API key. Three equivalent methods:

```bash
# Authorization header (recommended)
curl -H "Authorization: Bearer <key>" http://localhost:8081/ping

# X-Api-Key header
curl -H "X-Api-Key: <key>" http://localhost:8081/ping

# Query parameter (use only for browser links / quick tests)
curl "http://localhost:8081/ping?apiKey=<key>"
```

Requests without a valid key receive **HTTP 401**. The interactive web console (`GET /`) pre-fills the key automatically — paste it from the Remote Control tab on first launch.

To disable authentication (local development only), clear the API Key field in the app.

### Named Pipe Security

The named pipe is ACL-restricted to the current Windows user. Other local users and unprivileged processes cannot connect.

### Telegram Bot Authorization

Enter one or more Telegram chat IDs in the **Allowed Chat IDs** field (comma-separated). Any message from an unlisted chat ID receives "Unauthorized." and is logged. Leave the field empty only for local testing.

### Shell Execution (`/run`)

The `POST /run` and `GET /run` endpoints execute arbitrary `cmd.exe` commands. They are **disabled by default**. Enable them explicitly:

- In `appsettings.json`: `"EnableShellRun": true`
- Or via environment variable: `APEX_ENABLE_SHELL_RUN=true`

### Configuration

All settings can be layered via three sources (highest priority last wins for env vars):

**`appsettings.json`** (next to the executable):

```json
{
  "HttpPort":       8081,
  "HttpBindAll":    false,
  "PipeName":       "ApexComputerUse",
  "LogLevel":       "Information",
  "EnableShellRun": false,
  "ApiKey":         "",
  "AllowedChatIds": "",
  "TelegramToken":  "",
  "ModelPath":      "",
  "MmProjPath":     ""
}
```

**Environment variables** (prefix `APEX_`, override `appsettings.json`):

| Variable | Description |
|---|---|
| `APEX_HTTP_PORT` | HTTP listen port (default `8081`) |
| `APEX_HTTP_BIND_ALL` | `true` to bind all interfaces instead of localhost only |
| `APEX_PIPE_NAME` | Named pipe name |
| `APEX_LOG_LEVEL` | Serilog minimum level: `Debug` / `Information` / `Warning` / `Error` |
| `APEX_ENABLE_SHELL_RUN` | `true` to enable the `/run` shell-execution endpoint |
| `APEX_API_KEY` | Override the auto-generated API key |
| `APEX_ALLOWED_CHAT_IDS` | Comma-separated Telegram chat ID whitelist |
| `APEX_TELEGRAM_TOKEN` | Telegram bot token |
| `APEX_MODEL_PATH` | Default LLM `.gguf` path |
| `APEX_MMPROJ_PATH` | Default multimodal projector `.gguf` path |

**Network binding:** `HttpBindAll = false` (the default) binds to `http://localhost:{port}/` — loopback only, safe for single-machine use. Set `APEX_HTTP_BIND_ALL=true` to bind all interfaces for network-wide access (ensure firewall rules are in place).

**Logs** are written to `<exe>/logs/apex-YYYYMMDD.log` (daily rotation, 7-day retention).

### Run as a Windows Service

ApexComputerUse can run headlessly as a Windows service (no GUI):

```powershell
# Install
sc.exe create ApexComputerUse binPath="C:\ApexComputerUse\ApexComputerUse.exe --service" start=auto
sc.exe start ApexComputerUse

# Uninstall
sc.exe stop ApexComputerUse
sc.exe delete ApexComputerUse
```

Configure via `appsettings.json` or `APEX_*` environment variables before starting the service. The `APEX_TELEGRAM_TOKEN` and `APEX_API_KEY` variables are the recommended way to inject secrets in a service context.

---

## Usage — UI

| Field | Description |
|---|---|
| **Window Name** | Partial title of the target window. Fuzzy-matched if no exact match found. |
| **AutomationId** | The element's `AutomationId` (checked first). |
| **Element Name** | The element's `Name` property (fallback if AutomationId is blank). |
| **Search Type** | Filter the element search to a specific `ControlType`. `All` searches everything. |
| **Control Type** | Selects the action group (Button, TextBox, etc.). |
| **Action** | The action to perform on the found element. |
| **Value / Index** | Input for actions that need it (text to type, index, row,col, x,y, etc.). |

**Find Element** — locates the window and element, logs what was found.
**Execute Action** — runs the selected action against the last found element.

### Tools menu

| Item | Description |
|---|---|
| **Run AI Computer Use Mode** | Launches the interactive multimodal AI agent loop (requires model loaded on the Model tab). |
| **Output UI Map** | Scans the current window's element tree and logs it as nested JSON to the console tab. |
| **Render UI Map** | Scans the current window's element tree, draws a colour-coded bounding-box overlay on screen for 5 seconds, and offers to save the overlay as a PNG image. |
| **Scene Editor** | Opens the layered scene editor — create scenes, add shapes to layers, drag to reposition, use AI to generate and refine compositions. |
| **AI Chat** | Opens a streaming chat window with support for 8 AI providers (OpenAI, Anthropic, DeepSeek, Grok, Groq, Duck, LM Studio, LlamaSharp). Configure API keys in `ai-settings.json` next to the executable. |

---

## Window and Element ID Mapping

Every window and element is assigned a **stable numeric ID** (SHA-256 hash-based) that persists across sessions. These IDs can be used in `find` commands instead of titles or AutomationIds.

```bash
# 1. Get windows with their IDs
curl http://localhost:8081/windows
# Returns: [{"id":42,"title":"Notepad"},{"id":107,"title":"Calculator"},...]

# 2. Get elements with their IDs for the current window
curl http://localhost:8081/elements

# Onscreen elements only (prunes offscreen subtrees — 80% fewer elements on browser pages)
curl "http://localhost:8081/elements?onscreen=true"

# Limit tree depth — nodes at the cutoff show "childCount" instead of "children"
curl "http://localhost:8081/elements?depth=2&onscreen=true"

# Expand a specific subtree by numeric ID (IDs are stable; map is preserved between expansion calls)
curl "http://localhost:8081/elements?id=708379645&depth=2&onscreen=true"

# Combine with type filter
curl "http://localhost:8081/elements?onscreen=true&type=Button"

# Returns nested JSON including bounding rectangles:
# {
#   "id": 105,
#   "controlType": "Edit",
#   "name": "Text Editor",
#   "automationId": "15",
#   "boundingRectangle": { "x": 0, "y": 30, "width": 800, "height": 600 },
#   "children": [...]
# }
#
# When a depth limit truncates a node's children, "childCount" appears instead:
# {
#   "id": 708379645,
#   "controlType": "Pane",
#   "name": "",
#   "boundingRectangle": { ... },
#   "childCount": 7    <-- call /elements?id=708379645 to expand
# }

# 3. Find using numeric IDs (no fuzzy matching, direct map lookup)
curl -X POST http://localhost:8081/find \
     -H "Content-Type: application/json" \
     -d '{"window":42,"id":105}'
```

Using numeric IDs is faster and unambiguous — the element is resolved directly from the in-memory map without any search or fuzzy logic. Every `find` call also auto-focuses the matched window.

---

## Token Economics

Map rendering isn't just a debugging convenience — it has compounding implications for token consumption at scale.

### The Core Difference

With screenshot-based AI automation, every interaction requires sending a fresh image to the model. At typical desktop resolutions that's **1,000–3,500 tokens per screenshot** depending on the provider and resolution — every single step, accumulating in conversation history. With ApexComputerUse's map approach, the UI is rendered once as a structured, text-based representation. After that initial render, each individual interaction references elements by name, costing **5–20 tokens on average**.

The `?onscreen=true` filter further reduces the element map to only what is visible in the current viewport. On a real browser page this produces **126 elements** of compact JSON — well under the cost of a single screenshot of the same page.

### Real-world token costs (approximate — varies by provider and resolution)

| | Per step | 20-step task |
|---|---|---|
| Screenshot (1024×768) | ~765–1,050 tokens | ~15,000–21,000 tokens in images alone |
| Screenshot (1920×1080) | ~1,840–2,125 tokens | ~37,000–43,000 tokens in images alone |
| Screenshot (2048×2048) | ~2,765–3,500 tokens | ~55,000–70,000 tokens in images alone |
| ApexComputerUse (full map) | 400–1,800 tokens (one-time) + ~10 per action | ~1,000 tokens total |
| ApexComputerUse (`?onscreen=true`) | 200–600 tokens (one-time) + ~10 per action | ~400 tokens total |

> **Provider breakdown:** at 1024×768, Anthropic ≈ 1,050 tokens / OpenAI ≈ 765 tokens. At 1920×1080, Anthropic ≈ 1,840 / OpenAI ≈ 2,125. At 2048×2048, OpenAI ≈ 2,765 / Anthropic ≈ 2,500–3,500. Gemini is notably more efficient — typically under 1,000 tokens even for ~4K images. All providers compound costs across steps: every screenshot remains in context for the life of the conversation.

---

### Example 1 — Small App *(Calculator, tray utility, simple tool)*

> Screenshot: **2,500 tokens each** · Initial map: **400 tokens** · Per-action after map: **8 tokens**

**By time period — 1 person:**

| Timeframe | Screen Capture | Map Approach | Tokens Saved |
|---|---|---|---|
| 1 day | 250,000 | 1,192 | 248,808 |
| 1 week | 1,750,000 | 8,344 | 1,741,656 |
| 1 year | 91,250,000 | 435,080 | 90,814,920 |

**Annual totals — by team size:**

| Team Size | Screen Capture | Map Approach | Reduction Factor |
|---|---|---|---|
| 1 person | 91,250,000 | 435,080 | **~210x** |
| 10 people | 912,500,000 | 4,350,800 | **~210x** |
| 50 people | 4,562,500,000 | 21,754,000 | **~210x** |

---

## Usage — HTTP API

Start the HTTP server from the **Remote Control** group box, then use curl or open `http://localhost:8081/` in a browser to access the interactive test console.

### Interactive Test Console (`GET /`)

Opening the root URL in any browser launches a dark-themed console with:

- **Windows panel** — live list of all open windows; click to select and auto-load its element tree
- **Elements panel** — nested element tree flattened with indentation; onscreen-only toggle; ControlType filter; click any element to select it
- **Command builder** — grouped action buttons covering every action: Click, Text, Keys, State, Scroll, Toggle, Select, Window, Range/Slider, Grid/Table, Transform, Wait, Capture, AI Vision; Value input (multiline, Ctrl+Enter to execute) with context-sensitive hints; ▶ Execute button
- **AI Vision buttons** — `status`, `describe`, `ask`, `file`; requires model loaded on the Model tab
- **Format selector** — dropdown in the header (JSON / HTML / Text / PDF); all requests use the selected format; format demo links (help, status, windows) open directly in a new tab in the chosen format
- **Scene Editor link** — opens the browser-based canvas editor in a new tab
- **Response log** — newest result at top; captures rendered as inline images (click to zoom); PDF responses shown as an "Open PDF" link (browser-native rendering)

### Format negotiation

Every endpoint adapts its response to whatever format the caller can consume, selected by priority:

1. **URL file extension** — append `.json`, `.html`, `.txt`, or `.pdf` to any path
2. **`?format=` query parameter** — `html`, `text`, `json`, or `pdf`
3. **`Accept` request header** — `text/html`, `text/plain`, `application/json`, or `application/pdf`
4. **Default:** `html`

```bash
# URL extension (highest priority — works even if the AI cannot set headers or query params)
curl http://localhost:8081/status.json
curl http://localhost:8081/help.txt
curl http://localhost:8081/windows.html
curl http://localhost:8081/status.pdf --output status.pdf

# ?format= query parameter
curl "http://localhost:8081/ping?format=text"
curl "http://localhost:8081/ping?format=json"

# Accept header
curl -H "Accept: application/json"  http://localhost:8081/ping
curl -H "Accept: application/pdf"   http://localhost:8081/help --output help.pdf

# HTML response (default — works in any browser or AI that can fetch a page)
curl http://localhost:8081/ping
```

**HTML** includes a `<pre>` block for human readability and an embedded `<script type="application/json" id="apex-result">` block containing the full result as JSON — allowing any AI that can fetch a webpage to extract structured data without a vision model.

**PDF** is a valid A4 document using the built-in Courier font (no external dependencies). Useful for AI systems that can only accept PDF attachments.

### GET access to command endpoints

All command endpoints accept both `POST` (JSON body) and `GET` (query string parameters), so any command can be expressed as a plain URL — no request body required:

```bash
# Find a window via GET
curl "http://localhost:8081/find?window=Notepad"

# Execute an action via GET
curl "http://localhost:8081/exec?action=gettext"

# Combine with URL extension for full URL-only access
curl "http://localhost:8081/find.json?window=Notepad&id=15"
curl "http://localhost:8081/exec.pdf?action=describe" --output result.pdf
```

**GET parameter names** match the JSON body field names: `window`, `id` / `automationId`, `name` / `elementName`, `type` / `searchType`, `action`, `value`, `onscreen`, `depth`, `prompt`, `model`, `proj`.

> **`/elements`-specific:** `depth=N` limits tree depth (truncated nodes show `childCount`); `id=<numericId>` expands from a previously-mapped element without clearing the rest of the map.

### Response format

All endpoints return the same canonical structure:

```json
{
  "success": true,
  "action": "ping",
  "data": { "key": "value", ... },
  "error": null
}
```

HTTP status: **200** on success, **400** on error.

---

### System / utility routes

```bash
# Health check
curl http://localhost:8081/ping

# System information (OS, machine, user, CPU, CLR)
curl http://localhost:8081/sysinfo

# All environment variables
curl http://localhost:8081/env

# Directory listing (defaults to current working directory)
curl http://localhost:8081/ls
curl "http://localhost:8081/ls?path=C:\Users"

# Run a shell command (cmd.exe /c); 30-second timeout
# Requires EnableShellRun = true in appsettings.json or APEX_ENABLE_SHELL_RUN=true
curl -H "X-Api-Key: <key>" "http://localhost:8081/run?cmd=whoami"
curl -H "X-Api-Key: <key>" -X POST http://localhost:8081/run \
     -H "Content-Type: application/json" \
     -d '{"value":"dir C:\\"}'
```

`/run` response data fields: `cmd`, `stdout`, `stderr`, `exit_code`.

> **Security note:** `/run` executes arbitrary commands as the process user. It is disabled by default and should only be enabled in trusted, authenticated environments.

---

### UI automation routes

```bash
# List all open windows (with stable IDs)
curl http://localhost:8081/windows

# Get current state
curl http://localhost:8081/status

# List all elements in the current window (nested JSON with IDs and bounding rectangles)
curl http://localhost:8081/elements

# Onscreen elements only — prunes offscreen subtrees for maximum token efficiency
curl "http://localhost:8081/elements?onscreen=true"

# Limit depth — truncated nodes show "childCount" so you know where to drill in
curl "http://localhost:8081/elements?depth=2&onscreen=true"

# Expand a specific node by numeric ID (preserves the rest of the map — IDs stay stable)
curl "http://localhost:8081/elements?id=<elementId>&depth=2&onscreen=true"

# Filter by ControlType
curl "http://localhost:8081/elements?type=Button"

# All filters combined
curl "http://localhost:8081/elements?depth=3&onscreen=true&type=Button"

# Render the current window's UI element tree as a colour-coded PNG (returns base64)
curl http://localhost:8081/uimap

# Help
curl http://localhost:8081/help

# Find a window and element by title/name
curl -X POST http://localhost:8081/find \
     -H "Content-Type: application/json" \
     -d '{"window":"Notepad","id":"15"}'

# Find by element name with ControlType filter
curl -X POST http://localhost:8081/find \
     -H "Content-Type: application/json" \
     -d '{"window":"Notepad","name":"Text Editor","type":"Edit"}'

# Find by numeric window/element IDs (fast, no fuzzy search)
curl -X POST http://localhost:8081/find \
     -H "Content-Type: application/json" \
     -d '{"window":42,"id":105}'

# Type text into the found element
curl -X POST http://localhost:8081/execute \
     -H "Content-Type: application/json" \
     -d '{"action":"type","value":"Hello World"}'

# Click a button
curl -X POST http://localhost:8081/execute \
     -H "Content-Type: application/json" \
     -d '{"action":"click"}'

# Read text from element
curl -X POST http://localhost:8081/execute \
     -H "Content-Type: application/json" \
     -d '{"action":"gettext"}'

# Capture current element (returns base64 PNG in data field)
curl -X POST http://localhost:8081/capture

# Capture full screen
curl -X POST http://localhost:8081/capture \
     -H "Content-Type: application/json" \
     -d '{"action":"screen"}'

# Capture multiple elements stitched into one image
curl -X POST http://localhost:8081/capture \
     -H "Content-Type: application/json" \
     -d '{"action":"elements","value":"42,105,106"}'

# OCR the found element
curl -X POST http://localhost:8081/ocr

# OCR a region (x,y,width,height) within the element
curl -X POST http://localhost:8081/ocr \
     -H "Content-Type: application/json" \
     -d '{"value":"0,0,300,50"}'

# Check AI model status
curl http://localhost:8081/ai/status

# Load a vision/audio LLM (run once; model stays loaded until the server restarts)
curl -X POST http://localhost:8081/ai/init \
     -H "Content-Type: application/json" \
     -d '{"model":"C:\\models\\vision.gguf","proj":"C:\\models\\mmproj.gguf"}'

# Describe the currently selected UI element using the vision model
# Captures the element as an image and sends it to the LLM
curl -X POST http://localhost:8081/ai/describe

# Describe with a custom prompt
curl -X POST http://localhost:8081/ai/describe \
     -H "Content-Type: application/json" \
     -d '{"prompt":"List every button you can see."}'

# Ask a specific question about the current element
curl -X POST http://localhost:8081/ai/ask \
     -H "Content-Type: application/json" \
     -d '{"prompt":"Is there an error message visible?"}'

# Describe an image file on disk
curl -X POST http://localhost:8081/ai/file \
     -H "Content-Type: application/json" \
     -d '{"value":"C:\\screenshots\\app.png","prompt":"What dialog is shown?"}'
```

### Request body fields

| Field | Aliases | Description |
|---|---|---|
| `window` | — | Window title (partial match) or numeric ID from `/windows` |
| `automationId` | `id` | Element AutomationId string or numeric ID from `/elements` |
| `elementName` | `name` | Element Name property (fallback if `id` not given) |
| `searchType` | `type` | ControlType filter (`All` or e.g. `Button`) |
| `action` | — | Action name (see list below) |
| `value` | — | Value/input for the action |
| `model` | `modelPath` | AI: path to LLM `.gguf` file |
| `proj` | `mmProjPath` | AI: path to multimodal projector `.gguf` file |
| `prompt` | — | AI: question or instruction text |

---

## Usage — AI Drawing

The drawing engine renders GDI+ shapes to a base64 PNG on demand. Every shape type supports colour, opacity, fill/stroke, and dashed lines.

### Quick draw

```bash
# Draw a filled blue circle with white text
curl -X POST http://localhost:8081/draw \
     -H "Content-Type: application/json" \
     -d '{
       "value": "{\"canvas\":\"blank\",\"width\":400,\"height\":300,\"shapes\":[
         {\"type\":\"circle\",\"x\":200,\"y\":150,\"r\":80,\"color\":\"royalblue\",\"fill\":true},
         {\"type\":\"text\",\"x\":200,\"y\":140,\"text\":\"Hello!\",\"color\":\"white\",\"font_size\":20,\"font_bold\":true,\"align\":\"center\"}
       ]}"
     }'

# Render the built-in space scene
curl http://localhost:8081/draw/demo

# Show it as a full-screen overlay for 6 seconds
curl "http://localhost:8081/draw/demo?overlay=true&ms=6000"
```

The `data.result` field contains the base64 PNG. The web console renders it inline.

### Shape types

| Type | Key fields | Description |
|---|---|---|
| `rect` | `x y w h corner_radius` | Rectangle (rounded if `corner_radius > 0`) |
| `ellipse` | `x y w h` | Ellipse inside bounding box |
| `circle` | `x y r` | Circle — x,y is the centre |
| `line` | `x y x2 y2` | Straight line |
| `arrow` | `x y x2 y2` | Line with arrowhead at (x2,y2) |
| `polygon` | `points[]` | Closed polygon — flat array of x,y pairs |
| `triangle` | `x y w h` | Triangle — bounding-box anchored, top-centre apex |
| `arc` | `x y w h start_angle sweep_angle` | Open arc — angles in degrees, clockwise from 3 o'clock |
| `text` | `x y text font_size font_bold align background` | Rendered text |

**Common fields on all shapes:** `color`, `fill` (bool), `stroke_width`, `opacity` (0–1), `dashed` (bool), `rotation` (degrees, centre-origin).

**Canvas values:** `blank` (transparent), `white`, `black`, `screen` (live screenshot), `window` (current window), `element` (current element).

---

## Usage — Layered Scene Editor

The scene system lets AI agents and users collaborate on persistent, structured drawings. Every shape has a stable ID; coordinates are always accurate; the AI can read them back and refine the composition at any time.

### REST API (`/scenes/*`)

```bash
# Create a scene
curl -X POST http://localhost:8081/scenes \
     -H "Content-Type: application/json" \
     -d '{"name":"My Scene","width":800,"height":600,"background":"#1a1a2e"}'
# → data.scene contains the full scene with id

# List scenes
curl http://localhost:8081/scenes

# Get a scene
curl http://localhost:8081/scenes/{id}

# Add a layer
curl -X POST http://localhost:8081/scenes/{id}/layers \
     -H "Content-Type: application/json" \
     -d '{"name":"Background"}'

# Add a shape to a layer
curl -X POST http://localhost:8081/scenes/{id}/layers/{lid}/shapes \
     -H "Content-Type: application/json" \
     -d '{"shape":{"type":"circle","x":400,"y":300,"r":80,"color":"royalblue","fill":true},"name":"Planet"}'

# Render the scene to a PNG
curl http://localhost:8081/scenes/{id}/render
# → data.result is base64 PNG

# Patch shape geometry (after user drags it — never clobbers color/style)
curl -X PATCH http://localhost:8081/scenes/{id}/layers/{lid}/shapes/{sid} \
     -H "Content-Type: application/json" \
     -d '{"x":420,"y":310}'

# Move a shape to a different layer
curl -X POST http://localhost:8081/scenes/{id}/shapes/{sid}/move \
     -H "Content-Type: application/json" \
     -d '{"target_layer_id":"{newLayerId}"}'

# Delete a shape / layer / scene
curl -X DELETE http://localhost:8081/scenes/{id}/layers/{lid}/shapes/{sid}
curl -X DELETE http://localhost:8081/scenes/{id}/layers/{lid}
curl -X DELETE http://localhost:8081/scenes/{id}
```

### Full route reference

| Method | Route | Description |
|---|---|---|
| `GET` / `POST` | `/scenes` | List all scenes / create scene |
| `GET` / `PUT` / `DELETE` | `/scenes/{id}` | Get / update meta / delete scene |
| `GET` | `/scenes/{id}/render` | Render scene → base64 PNG |
| `GET` / `POST` | `/scenes/{id}/layers` | List layers / add layer |
| `GET` / `PUT` / `DELETE` | `/scenes/{id}/layers/{lid}` | Get / update / delete layer |
| `GET` / `POST` | `/scenes/{id}/layers/{lid}/shapes` | List shapes / add shape |
| `GET` / `PUT` / `PATCH` / `DELETE` | `/scenes/{id}/layers/{lid}/shapes/{sid}` | Get / replace / patch geometry / delete shape |
| `POST` | `/scenes/{id}/shapes/{sid}/move` | Move shape to a different layer |

### Scene Editor — WinForms (Tools → Scene Editor)

The desktop editor opens a standalone window with:

- **Scene list** — create, select, or delete scenes
- **Toolbar** — arrow (select/move), rect, ellipse, circle, line, text, delete
- **Canvas** — double-buffered; drag shapes to reposition; draw new shapes by clicking and dragging; mouse wheel to zoom
- **Layers panel** — add/delete layers; click to select the active layer; eye icon to toggle visibility
- **Properties panel** — x, y, w, h, r fields for the selected shape; edits commit to the store immediately
- **Keyboard shortcuts** — V/R/E/C/L/T for tools, Delete to remove selected shape, Escape to deselect

All changes are persisted to disk (`<exe>/scenes/{id}.json`) and immediately available via the REST API.

### Scene Editor — Browser (`GET /editor`)

Open `http://localhost:8081/editor` for the same editing experience in a browser:

- HTML5 Canvas renderer for all 7 shape types
- Click-and-drag to place shapes; click to select and drag to move
- Layer panel with add/delete/visibility toggle
- Properties panel showing live coordinates
- Keyboard shortcuts (V/R/E/C/L/T, Delete, Escape)
- All changes sync to the same `/scenes/*` REST API

---

## Usage — Telegram Bot

After starting the bot, send commands to it in any Telegram chat:

```
/find window=Notepad id=15
/find window=Calculator name=Equals type=Button
/exec action=type value="Hello from Telegram"
/exec action=click
/exec action=gettext
/ocr
/ocr value=0,0,300,50
/status
/windows
/elements
/elements type=Button
/help
```

Key=value pairs support quoted values for multi-word strings:
```
/find window="My Application" name="Save Button"
/exec action=type value="some text with spaces"
```

AI commands work the same way:
```
/ai action=status
/ai action=init model=C:\models\vision.gguf proj=C:\models\mmproj.gguf
/ai action=describe
/ai action=describe prompt="List every button you can see."
/ai action=ask prompt="Is there an error message visible?"
/ai action=file value=C:\screenshots\app.png prompt="What dialog is shown?"
```

---

## Usage — PowerShell

The app exposes a **named pipe server** (default name `ApexComputerUse`). Start it from the **Remote Control** group box, then use the bundled `ApexComputerUse.psm1` module:

```powershell
# Import the module
Import-Module .\Scripts\ApexComputerUse.psm1

# Connect to the pipe (must be started in the app first)
Connect-FlaUI                        # default pipe name: ApexComputerUse
Connect-FlaUI -PipeName MyPipe -TimeoutMs 10000

# Discovery
Get-FlaUIWindows                     # list all open window titles
Get-FlaUIStatus                      # current window/element state
Get-FlaUIHelp                        # command reference
Get-FlaUIElements                    # list all elements in current window
Get-FlaUIElements -Type Button       # filter by ControlType

# Find
Find-FlaUIElement -Window 'Notepad'
Find-FlaUIElement -Window 'Notepad' -Name 'Text Editor' -Type Edit
Find-FlaUIElement -Window 'Calculator' -Id 'num5Button'

# Execute actions
Invoke-FlaUIAction -Action click
Invoke-FlaUIAction -Action type  -Value 'Hello from PowerShell'
Invoke-FlaUIAction -Action gettext
Invoke-FlaUIAction -Action screenshot

# OCR
Invoke-FlaUIOcr
Invoke-FlaUIOcr -Region '0,0,300,50'

# AI
Invoke-FlaUIAi -SubCommand init     -Model 'C:\models\v.gguf' -Proj 'C:\models\p.gguf'
Invoke-FlaUIAi -SubCommand status
Invoke-FlaUIAi -SubCommand describe -Prompt 'What buttons are visible?'
Invoke-FlaUIAi -SubCommand ask      -Prompt 'Is there an error message?'
Invoke-FlaUIAi -SubCommand file     -Value 'C:\screen.png' -Prompt 'Describe this.'

# Send raw JSON (advanced)
Send-FlaUICommand @{ command='find'; window='Notepad'; elementName='Text Editor' }

# Disconnect
Disconnect-FlaUI
```

### PowerShell cmdlet reference

| Cmdlet | Key Parameters | Description |
|---|---|---|
| `Connect-FlaUI` | `PipeName`, `TimeoutMs` | Connect to the pipe server |
| `Disconnect-FlaUI` | — | Close the connection |
| `Send-FlaUICommand` | `Request` (hashtable) | Send a raw JSON command |
| `Get-FlaUIWindows` | — | List open window titles |
| `Get-FlaUIStatus` | — | Show current window/element |
| `Get-FlaUIHelp` | — | Server command reference |
| `Get-FlaUIElements` | `Type` | List elements in current window |
| `Find-FlaUIElement` | `Window`, `Id`, `Name`, `Type` | Find a window and element |
| `Invoke-FlaUIAction` | `Action`, `Value` | Execute action on current element |
| `Invoke-FlaUIOcr` | `Region` | OCR current element or region |
| `Invoke-FlaUICapture` | `Target`, `Value` | Capture screen/window/element(s); returns base64 PNG in `data` |
| `Invoke-FlaUIAi` | `SubCommand`, `Model`, `Proj`, `Prompt`, `Value` | Multimodal AI sub-commands |

> The pipe connection is **session-based**: window and element state are preserved across calls within a single `Connect-FlaUI` / `Disconnect-FlaUI` session. Use `Find-FlaUIElement` to select a target, then call `Invoke-FlaUIAction` as many times as needed without re-finding.

---

## Usage — cmd.exe

Use `Scripts\apex.cmd` — a batch helper that wraps the HTTP server with simpler positional syntax. Requires the HTTP server to be started first and `curl` (built-in on Windows 10+).

```batch
:: Optional: override port (default is 8081)
set APEX_HTTP_PORT=8081

:: Discovery
apex windows
apex status
apex elements
apex elements Button
apex help

:: Find a window and element
apex find Notepad
apex find "My App" id=btnOK
apex find Notepad name="Text Editor" type=Edit

:: Execute actions
apex exec click
apex exec type value=Hello
apex exec gettext
apex exec screenshot

:: Capture
apex capture
apex capture action=screen
apex capture action=window
apex capture action=elements value=42,105,106

:: OCR
apex ocr
apex ocr 0,0,300,50

:: AI
apex ai status
apex ai init model=C:\models\v.gguf proj=C:\models\p.gguf
apex ai describe
apex ai describe prompt="What do you see?"
apex ai ask prompt="Is there an error message?"
apex ai file value=C:\screen.png prompt="Describe this."
```

Add `Scripts\` to your `PATH` (or copy `apex.cmd` next to your scripts) to use it from any directory.

---

## Usage — AI (Multimodal)

The AI command set is backed by `MtmdHelper`, which uses [LLamaSharp](https://github.com/SciSharp/LLamaSharp) to run a local multimodal (vision + audio) LLM. No cloud API is required.

### Setup

Download a vision-capable GGUF model and its multimodal projector (e.g. LFM2.5-VL from LM Studio) and note the paths to both `.gguf` files, or use **Download All** on the Model tab. Then call `ai init` before any inference commands.

### AI sub-commands

| Sub-action | Required params | Optional params | Description |
|---|---|---|---|
| `init` | `model=<path>` `proj=<path>` | — | Load the LLM and projector into memory |
| `status` | — | — | Report whether the model is loaded and which modalities it supports |
| `describe` | — (uses current element) | `prompt=<text>` | Capture the current UI element as an image and ask the vision model to describe it |
| `ask` | `prompt=<text>` | — | Ask a specific question about the current UI element (captures element image) |
| `file` | `value=<file path>` | `prompt=<text>` | Send an image or audio file from disk to the model |

> **Note:** `describe`, `ask`, and `file` require a prior `find` command to select a window/element. The model must be initialized with `init` before any inference call. Each inference call starts completely fresh — no chat history is retained between calls.

### AI Vision in the test console

The HTTP test console (`GET /`) has a dedicated **AI Vision** button group (purple-tinted):

| Button | Endpoint | Value field |
|---|---|---|
| **status** | `GET /ai/status` | — |
| **describe** | `POST /ai/describe` | Optional prompt (e.g. `list all buttons`) |
| **ask** | `POST /ai/ask` | Required question (e.g. `what number is shown?`) |

Select an element in the Elements panel first, then click **describe** or **ask**. The console shows a "Running vision model…" notice immediately and updates with the result when inference completes.

---

## UI Map Renderer

The UI Map Renderer scans the current window's accessibility tree and renders every element's bounding rectangle as a colour-coded overlay. Each control type gets a deterministic, visually distinct colour. Element names are drawn inside the bounding box.

### Via HTTP API

```bash
# Returns base64-encoded PNG of the current window's element tree
curl http://localhost:8081/uimap
```

Requires a prior `find` call to select a window. The response `data.result` field contains the base64 PNG — identical format to the `/capture` endpoints. In the interactive test console, the **UI map** button (in the Capture group) renders the result inline in the response log.

### Via the desktop UI

**Tools → Render UI Map** draws the overlay directly on screen for 5 seconds (press Escape to dismiss early) and offers to save it as a PNG file. This also triggers a live screen overlay, which is not available via the HTTP API.

**Tools → Output UI Map** logs the raw nested JSON element tree to the console tab — useful for inspecting the tree structure or copying it for use with an AI agent.

Element JSON includes bounding rectangles:
```json
{
  "id": 105,
  "controlType": "Button",
  "name": "OK",
  "automationId": "btn_ok",
  "boundingRectangle": { "x": 120, "y": 340, "width": 80, "height": 30 },
  "children": []
}
```

---

## Available Actions (exec/execute)

### General

| Action | Aliases | Value | Description |
|---|---|---|---|
| `click` | — | — | Smart click: Invoke → Toggle → SelectionItem → mouse fallback |
| `mouse-click` | `mouseclick` | — | Force mouse left-click (bypasses smart chain) |
| `middle-click` | `middleclick` | — | Middle-mouse-button click |
| `invoke` | — | — | Invoke pattern directly |
| `right-click` | `rightclick` | — | Right-click |
| `double-click` | `doubleclick` | — | Double-click |
| `click-at` | `clickat` | `x,y` | Click at pixel offset from element top-left |
| `drag` | — | `x,y` | Drag element to screen coordinates |
| `hover` | — | — | Move mouse over element |
| `highlight` | — | — | Draw orange highlight around element for 1 second |
| `focus` | — | — | Set keyboard focus |
| `keys` | — | text | Send keystrokes; supports `{CTRL}`, `{ALT}`, `{SHIFT}`, `{F5}`, `Ctrl+A`, `Alt+F4`, etc. |
| `screenshot` | `capture` | — | Save element image to `Desktop\Apex_Captures` |
| `describe` | — | — | Return full element property description (UIA properties — not AI vision) |
| `patterns` | — | — | List automation patterns supported by the element |
| `bounds` | — | — | Return bounding rectangle |
| `isenabled` | — | — | Returns `True` or `False` |
| `isvisible` | — | — | Returns `True` or `False` |
| `wait` | — | automationId | Wait for element with given AutomationId to appear |

### Text / Value

| Action | Aliases | Value | Description |
|---|---|---|---|
| `type` | `enter` | text | Enter text (smart: Value pattern → keyboard) |
| `insert` | — | text | Type at current caret position |
| `gettext` | `text` | — | Smart read: Text pattern → Value → LegacyIAccessible → Name |
| `getvalue` | `value` | — | Smart read: Value → Text → LegacyIAccessible → Name |
| `setvalue` | — | text | Smart set: Value pattern (if writable) → RangeValue (if numeric) → keyboard |
| `clearvalue` | — | — | Set value to empty string via Value pattern |
| `appendvalue` | — | text | Append text to current value |
| `getselectedtext` | — | — | Get selected text via Text pattern |
| `selectall` | — | — | Ctrl+A |
| `copy` | — | — | Ctrl+C |
| `cut` | — | — | Ctrl+X |
| `paste` | — | — | Ctrl+V |
| `undo` | — | — | Ctrl+Z |
| `clear` | — | — | Select all and delete |

### Range / Slider

| Action | Aliases | Value | Description |
|---|---|---|---|
| `setrange` | — | number | Set RangeValue pattern |
| `getrange` | — | — | Read current RangeValue |
| `rangeinfo` | — | — | Min / max / smallChange / largeChange |

### Toggle / CheckBox

| Action | Aliases | Value | Description |
|---|---|---|---|
| `toggle` | — | — | Toggle CheckBox (cycles state) |
| `toggle-on` | `toggleon` | — | Set toggle to On |
| `toggle-off` | `toggleoff` | — | Set toggle to Off |
| `gettoggle` | — | — | Read current toggle state (`On` / `Off` / `Indeterminate`) |

### Expand / Collapse

| Action | Aliases | Value | Description |
|---|---|---|---|
| `expand` | — | — | Expand via ExpandCollapse pattern |
| `collapse` | — | — | Collapse via ExpandCollapse pattern |
| `expandstate` | — | — | Read current ExpandCollapse state |

### Selection (SelectionItem / Selection)

| Action | Aliases | Value | Description |
|---|---|---|---|
| `select` | — | item text | Select ComboBox/ListBox item by text |
| `select-item` | `selectitem` | — | Select current element via SelectionItem pattern |
| `addselect` | — | — | Add element to multi-selection |
| `removeselect` | — | — | Remove element from selection |
| `isselected` | — | — | Returns `True` or `False` |
| `getselection` | — | — | Get selected items from a Selection container |
| `select-index` | `selectindex` | n | Select ComboBox/ListBox item by zero-based index |
| `getitems` | — | — | List all items in a ComboBox or ListBox (newline-separated) |
| `getselecteditem` | — | — | Get currently selected item text |

### Window State

| Action | Aliases | Value | Description |
|---|---|---|---|
| `minimize` | — | — | Minimize window |
| `maximize` | — | — | Maximize window |
| `restore` | — | — | Restore window to normal state |
| `windowstate` | — | — | Read current window visual state (Normal / Maximized / Minimized) |

### Transform (Move / Resize)

| Action | Aliases | Value | Description |
|---|---|---|---|
| `move` | — | `x,y` | Move element via Transform pattern |
| `resize` | — | `w,h` | Resize element via Transform pattern |

### Scroll

| Action | Aliases | Value | Description |
|---|---|---|---|
| `scroll-up` | `scrollup` | n (optional) | Scroll up n clicks (default 3) |
| `scroll-down` | `scrolldown` | n (optional) | Scroll down n clicks (default 3) |
| `scroll-left` | `scrollleft` | n (optional) | Horizontal scroll left n clicks (default 3) |
| `scroll-right` | `scrollright` | n (optional) | Horizontal scroll right n clicks (default 3) |
| `scrollinto` | `scrollintoview` | — | Scroll element into view |
| `scrollpercent` | — | `h,v` | Scroll to h%/v% position via Scroll pattern (0–100) |
| `getscrollinfo` | — | — | Scroll position and scrollable flags |

### Grid / Table

| Action | Aliases | Value | Description |
|---|---|---|---|
| `griditem` | — | `row,col` | Get element description at grid cell |
| `gridinfo` | — | — | Row and column counts |
| `griditeminfo` | — | — | Row / column / span for a GridItem element |

---

## Capture

Returns a screen capture inline as a base64-encoded PNG in the `data` field. Supports four targets.

| Target | Description |
|---|---|
| `element` (default) | Current element (requires a prior `find`) |
| `window` | Current window (requires a prior `find`) |
| `screen` | Full display |
| `elements` | Multiple elements by ID, stitched vertically into one image |

For `elements`, provide comma-separated numeric IDs from a prior `elements` scan in the `value` field.

```bash
# Current element
curl -X POST http://localhost:8081/capture

# Full screen
curl -X POST http://localhost:8081/capture \
     -H "Content-Type: application/json" \
     -d '{"action":"screen"}'

# Current window
curl -X POST http://localhost:8081/capture \
     -H "Content-Type: application/json" \
     -d '{"action":"window"}'

# Multiple elements stitched into one image
curl -X POST http://localhost:8081/capture \
     -H "Content-Type: application/json" \
     -d '{"action":"elements","value":"42,105,106"}'
```

Response `data` field contains the base64 PNG. Decode it to get the image:
```bash
curl -s -X POST http://localhost:8081/capture -d '{"action":"screen"}' \
  | python -c "import sys,json,base64; d=json.load(sys.stdin)['data']; open('screen.png','wb').write(base64.b64decode(d))"
```

**Telegram:** `/capture` sends the image as a photo message (not text).
```
/capture
/capture action=screen
/capture action=window
/capture action=elements value=42,105,106
```

**PowerShell:**
```powershell
$r = Send-FlaUICommand @{ command='capture'; action='screen' }
[IO.File]::WriteAllBytes('screen.png', [Convert]::FromBase64String($r.data))
```

> **Note:** This is distinct from the `screenshot` exec action, which saves to `Desktop\Apex_Captures` and returns only the file path.

---

## OCR

OCR uses [Tesseract](https://github.com/tesseract-ocr/tesseract). Download language files from [github.com/tesseract-ocr/tessdata](https://github.com/tesseract-ocr/tessdata) and place them in a `tessdata\` folder next to the executable (e.g. `tessdata\eng.traineddata`). Additional languages work the same way.

Captures saved by **OCR Element + Save** go to `Desktop\Apex_Captures\`.

---

## AI (Multimodal)

The AI command set is backed by `MtmdHelper` using LLamaSharp's multimodal (MTMD) API. Supports vision and audio modalities depending on the model. Every inference call is fully stateless — no chat history is retained between calls.

Download a vision-capable GGUF model and its multimodal projector (e.g. LFM2.5-VL from LM Studio) and note the paths to both `.gguf` files, or click **Download All** on the Model tab. Then call `ai init` before any inference commands.

---

## Project Structure

```
ApexComputerUse/
├── Form1.cs / Form1.Designer.cs         — Main UI (tabs: Console, Find & Execute, Remote Control, Model)
├── AiChatForm.cs / AiChatForm.Designer.cs — AI Chat window (Tools → AI Chat); 8-provider streaming chat
├── AiChatService.cs                     — Thread-safe service layer wrapping AiMessagingCore; manages provider config, session lifecycle, and streaming
├── FlaUIHelper.cs                       — All FlaUI automation wrappers
├── ElementIdGenerator.cs                — Stable SHA-256 hash-based element ID mapping
├── CommandProcessor.cs                  — Shared remote command logic (used by all server types)
├── HttpCommandServer.cs                 — HTTP REST server (System.Net.HttpListener)
│     ├── ApexResult                     — Canonical {success, action, data, error} result type
│     ├── FormatAdapter                  — Format negotiation (HTML / JSON / text / PDF)
│     └── PdfWriter                      — Minimal PDF generator (no external dependencies)
├── PipeCommandServer.cs                 — Named-pipe server
├── TelegramController.cs                — Telegram bot (Telegram.Bot)
├── OcrHelper.cs                         — Tesseract OCR wrapper
├── MtmdHelper.cs                        — Stateless multimodal LLM wrapper (LLamaSharp MTMD)
├── MtmdInteractiveModeExecute.cs        — Interactive AI computer use mode (Tools menu)
├── UiMapRenderer.cs                     — Renders element trees as colour-coded screen overlays and PNG images
├── AIDrawingCommand.cs                  — GDI+ drawing engine; 9 shape types; screen overlay; built-in space scene
├── Scene.cs                             — SceneShape / Layer / Scene data models with stable IDs
├── SceneStore.cs                        — Thread-safe in-memory + disk-persisted scene store
├── SceneEditorForm.cs / .Designer.cs    — WinForms layered scene editor (Tools → Scene Editor)
├── ai-settings.json                     — Starter config for AI Chat providers (placeholder keys)
└── Scripts/
    ├── ApexComputerUse.psm1             — PowerShell module (pipe-based)
    └── apex.cmd                         — cmd.exe helper (HTTP-based)

AIClients/                               — AI messaging projects (also in ApexComputerUse.sln)
├── AiMessagingCore/                     — Provider-neutral .NET 10 library; 8 providers; streaming via events
└── AIClients/                           — Standalone WinForms chat harness (builds independently via AIClients.sln)
```

> **OCR:** place Tesseract language files in a `tessdata\` folder next to the executable. Not included in the repo — download from [github.com/tesseract-ocr/tessdata](https://github.com/tesseract-ocr/tessdata).

---

## Development

### Build

```powershell
# Restore and build (Release)
dotnet build -c Release ApexComputerUse/ApexComputerUse.csproj

# Run from source
dotnet run --project ApexComputerUse/ApexComputerUse.csproj
```

Requires the **.NET 10 SDK** and the **Windows Desktop workload** (`dotnet workload install windows`).

### Unit Tests

```powershell
dotnet test ApexComputerUse.Tests/ApexComputerUse.Tests.csproj
```

The test suite covers the pure-logic and data-model layers — everything that can be tested without a live desktop session:

| Test file | Coverage area |
|---|---|
| `ElementIdGeneratorTests.cs` | Hash mode, incremental mode, reset, thread safety |
| `SceneStoreTests.cs` | CRUD, disk persistence, concurrent creates |
| `SceneModelTests.cs` | `FlattenForRender`, ZIndex ordering, opacity, `SceneIds` |
| `AIDrawingCommandTests.cs` | JSON parsing, canvas backgrounds, all 8 shape types |
| `TelegramParseCommandTests.cs` | Command + key-value parser, `DictExtensions.Get` |
| `PipeCommandServerTests.cs` | Named-pipe JSON protocol parser |
| `LevenshteinTests.cs` | Edit-distance boundary and domain cases |
| `CommandResponseTests.cs` | `ToText` / `ToJson` serialisation |
| `OcrHelperTests.cs` | `CropBitmap` region logic, `OcrResult.ToString` |

Components that require an active Windows session (FlaUI UIA, Tesseract, LLamaSharp, WinForms UI) are covered by the existing integration script `Scripts/test_controls.py` and manual testing.
