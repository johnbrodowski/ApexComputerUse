# ApexComputerUse

**Structured Windows UI automation for AI agents — accurate, token-efficient, and framework-agnostic.**

ApexComputerUse uses the **Windows UI Automation API (UIA3 via FlaUI)** to expose every desktop application and browser as a structured, named element tree over a plain **HTTP REST API**. AI agents interact with elements by name or stable ID — no screenshots, no pixel coordinates, no vision model required.

Works on Win32, WPF, UWP, WinForms, and browsers. Controlled via **HTTP REST**, **named pipes**, **cmd.exe**, and **Telegram**.

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

### Stable element IDs

Every element is assigned a **SHA-256 hash-based numeric ID** derived from its control type, name, AutomationId, and position in the tree. These IDs are stable across sessions — an agent can reference the same element in turn 1 and turn 20 without re-querying the tree. No other tool in the Windows desktop automation space publishes this property.

### The onscreen filter

`GET /elements?onscreen=true` prunes any element where `IsOffscreen = true` during the tree scan, skipping entire offscreen subtrees. On a live Chewy.com product page this reduces **634 elements to 126** — an 80% reduction — putting token cost per step in the same range as the best browser-only tools while covering all desktop apps too.

The filter composes with the existing type filter: `?onscreen=true&type=Button`.

---

## Features

- Find any window and element by name or AutomationId (exact or fuzzy match)
- Filter element search by ControlType
- Persistent, hash-based stable element and window IDs (survive app restarts)
- Onscreen-only element map (`?onscreen=true`) — prunes offscreen subtrees at scan time
- Element nodes include `boundingRectangle` (x, y, width, height) for spatial context and visual rendering
- Execute all common UI actions: click, type, select, toggle, scroll, drag & drop, etc.
- OCR any UI element using Tesseract
- Multimodal AI: describe UI elements, ask questions about them, analyse image/audio files using a local vision LLM (LLamaSharp MTMD)
- Remote control via HTTP REST API (curl-friendly JSON)
- Remote control via named pipe (PowerShell module included)
- Remote control via cmd.exe batch helper (`apex.cmd`)
- Remote control via Telegram bot
- Screenshot capture of elements, windows, and full screen (returned as base64 PNG)
- **Interactive HTTP test console** — served at `GET /`, includes live windows list, element tree browser, grouped command builder, inline capture/OCR/AI vision buttons, and a response log
- **UI Map Renderer** — renders the element tree as a colour-coded overlay drawn directly on screen, and optionally exports a PNG image; accessible via Tools → Render UI Map or `GET /uimap` (returns base64 PNG)
- **Format-adaptive responses** — every endpoint serves HTML, plain text, or JSON via `?format=` or `Accept` header; default is an HTML page with embedded JSON readable by any AI that can fetch a URL
- **System utility routes** — `/ping`, `/sysinfo`, `/env`, `/ls`, `/run` for AI agents that need OS-level context without a separate tool

---

## Setup

### 1. tessdata (required for OCR)

Place `eng.traineddata` (and any other language files) in:

```
bin\Debug\net10.0-windows\tessdata\
```

