## Surgical Code Modification Protocol

When working in this codebase, optimize for **minimum token usage and minimal file reads**.

### File Reading Rules

* **Do not open entire files by default.**
* First use search tools (`grep`, symbol search, references, definitions) to locate only the relevant functions, classes, or blocks.
* Read **only the smallest necessary section** surrounding the target code.
* Expand to larger sections **only if dependencies or surrounding logic require it**.
* Never re-read the same file region unless necessary.
* Prefer reading by **method/function boundary** rather than by line count.
* Read no more than **100 lines at a time** unless explicitly required for dependency resolution.

### Dependency Tracing Rules

Before making edits:

1. Identify the exact symbol(s) to modify.
2. Trace where they are called or referenced.
3. Determine whether the requested change is:

   * local to one function
   * local to one module
   * cross-module
4. Only inspect files directly involved in the execution path.

### Editing Rules

* Make **surgical edits only**.
* Modify only code directly involved in implementing the requested behavior.
* Change the **smallest viable code region**.
* Do not refactor unrelated code.
* Do not rewrite entire functions if only a few lines need changing.
* Preserve surrounding formatting and patterns.

**Scope Control Constraints**

* Introduce only the **minimum number of new variables, methods, or helper classes** required.
* Do not alter unrelated systems.
* Do not introduce new abstractions unless absolutely necessary to complete the task.
* Do not redesign existing patterns or architecture.

### Escalation Rules

Only widen scope if:

* the implementation depends on upstream context
* the symbol behavior is inherited or abstracted elsewhere
* the change affects interfaces/shared types
* local modification would break existing logic

If scope widens:

1. state why
2. identify the additional file(s) needed
3. inspect only the relevant sections

### Hard Stop Rule

* If the requested change **cannot be completed within a clearly minimal and localized scope**, **stop immediately**.
* Do not proceed with broad refactors, speculative fixes, or architectural changes.
* Instead, return:

  1. why the task exceeds surgical scope
  2. what additional areas would need modification
  3. a concise set of options for how to proceed

### Output Rules

Before editing, provide:

1. target symbol/file
2. expected scope of change
3. why that scope is sufficient

After editing, provide:

1. exact files changed
2. exact symbols changed
3. any downstream areas that may be affected

### Token Efficiency Priority

Prioritize:

1. symbol search
2. targeted reads
3. localized edits
4. minimal verification reads

Avoid:

* full-file reads
* repository-wide scans unless necessary
* unnecessary context loading
* speculative inspection of unrelated modules

Treat every additional file read as a cost that must be justified.

# ApexComputerUse — Developer Reference

## Platform & Build

- **Runtime:** .NET 10, Windows only
- **UI framework:** WinForms
- **Windows Desktop workload required:** `dotnet workload install windows`

```powershell
# Build
dotnet build -c Release ApexComputerUse/ApexComputerUse.csproj

# Run from source
dotnet run --project ApexComputerUse/ApexComputerUse.csproj

# Unit tests
dotnet test ApexComputerUse.Tests/ApexComputerUse.Tests.csproj
```

---

## Project Structure

```
ApexComputerUse/
├── Form1.cs / Form1.Designer.cs         — Main UI (tabs: Console, Find & Execute, Remote Control, Model)
├── AiChatForm.cs / AiChatForm.Designer.cs — AI Chat window (Tools → AI Chat)
├── AiChatService.cs                     — Thread-safe service layer; provider config, session lifecycle, streaming
├── FlaUIHelper.cs                       — All FlaUI/UIA3 automation wrappers
├── ElementIdGenerator.cs                — Stable SHA-256 hash-based element ID mapping
├── CommandProcessor.cs                  — Shared command logic (HTTP, pipe, and Telegram all call this)
├── HttpCommandServer.cs                 — HTTP REST server (System.Net.HttpListener)
│     ├── ApexResult                     — Canonical response type: {success, action, data, error}
│     ├── FormatAdapter                  — Format negotiation: HTML / JSON / text / PDF
│     └── PdfWriter                      — Minimal PDF generator (no external dependencies)
├── PipeCommandServer.cs                 — Named-pipe server
├── TelegramController.cs                — Telegram bot (Telegram.Bot)
├── OcrHelper.cs                         — Tesseract OCR wrapper
├── MtmdHelper.cs                        — Stateless multimodal LLM wrapper (LLamaSharp MTMD)
├── MtmdInteractiveModeExecute.cs        — Interactive AI computer use mode (Tools menu)
├── UiMapRenderer.cs                     — Element tree → colour-coded screen overlay and PNG
├── AIDrawingCommand.cs                  — GDI+ drawing engine; 9 shape types; screen overlay
├── Scene.cs                             — SceneShape / Layer / Scene data models with stable IDs
├── SceneStore.cs                        — Thread-safe in-memory + disk-persisted scene store
├── SceneEditorForm.cs / .Designer.cs    — WinForms layered scene editor
├── ai-settings.json                     — AI Chat provider config (placeholder keys)
└── Scripts/
    ├── ApexComputerUse.psm1             — PowerShell module (pipe-based control)
    └── apex.cmd                         — cmd.exe helper (HTTP-based control)

AIClients/
├── AiMessagingCore/                     — Provider-neutral .NET 10 library; 8 providers; streaming via events
└── AIClients/                           — Standalone WinForms chat harness
```

