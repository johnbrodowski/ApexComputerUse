# ApexComputerUse

An AI computer use application for Windows desktop automation via **FlaUI UI Automation**. Controlled by AI agents through **HTTP REST**, **named pipes**, and **Telegram**. Supports local point-and-click usage. Includes **Tesseract OCR** for reading text from UI elements and **multimodal AI** (vision + audio) via a local LLM.

---

## Features

- Find any window and element by name or AutomationId (exact or fuzzy match)
- Filter element search by ControlType
- Persistent, hash-based stable element and window IDs (survive app restarts)
- Execute all common UI actions: click, type, select, toggle, scroll, drag & drop, etc.
- OCR any UI element using Tesseract
- Multimodal AI: describe UI elements, analyse image/audio files using a local vision LLM
- Remote control via HTTP REST API (curl-friendly JSON)
- Remote control via named pipe (PowerShell module included)
- Remote control via cmd.exe batch helper (`apex.cmd`)
- Remote control via Telegram bot
- Screenshot capture of elements and screen

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

Use **Tools → Output UI Map** to capture the full element tree of the current window as nested JSON.

---

## Window and Element ID Mapping

Every window and element is assigned a **stable numeric ID** (SHA-256 hash-based) that persists across sessions. These IDs can be used in `find` commands instead of titles or AutomationIds.

```bash
# 1. Get windows with their IDs
curl http://localhost:8080/windows
# Returns: [{"id":42,"title":"Notepad"},{"id":107,"title":"Calculator"},...]

# 2. Get elements with their IDs for the current window
curl http://localhost:8080/elements
# Returns nested JSON: {"id":105,"controlType":"Edit","name":"Text Editor","automationId":"15","children":[...]}

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

With screen-capture-based AI automation, every interaction requires sending a fresh image to the model. At typical resolutions that's **2,000–30,000+ tokens per capture** — every single time, for every action. With ApexUIBridge's map approach, the UI is rendered once as a structured, text-based representation. After that initial render, each individual interaction references elements by name, costing **5–20 tokens on average** — comparable to the overhead of a single API tool call.

The initial map render is a one-time cost per session. Everything after it is nearly free by comparison.

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

Start the HTTP server from the **Remote Control** group box, then use curl:

```bash
# List all open windows (with stable IDs)
curl http://localhost:8080/windows

# Get current state
curl http://localhost:8080/status

# List all elements in the current window (nested JSON with IDs)
curl http://localhost:8080/elements
curl "http://localhost:8080/elements?type=Button"

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

# Describe the currently selected UI element (requires a prior /find)
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

### Response format

```json
{
  "success": true,
  "message": "Window: Notepad (exact) | Element (exact)",
  "data": "Name=Text Editor  ControlType=Edit  AutomationId=15 ..."
}
```

HTTP status: **200** on success, **400** on error. `data` may be `null` for void actions.

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
| `describe` | — (uses current element) | `prompt=<text>` | Capture the current UI element and ask the model to describe it |
| `ask` | `prompt=<text>` | — | Ask a specific question about the current UI element |
| `file` | `value=<file path>` | `prompt=<text>` | Send an image or audio file from disk to the model |

> **Note:** `describe`, `ask`, and `file` require a prior `find` command to select a window/element (for `describe` and `ask`). The model must be initialized with `init` before any inference call.

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
| `describe` | — | — | Return full element property description |
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

The AI command set is backed by `MtmdHelper` using LLamaSharp's multimodal (MTMD) API. Supports vision and audio modalities depending on the model.

Download a vision-capable GGUF model and its multimodal projector (e.g. LFM2-VL from LM Studio) and note the paths to both `.gguf` files. Then call `ai init` before any inference commands.

---

## Project Structure

```
ApexComputerUse/
├── Form1.cs / Form1.Designer.cs   — Main UI
├── FlaUIHelper.cs                 — All FlaUI automation wrappers
├── ElementIdGenerator.cs          — Stable SHA-256 hash-based element ID mapping
├── CommandProcessor.cs            — Shared remote command logic
├── HttpCommandServer.cs           — HTTP REST server (System.Net.HttpListener)
├── PipeCommandServer.cs           — Named-pipe server
├── TelegramController.cs          — Telegram bot (Telegram.Bot)
├── OcrHelper.cs                   — Tesseract OCR wrapper
├── MtmdHelper.cs                  — Multimodal LLM wrapper (LLamaSharp MTMD)
├── MtmdInteractiveModeExecute.cs  — Interactive AI computer use mode
├── tessdata/
│   └── eng.traineddata
└── Scripts/
    ├── ApexComputerUse.psm1       — PowerShell module (pipe-based)
    └── apex.cmd                   — cmd.exe helper (HTTP-based)
```