The file is already included in the project and copied on build. Additional languages can be downloaded from [github.com/tesseract-ocr/tessdata](https://github.com/tesseract-ocr/tessdata).

### 2. Telegram Bot (optional)

1. Message [@BotFather](https://t.me/BotFather) on Telegram and create a bot with `/newbot`.
2. Copy the token (format: `123456789:ABC-DEF...`).
3. Paste it into the **Bot Token** field in the app and click **Start Telegram**.

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

---

## Window and Element ID Mapping

Every window and element is assigned a **stable numeric ID** (SHA-256 hash-based) that persists across sessions. These IDs can be used in `find` commands instead of titles or AutomationIds.

```bash
# 1. Get windows with their IDs
curl http://localhost:8080/windows
# Returns: [{"id":42,"title":"Notepad"},{"id":107,"title":"Calculator"},...]

# 2. Get elements with their IDs for the current window
curl http://localhost:8080/elements

# Onscreen elements only (prunes offscreen subtrees — 80% fewer elements on browser pages)
curl "http://localhost:8080/elements?onscreen=true"

# Combine with type filter
curl "http://localhost:8080/elements?onscreen=true&type=Button"

# Returns nested JSON including bounding rectangles:
# {
#   "id": 105,
#   "controlType": "Edit",
#   "name": "Text Editor",
#   "automationId": "15",
#   "boundingRectangle": { "x": 0, "y": 30, "width": 800, "height": 600 },
#   "children": [...]
# }

# 3. Find using numeric IDs (no fuzzy matching, direct map lookup)
curl -X POST http://localhost:8080/find \
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

### Assumptions Used Below

| | Screen Capture | Map Approach |
|---|---|---|
| Per-interaction cost | 2,500–10,000 tokens (image) | 5–20 tokens (text reference) |
| Session setup cost | none — image sent every time | 400–1,800 tokens (one-time map render) |
| Interactions per person/day | 100 | 100 |

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

Start the HTTP server from the **Remote Control** group box, then use curl or open `http://localhost:8080/` in a browser to access the interactive test console.

### Interactive Test Console (`GET /`)

Opening the root URL in any browser launches a dark-themed console with:

- **Windows panel** — live list of all open windows; click to select and auto-load its element tree
- **Elements panel** — nested element tree flattened with indentation; onscreen-only toggle; ControlType filter; click any element to select it
- **Command builder** — grouped action buttons (Click, Text, Keys, State, Scroll, Toggle, Select, Window, Capture, AI Vision); Capture group includes a **UI map** button that renders the current window's element tree as a base64 PNG inline; Value input with context hints; ▶ Execute button
- **AI Vision buttons** — `status` (check model), `describe` (capture element → vision model), `ask` (question about element); requires model loaded on the Model tab
- **Response log** — newest result at top; captures rendered as inline images (click to zoom)

### Format negotiation

Every endpoint supports three response formats, selected by priority:

1. `?format=` query parameter (`html`, `text`, or `json`)
2. `Accept` request header (`text/html`, `text/plain`, or `application/json`)
3. Default: `html`

```bash
# HTML response (default — works in any browser or AI that can fetch a page)
curl http://localhost:8080/ping

# Plain text — compact key:value lines
curl "http://localhost:8080/ping?format=text"

# JSON — structured data
curl "http://localhost:8080/ping?format=json"

# Via Accept header
curl -H "Accept: application/json" http://localhost:8080/ping
```

The HTML response includes a `<pre>` block for human readability and an embedded `<script type="application/json" id="apex-result">` block containing the full result as JSON — allowing any AI that can fetch a webpage to extract structured data without a vision model.

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
curl http://localhost:8080/ping

# System information (OS, machine, user, CPU, CLR)
curl http://localhost:8080/sysinfo

# All environment variables
curl http://localhost:8080/env

# Directory listing (defaults to current working directory)
curl http://localhost:8080/ls
curl "http://localhost:8080/ls?path=C:\Users"

# Run a shell command (cmd.exe /c); 30-second timeout
curl "http://localhost:8080/run?cmd=whoami"
curl -X POST http://localhost:8080/run \
     -H "Content-Type: application/json" \
     -d '{"value":"dir C:\\"}'
```

`/run` response data fields: `cmd`, `stdout`, `stderr`, `exit_code`.

---

### UI automation routes

```bash
# List all open windows (with stable IDs)
curl http://localhost:8080/windows

# Get current state
curl http://localhost:8080/status

# List all elements in the current window (nested JSON with IDs and bounding rectangles)
curl http://localhost:8080/elements

# Onscreen elements only — prunes offscreen subtrees for maximum token efficiency
curl "http://localhost:8080/elements?onscreen=true"

# Filter by ControlType
curl "http://localhost:8080/elements?type=Button"

# Both filters combined
curl "http://localhost:8080/elements?onscreen=true&type=Button"

# Render the current window's UI element tree as a colour-coded PNG (returns base64)
curl http://localhost:8080/uimap

# Help
curl http://localhost:8080/help

# Find a window and element by title/name
curl -X POST http://localhost:8080/find \
     -H "Content-Type: application/json" \
     -d '{"window":"Notepad","id":"15"}'

# Find by element name with ControlType filter
curl -X POST http://localhost:8080/find \
     -H "Content-Type: application/json" \
     -d '{"window":"Notepad","name":"Text Editor","type":"Edit"}'

# Find by numeric window/element IDs (fast, no fuzzy search)
curl -X POST http://localhost:8080/find \
     -H "Content-Type: application/json" \
     -d '{"window":42,"id":105}'

# Type text into the found element
curl -X POST http://localhost:8080/execute \
     -H "Content-Type: application/json" \
     -d '{"action":"type","value":"Hello World"}'

# Click a button
curl -X POST http://localhost:8080/execute \
     -H "Content-Type: application/json" \
     -d '{"action":"click"}'

# Read text from element
curl -X POST http://localhost:8080/execute \
     -H "Content-Type: application/json" \
     -d '{"action":"gettext"}'

# Capture current element (returns base64 PNG in data field)
curl -X POST http://localhost:8080/capture

# Capture full screen
curl -X POST http://localhost:8080/capture \
     -H "Content-Type: application/json" \
     -d '{"action":"screen"}'

# Capture multiple elements stitched into one image
curl -X POST http://localhost:8080/capture \
     -H "Content-Type: application/json" \
     -d '{"action":"elements","value":"42,105,106"}'

# OCR the found element
curl -X POST http://localhost:8080/ocr

# OCR a region (x,y,width,height) within the element
curl -X POST http://localhost:8080/ocr \
     -H "Content-Type: application/json" \
     -d '{"value":"0,0,300,50"}'

# Check AI model status
curl http://localhost:8080/ai/status

# Load a vision/audio LLM (run once; model stays loaded until the server restarts)
curl -X POST http://localhost:8080/ai/init \
     -H "Content-Type: application/json" \
     -d '{"model":"C:\\models\\vision.gguf","proj":"C:\\models\\mmproj.gguf"}'

# Describe the currently selected UI element using the vision model
# Captures the element as an image and sends it to the LLM
curl -X POST http://localhost:8080/ai/describe

# Describe with a custom prompt
curl -X POST http://localhost:8080/ai/describe \
     -H "Content-Type: application/json" \
     -d '{"prompt":"List every button you can see."}'

# Ask a specific question about the current element
curl -X POST http://localhost:8080/ai/ask \
     -H "Content-Type: application/json" \
     -d '{"prompt":"Is there an error message visible?"}'

# Describe an image file on disk
curl -X POST http://localhost:8080/ai/file \
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
:: Optional: override port
set APEX_PORT=8080

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

Download a vision-capable GGUF model and its multimodal projector (e.g. LFM2-VL from LM Studio) and note the paths to both `.gguf` files. Then call `ai init` before any inference commands.

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
curl http://localhost:8080/uimap
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
curl -X POST http://localhost:8080/capture

# Full screen
curl -X POST http://localhost:8080/capture \
     -H "Content-Type: application/json" \
     -d '{"action":"screen"}'

# Current window
curl -X POST http://localhost:8080/capture \
     -H "Content-Type: application/json" \
     -d '{"action":"window"}'

# Multiple elements stitched into one image
curl -X POST http://localhost:8080/capture \
     -H "Content-Type: application/json" \
     -d '{"action":"elements","value":"42,105,106"}'
```

Response `data` field contains the base64 PNG. Decode it to get the image:
```bash
curl -s -X POST http://localhost:8080/capture -d '{"action":"screen"}' \
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

OCR uses Tesseract. The `tessdata` folder must contain the language file (default: `eng.traineddata`).

Captures saved by **OCR Element + Save** go to `Desktop\Apex_Captures\`.

---

## AI (Multimodal)

The AI command set is backed by `MtmdHelper` using LLamaSharp's multimodal (MTMD) API. Supports vision and audio modalities depending on the model. Every inference call is fully stateless — no chat history is retained between calls.

Download a vision-capable GGUF model and its multimodal projector (e.g. LFM2-VL from LM Studio) and note the paths to both `.gguf` files. Then call `ai init` before any inference commands.

---

## Project Structure

```
ApexComputerUse/
├── Form1.cs / Form1.Designer.cs   — Main UI (tabs: Console, Find & Execute, Remote Control, Model)
├── FlaUIHelper.cs                 — All FlaUI automation wrappers
├── ElementIdGenerator.cs          — Stable SHA-256 hash-based element ID mapping
├── CommandProcessor.cs            — Shared remote command logic (used by all server types)
├── HttpCommandServer.cs           — HTTP REST server (System.Net.HttpListener)
│     ├── ApexResult               — Canonical {success, action, data, error} result type
│     └── FormatAdapter            — Format negotiation and HTML/text/JSON renderers
├── PipeCommandServer.cs           — Named-pipe server
├── TelegramController.cs          — Telegram bot (Telegram.Bot)
├── OcrHelper.cs                   — Tesseract OCR wrapper
├── MtmdHelper.cs                  — Stateless multimodal LLM wrapper (LLamaSharp MTMD)
├── MtmdInteractiveModeExecute.cs  — Interactive AI computer use mode (Tools menu)
├── UiMapRenderer.cs               — Renders element trees as colour-coded screen overlays and PNG images
├── tessdata/
│   └── eng.traineddata
└── Scripts/
    ├── ApexComputerUse.psm1       — PowerShell module (pipe-based)
    └── apex.cmd                   — cmd.exe helper (HTTP-based)
```