---

## Architecture

### Command flow

All control surfaces (HTTP, named pipe, Telegram) parse their input format and delegate to `CommandProcessor`. This is the single authoritative place for command logic — if you're changing what a command does, it's almost always in `CommandProcessor`.

```
HTTP request   → HttpCommandServer  ┐
Named pipe msg → PipeCommandServer  ├→ CommandProcessor → FlaUIHelper → Windows UIA
Telegram msg   → TelegramController ┘
```

### Response contract

Every endpoint returns `ApexResult` (defined in `HttpCommandServer.cs`):

```json
{
  "success": true,
  "action":  "find",
  "data":    { "result": "...", "message": "..." },
  "error":   null
}
```

HTTP 200 on success, 400 on error. Never change this shape — all control surfaces and the test suite depend on it.

### Element ID stability

`ElementIdGenerator` assigns a SHA-256 hash-based numeric ID to every element, derived from control type, name, AutomationId, and tree position. These IDs are stable across sessions. Any change to the hash inputs breaks ID stability for existing callers — treat the hashing logic as a contract.

### State model

The server holds a single **current element** pointer. `/find` moves it; `/exec` acts on it. This is intentional and central to the design — do not introduce per-request element resolution without understanding the implications for all callers.

### Configuration layering

Settings resolve in this order (last wins):
1. `appsettings.json` (next to executable)
2. `APEX_*` environment variables
3. `%APPDATA%\ApexComputerUse\settings.json` (GUI state)

Key settings:

| Key | Default | Description |
|---|---|---|
| `HttpPort` | `8081` | HTTP listen port |
| `HttpBindAll` | `false` | `true` binds all interfaces |
| `PipeName` | `ApexComputerUse` | Named pipe name |
| `EnableShellRun` | `false` | Enables `/run` shell execution endpoint |
| `LogLevel` | `Information` | Serilog minimum level |
| `ApiKey` | auto-generated | HTTP auth key |

Logs: `<exe>/logs/apex-YYYYMMDD.log` — daily rotation, 7-day retention.

---

## Key Subsystems

### FlaUIHelper
All Windows UIA3 interaction. Find windows, find elements, execute actions. If automation behaviour is wrong, start here. Does not own the current-element pointer — that lives in `CommandProcessor`.

### CommandProcessor
Receives a parsed command dict, dispatches to the right FlaUIHelper method, returns an `ApexResult`. All three server types call the same methods here. Changes to command behaviour belong here, not in the individual server files.

### HttpCommandServer
Handles HTTP lifecycle only — routing, auth (API key check), format negotiation, serialisation. Does not contain business logic. `FormatAdapter` handles the HTML/JSON/text/PDF response variants. `PdfWriter` has no external dependencies.

### ElementIdGenerator
Two modes: hash (stable across sessions, derived from element properties) and incremental (monotonic, for cases where hash collisions are a concern). Hash mode is the default and what callers depend on.

### SceneStore
Thread-safe. Persists scenes to `<exe>/scenes/{id}.json`. In-memory map is the source of truth during a session; disk is loaded on startup. Concurrent creates are safe; last-write-wins on concurrent patches.

### MtmdHelper
Fully stateless — no chat history between calls. Must be initialised with `ai/init` before any inference. Requires both a `.gguf` model file and a multimodal projector file.

---

## Unit Test Coverage

```powershell
dotnet test ApexComputerUse.Tests/ApexComputerUse.Tests.csproj

# Run a specific class
dotnet test --filter "ElementIdGeneratorTests"
```

