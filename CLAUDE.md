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

The main project is split into responsibility-based subdirectories. `CommandProcessor`, `FlaUIHelper`, and `HttpCommandServer` are each split across multiple `partial class` files.

```
ApexComputerUse/
├── Program.cs / AssemblyAttributes.cs
├── appsettings.json                     — Deployment defaults (port, auth, file-IO whitelist)
├── ai-settings.json                     — AI Chat provider config (placeholder keys)
├── AI/
│   ├── AIDrawingCommand.cs              — GDI+ drawing engine; 9 shape types; screen overlay
│   ├── AiChatService.cs                 — Thread-safe service layer; provider config, session lifecycle, streaming
│   ├── MtmdHelper.cs                    — Stateless multimodal LLM wrapper (LLamaSharp MTMD)
│   ├── MtmdInteractiveModeExecute.cs    — Interactive AI computer use mode (Tools menu)
│   └── SceneChatAgent.cs                — Chat agent that drives scene composition
├── Automation/
│   ├── FlaUIHelper.cs                   — Partial class entry; common helpers
│   ├── FlaUIHelper.Windows.cs           — Window enumeration / activation
│   ├── FlaUIHelper.Controls.cs          — Combo/list/checkbox/button helpers
│   ├── FlaUIHelper.Keyboard.cs          — Key notation / SendKeys
│   ├── FlaUIHelper.Capture.cs           — Element + screen capture
│   ├── FlaUIHelper.ElementInfo.cs       — UIA property dumps
│   ├── FlaUIHelper.MouseScroll.cs       — Mouse / scroll
│   ├── FlaUIHelper.TextValue.cs         — TextPattern / ValuePattern reads + writes
│   ├── ElementIdGenerator.cs            — Stable SHA-256 hash-based element ID mapping
│   └── UiMapRenderer.cs                 — Element tree → colour-coded screen overlay and PNG
├── Clients/
│   ├── ClientPermissions.cs             — Per-client permission flags
│   ├── ClientStore.cs                   — Persisted client list
│   └── RemoteClient.cs                  — Client model
├── Commands/
│   ├── CommandProcessor.cs              — Entry, _stateLock, OnLog
│   ├── CommandProcessor.Find.cs
│   ├── CommandProcessor.Execute.cs
│   ├── CommandProcessor.Ai.cs           — AI inference; runs OUTSIDE _stateLock
│   ├── CommandProcessor.Capture.cs
│   ├── CommandProcessor.Help.cs
│   ├── CommandProcessor.Scenes.cs
│   ├── CommandProcessor.Windows.cs
│   ├── CommandDispatcher.cs
│   ├── CommandLineParser.cs
│   ├── CommandRequest.cs
│   └── CommandRequestJsonMapper.cs
├── Infrastructure/
│   ├── AppConfig.cs                     — Layered settings (appsettings.json + APEX_* env)
│   ├── AppSettings.cs                   — User preferences (%APPDATA%)
│   ├── AppLog.cs                        — Serilog wrapper
│   ├── ApexService.cs                   — Optional Windows service host
│   ├── DownloadManager.cs
│   ├── EventBroker.cs
│   └── OcrHelper.cs                     — Tesseract OCR wrapper
├── Scenes/
│   ├── Scene.cs                         — SceneShape / Layer / Scene data models with stable IDs
│   └── SceneStore.cs                    — Thread-safe in-memory + disk-persisted scene store
├── Servers/
│   ├── HttpCommandServer.cs             — Lifecycle, routing, auth
│   ├── HttpCommandServer.SystemRoutes.cs    — /ping /status /windows /find /exec /capture /ocr /run /file
│   ├── HttpCommandServer.ChatRoutes.cs      — /chat (WebView2 + AI session)
│   ├── HttpCommandServer.SceneRoutes.cs     — /scene CRUD
│   ├── HttpCommandServer.Events.cs          — SSE event stream
│   ├── HttpCommandServer.Parsing.cs         — Request body parsers
│   ├── HttpCommandServer.HelpPage.cs        — / (interactive HTML console)
│   ├── HttpCommandServer.SettingsPage.cs    — /settings HTML
│   ├── HttpCommandServer.EditorPage.cs      — Scene editor HTML
│   ├── HttpCommandServer.TestPage.cs        — Test harness HTML
│   ├── ApexResult.cs                    — Canonical {success, action, data, error}
│   ├── FormatAdapter.cs                 — Format negotiation: HTML / JSON / text / PDF
│   ├── JsonElementExtensions.cs
│   ├── PipeCommandServer.cs             — Named-pipe server
│   └── TelegramController.cs            — Telegram bot (Telegram.Bot)
└── UI/
    ├── Form1.cs / Form1.Designer.cs     — Main UI (tabs: Console, Find & Execute, Remote Control, Model, Chat, Clients)
    ├── ChatTabController.cs             — Chat tab; WebView2 navigates to /chat
    ├── ClientsTabController.cs          — Clients tab; permissions UI
    ├── ClientEditForm.cs / .Designer.cs — Per-client edit dialog
    ├── ModelTabController.cs            — Model tab logic
    ├── ServerTabController.cs           — Remote Control tab logic
    ├── ActionExecutor.cs                — Synchronous → UI-thread bridge
    ├── StatusMonitor.cs                 — Live status panel
    └── SceneEditorForm.cs / .Designer.cs — WinForms layered scene editor

AIClients/
├── AiMessagingCore/                     — Provider-neutral .NET 10 library; 8 providers; streaming via events
└── AIClients/                           — Standalone WinForms chat harness

Scripts/                                 — (repo root, not under ApexComputerUse/)
├── ApexComputerUse.psm1                 — PowerShell module (pipe-based control)
└── apex.cmd                             — cmd.exe helper (HTTP-based control)
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

Key settings (full list in `Infrastructure/AppConfig.cs`):

| Key | Default | Description |
|---|---|---|
| `HttpPort` | `8080` | HTTP listen port |
| `HttpBindAll` | `false` | `true` binds all interfaces (network access). Refuses to start if no API key. |
| `HttpAutoStart` | `true` (in shipped `appsettings.json`) | Server starts automatically on launch |
| `PipeName` | `ApexComputerUse` | Named pipe name |
| `EnableShellRun` | `false` | Enables `/run` shell execution endpoint |
| `EnableFileIo` | `false` | Enables `/file` read endpoint |
| `FileIoAllowedRoots` | `[]` | Whitelist of directories `/file` may resolve within. Empty = fail-closed. |
| `LogLevel` | `Information` | Serilog minimum level |
| `ApiKey` | auto-generated | HTTP auth key |
| `TelegramToken` | `""` | Telegram bot token |
| `TestRunnerExePath` / `TestRunnerConfigPath` | `""` | Optional paths used by the integration test runner |

`APEX_*` env-var equivalents (all overrideable): `APEX_HTTP_PORT`, `APEX_HTTP_BIND_ALL`, `APEX_HTTP_AUTOSTART`, `APEX_PIPE_NAME`, `APEX_LOG_LEVEL`, `APEX_ENABLE_SHELL_RUN`, `APEX_ENABLE_FILE_IO`, `APEX_FILE_IO_ALLOWED_ROOTS`, `APEX_API_KEY`, `APEX_ALLOWED_CHAT_IDS`, `APEX_TELEGRAM_TOKEN`, `APEX_MODEL_PATH`, `APEX_MMPROJ_PATH`, plus tuning knobs `APEX_WAITFOR_TIMEOUT_MS`, `APEX_WAITPAGE_TIMEOUT_MS`, `APEX_RETRY_ATTEMPTS`, `APEX_SCAN_CHILD_TIMEOUT_MS`, `APEX_FOREGROUND_SETTLE_MS`.

Logs: `%LOCALAPPDATA%\ApexComputerUse\Logs\apex-YYYYMMDD.log` — daily rotation, 7-day retention.

---

## Key Subsystems

### FlaUIHelper
All Windows UIA3 interaction. Split into `FlaUIHelper.{Windows,Controls,Keyboard,Capture,ElementInfo,MouseScroll,TextValue}.cs` partials. If automation behaviour is wrong, start here. Does not own the current-element pointer — that lives in `CommandProcessor`.

### CommandProcessor
Split into `CommandProcessor.{Find,Execute,Ai,Capture,Help,Scenes,Windows}.cs` partials, plus the entry file `CommandProcessor.cs` which owns `_stateLock`. Receives a parsed command dict, dispatches to the right FlaUIHelper method, returns an `ApexResult`. All three server types call the same methods here. Changes to command behaviour belong here, not in the individual server files. AI inference (`ai/describe|ask|file|init`) runs **outside** `_stateLock` so a 30-second model call doesn't block other commands.

### HttpCommandServer
Handles HTTP lifecycle only — routing, auth (API key check), format negotiation, serialisation. Does not contain business logic. Split into `HttpCommandServer.{SystemRoutes,ChatRoutes,SceneRoutes,Events,Parsing,HelpPage,SettingsPage,EditorPage,TestPage}.cs`. `FormatAdapter` handles HTML/JSON/text/PDF; `PdfWriter` (inside `HttpCommandServer.cs`) has no external dependencies.

### Chat tab (`UI/ChatTabController.cs`)
Owns the WebView2 in the **Chat** tab; navigates to `/chat`. Auto-starts the HTTP server, model, and netsh URL ACL on first run.

### Clients tab (`UI/ClientsTabController.cs`)
UI for managing per-client permissions stored under `Clients/`. Backed by `ClientStore` and `ClientPermissions`.

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
| `ElementIdGeneratorTests.cs` | Hash mode, incremental mode, reset, thread safety |
| `SceneStoreTests.cs` | CRUD, disk persistence, concurrent creates |
| `SceneModelTests.cs` | `FlattenForRender`, ZIndex ordering, opacity, `SceneIds` |
| `SceneChatAgentConcurrencyTests.cs` | Concurrent scene-chat agent operations |
| `AIDrawingCommandTests.cs` | JSON parsing, canvas backgrounds, shape types |
| `AiChatServiceTests.cs` | Provider config, session lifecycle |
| `CommandProcessorTreeTests.cs` | Find / element-tree mapping behaviour |
| `CommandResponseTests.cs` | `ToText` / `ToJson` serialisation |
| `FuzzyMatchPolicyTests.cs` | Fuzzy match scoring policy |
| `HttpAuthorizationTests.cs` | API-key auth on HTTP endpoints |
| `HttpRunParserTests.cs` | `/run` request parsing |
| `KeyNotationNormalizationTests.cs` | Key notation normalisation (`{ENTER}`, `Ctrl+A`, etc.) |
| `LevenshteinTests.cs` | Edit-distance boundary and domain cases |
| `NewFeaturesTests.cs` | Recently added features |
| `OcrHelperTests.cs` | `CropBitmap` region logic, `OcrResult.ToString` |
| `PipeCommandServerTests.cs` | Named-pipe JSON protocol parser |
| `TelegramParseCommandTests.cs` | Command + key-value parser, `DictExtensions.Get` |

Components requiring a live Windows session (FlaUI UIA, Tesseract, LLamaSharp, WinForms UI) are not covered by unit tests — verify those manually or via the integration test runner.

---

## Verifying Behaviour with curl

The HTTP server starts automatically on launch (`HttpAutoStart = true`). Include the API key from the Remote Control tab on every request.

```bash
# Confirm server is up
curl -H "X-Api-Key: <key>" http://localhost:8080/ping

# Check current element state
curl -H "X-Api-Key: <key>" http://localhost:8080/status

# List open windows (confirms FlaUI enumeration is working)
curl -H "X-Api-Key: <key>" http://localhost:8080/windows

# Get element tree for the current window
curl -H "X-Api-Key: <key>" "http://localhost:8080/elements?onscreen=true"

# Find a window and element (sets the current element pointer)
curl -H "X-Api-Key: <key>" -X POST http://localhost:8080/find \
  -H "Content-Type: application/json" \
  -d '{"window":"Notepad","name":"Text Editor","type":"Edit"}'

# Execute an action on the current element
curl -H "X-Api-Key: <key>" -X POST http://localhost:8080/exec \
  -H "Content-Type: application/json" \
  -d '{"action":"gettext"}'

# Get raw JSON response (skip HTML wrapper)
curl -H "X-Api-Key: <key>" http://localhost:8080/status.json
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
curl -H "X-Api-Key: <key>" -X POST http://localhost:8080/capture

# Full screen
curl -H "X-Api-Key: <key>" -X POST http://localhost:8080/capture \
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