| Test file | What it covers |
|---|---|
| `ElementIdGeneratorTests.cs` | Hash mode, incremental mode, reset, thread safety (50 concurrent threads) |
| `SceneStoreTests.cs` | CRUD, disk persistence, concurrent creates |
| `SceneModelTests.cs` | `FlattenForRender`, ZIndex ordering, opacity, `SceneIds` |
| `AIDrawingCommandTests.cs` | JSON parsing, canvas backgrounds, all 8 shape types |
| `TelegramParseCommandTests.cs` | Command + key-value parser, `DictExtensions.Get` |
| `PipeCommandServerTests.cs` | Named-pipe JSON protocol parser |
| `LevenshteinTests.cs` | Edit-distance boundary and domain cases |
| `CommandResponseTests.cs` | `ToText` / `ToJson` serialisation |
| `OcrHelperTests.cs` | `CropBitmap` region logic, `OcrResult.ToString` |

Components requiring a live Windows session (FlaUI UIA, Tesseract, LLamaSharp, WinForms UI) are not covered by unit tests — verify those manually or via the integration test runner.

---

## Verifying Behaviour with curl

The HTTP server must be running before any curl verification. Start it from the Remote Control tab or via `dotnet run`. Include the API key from the Remote Control tab on every request.

```bash
# Confirm server is up
curl -H "X-Api-Key: <key>" http://localhost:8081/ping

# Check current element state
curl -H "X-Api-Key: <key>" http://localhost:8081/status

# List open windows (confirms FlaUI enumeration is working)
curl -H "X-Api-Key: <key>" http://localhost:8081/windows

# Get element tree for the current window
curl -H "X-Api-Key: <key>" "http://localhost:8081/elements?onscreen=true"

# Find a window and element (sets the current element pointer)
curl -H "X-Api-Key: <key>" -X POST http://localhost:8081/find \
  -H "Content-Type: application/json" \
  -d '{"window":"Notepad","name":"Text Editor","type":"Edit"}'

# Execute an action on the current element
curl -H "X-Api-Key: <key>" -X POST http://localhost:8081/exec \
  -H "Content-Type: application/json" \
  -d '{"action":"gettext"}'

# Get raw JSON response (skip HTML wrapper)
curl -H "X-Api-Key: <key>" http://localhost:8081/status.json
```

**Find fields:** `window` (title or numeric ID), `name` (element Name), `id` (AutomationId or numeric ID), `type` (ControlType: Button, Edit, CheckBox, etc.)

**Useful exec actions for verification:**

| Action | Value | Purpose |
|---|---|---|
| `gettext` | — | Read current element value |
| `click` | — | Smart click (Invoke → Toggle → SelectionItem → mouse) |
| `type` | text | Type text into element |
| `keys` | e.g. `{ENTER}`, `Ctrl+A` | Send keystrokes |
| `highlight` | — | Orange border for 1 second — visual confirm correct element |
| `describe` | — | Dump full UIA property set |
| `isenabled` | — | Returns `True` or `False` |
| `isvisible` | — | Returns `True` or `False` |

**Capture for visual verification:**
```bash
# Current element — returns base64 PNG in data.result
curl -H "X-Api-Key: <key>" -X POST http://localhost:8081/capture

# Full screen
curl -H "X-Api-Key: <key>" -X POST http://localhost:8081/capture \
  -H "Content-Type: application/json" \
  -d '{"action":"screen"}'
```

---

## Integration Test Runner

A separate cycle-based orchestrator in `TestApplications/TestRunner/` launches the WinForms, WPF, and web test apps, runs the full suite against the live HTTP API, and reports results. Use this when verifying changes that touch `CommandProcessor`, `FlaUIHelper`, or `HttpCommandServer`.

```bash
# Demo mode — human-readable output, 3 cycles
dotnet run --project TestApplications/TestRunner -- --mode demo

# Benchmark mode — JSON-line output, 25 cycles
dotnet run --project TestApplications/TestRunner -- --mode benchmark
```

Test apps:
- **WinForms:** `TortureTestForm.cs` — textbox, button, checkbox, radio, combo, listbox, slider, menu, grid
- **WPF:** `TortureTestWindow.xaml` — same controls plus Expander, ViewModel-driven state
- **Web:** `index.html` — menu, tabs, form controls, scrollable regions

The runner interacts exclusively through the HTTP API. If a test fails, the reported command is the exact curl call that failed.
