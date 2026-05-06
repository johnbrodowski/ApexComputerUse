# Changelog

All notable changes to ApexComputerUse are documented in this file.

## [0.15.0] — 2026-05-06

### Added

#### Element Annotations & Filtering
- New `ElementAnnotation` model and `ElementAnnotationStore` — per-element notes and exclusion flags keyed by stable element hash, persisted at `%LOCALAPPDATA%\ApexComputerUse\annotations\elements.json`. Empty records auto-GC'd.
- New verbs in `CommandProcessor.Annotations.cs`: `annotate`, `unannotate`, `exclude`, `unexclude`, `annotations`, `excluded`
- New HTTP routes: `POST /annotate`, `POST /unannotate`, `POST /exclude`, `POST /unexclude`, `GET /annotations`, `GET /excluded`
- Notes appear as a `note` field on `/elements` output; excluded subtrees are skipped during scan (root never excluded — `depth > 0` guard)
- New query param `?unfiltered=true` on `/elements` bypasses the exclusion filter
- 7 new unit tests in `ElementAnnotationStoreTests.cs`

#### Region Maps
- New `RegionMap` model and `RegionMapStore` — persistent named pixel-coordinate grids tied to a window or stable element hash. One file per map under `<exe>/regionmaps/{id}.json`
- Built for AI self-calibration loops on canvas-rendered content (board games, emulators, video timelines) where individual cells are not UIA elements
- Static helpers: `CellToPixel(map, row, col)` returns cell center; `BuildGridDrawRequest(...)` produces a re-usable draw request for both overlay and render paths
- New verbs in `CommandProcessor.RegionMaps.cs`: `regionmap` umbrella with sub-actions `list|get|delete|overlay|render|cell`
- New HTTP routes in `HttpCommandServer.AnnotationRoutes.cs`:
  - `GET|POST /regionmap` — list/create
  - `GET|PUT|PATCH|DELETE /regionmap/{id}` — per-map ops
  - `POST /regionmap/{id}/overlay` — click-through screen overlay
  - `POST /regionmap/{id}/render` — base64 PNG of screen (or current window) with grid drawn over it; supports `{"canvas":"screen"}` (default) or `{"canvas":"window"}` (auto-translates grid coords to window-local)
  - `POST /regionmap/{id}/cell` — `{row, col}` → `{x, y}` for `click-at`
- 10 new unit tests in `RegionMapStoreTests.cs` (incl. corner-case cell-coord math)

#### Region Monitors
- New `RegionMonitor` model and `RegionMonitorStore` — persistent per-region screen-change watchers, one file per monitor under `%LOCALAPPDATA%\ApexComputerUse\monitors\{id}.json`. Each monitor holds an array of `MonitorRegion` so one logical "watch" can cover multiple indicators (LEDs, status icons, etc.) with independent diffs.
- New `RegionMonitorRunner` — background dispatcher; one Task per enabled monitor; per-region capture → diff vs previous → fire SSE event when over threshold. First tick is the baseline (no fire). Disabled monitors are not polled. Region-count changes handled at runtime. Diff via `LockBits + Marshal.Copy` — per-pixel max-channel-difference > tolerance counts as "changed".
- New verbs in `CommandProcessor.Monitors.cs`: `monitor` umbrella with sub-actions `list|get|delete|start|stop|check`.
- New HTTP routes in `HttpCommandServer.MonitorRoutes.cs`:
  - `GET|POST /monitor` — list/create
  - `GET|PUT|DELETE /monitor/{id}` — per-monitor CRUD
  - `POST /monitor/{id}/start` / `/stop` — toggle enabled
  - `POST /monitor/{id}/check` — manual one-shot diff vs current baselines
  - `POST /monitor/{id}/snapshot?index=N` — base64 PNG of region N right now
- Notifications via the existing `/events` SSE stream as `monitor.fired` events: `{monitorId, name, regionIndex, label, x, y, width, height, percentDiff, threshold, seq, time}`.
- Defaults: `intervalMs=1000` (floor 100ms), `thresholdPct=5.0`, `tolerance=8`, `enabled=false`.
- Last-fire telemetry persisted on the monitor: `lastFiredUtc`, `lastPercentDiff`, `lastRegionIndex`, `hitCount`.
- 11 new unit tests in `RegionMonitorStoreTests.cs` covering CRUD, telemetry, persistence, and diff math.

#### EventBroker generalization
- `EventEnvelope` reshaped: `int? WindowId` plus `IReadOnlyDictionary<string, object?> Data` replace the fixed `WindowId`/`Title` fields. Window events still carry `id`/`title` inside `Data`; non-window subsystems attach arbitrary payloads.
- New public `EventBroker.Emit(string type, IDictionary<string, object?> data, int? windowId = null)` for non-window emitters (region monitors today, anything else later).
- SSE serializer in `HttpCommandServer.Events.cs` now flattens `Data` into the frame payload alongside `seq`/`time` — both event families render uniformly.
- `JsonElementExtensions.Dbl(name)` helper added for parsing `thresholdPct`.

#### Public /help Page
- New optional setting `PublicHelpPage` (default `false`): when on, `GET /help` is reachable without an API key
- New setting `PublicHelpRateLimit` (default `30` req/min/IP): sliding 60-second per-IP window protects the unauthenticated route
- Returns HTTP 429 with `Retry-After: 60` when limit exceeded
- Loopback callers and API-keyed callers always have full access (never rate-limited)
- New `RuntimeFlags` static — mutable mirror of `AppConfig` values seeded at startup, allows GUI changes to take effect without restart
- GUI controls in Remote Control tab: `chkPublicHelp` checkbox + `numHelpRateLimit` numeric input. Persisted in `%APPDATA%\ApexComputerUse\settings.json` alongside other user prefs.
- `appsettings.json` keys: `PublicHelpPage`, `PublicHelpRateLimit`. Env: `APEX_PUBLIC_HELP_PAGE`, `APEX_PUBLIC_HELP_RATE_LIMIT`.

### Changed

#### UI
- Remote Control tab cleaned up: `lblTelegramStatus` moved from (8, 168) — was overlapping the new public-help checkbox — to (465, 104) on the bot-token row. `btnStartTelegram` shrunk from 120 to 100 wide to make room.
- Added tooltips to all interactive controls in Remote Control tab (HTTP port/start, API key, Copy, bot token, Start Telegram, allowed chat IDs, public help, rate limit, pipe name, Start Pipe, status labels).

#### Documentation
- New `LICENSE`: PolyForm Noncommercial 1.0.0. Source-available; commercial use requires a separate license.
- New `THIRD_PARTY_NOTICES.md`: license attributions for all 9 NuGet dependencies (FlaUI, Serilog, LLamaSharp, Telegram.Bot, Tesseract, etc.). MIT and Apache 2.0 obligations met inline.
- Merged `ACU_AI_CONTROL_GUIDE.md` into `ACU_CONTROL_GUIDE.md` (deleted the former) — single comprehensive guide. Added "Rules of Thumb" section + Annotations + Region Maps coverage.
- Slimmed `ACU_SYSTEM_PROMPT.md` from 9 KB → 3.8 KB. Now points at the auto-generated `/help` page for endpoint reference instead of duplicating tables that drift. Retains auth, mental model, 10 critical rules, minimal control loop.
- Updated `ACU_OPERATIONAL_REFERENCE.md` for staleness: added `/winrun`, annotations, region maps, `note` field, `?unfiltered`, `PublicHelpPage`/`PublicHelpRateLimit` config keys.

### Fixed

- `CommandProcessor.ScanElementsIntoMap` now consults `ElementAnnotationStore` to skip excluded subtrees and attach notes during scan; existing `/elements` callers see no behavioral change unless annotations exist.
- `RegionMap.canvas:"window"` mode correctly translates screen-absolute grid coords into window-local space before drawing, so the grid lines up with the captured window image.

---

## [0.14.0] — 2026-04-27

### Added

#### Multiple Instance Support
- Added `--port` command-line argument to override HTTP listen port for running multiple instances
- Added `--pipe` command-line argument to override named-pipe name
- Added `--client` command-line argument to mark an instance as a subordinate client (disables Launch Instance button)
- Port auto-increment in `HttpCommandServer.Start()` — automatically tries next available port if preferred port is taken
- New buttons in Clients tab: "Open Web UI" (launches `/chat` page in default browser) and "Launch Instance" (spawns new instance with incremented port)
- `ClientsTabController.LaunchInstance()` auto-registers spawned instance in client list

#### Client Permissions System
- New `ClientPermissions` class with per-client flags: `AllowAutomation`, `AllowCapture`, `AllowAi`, `AllowScenes`, `AllowShellRun`, `AllowClients`
- Permissions stored in JSON alongside each `RemoteClient` and loaded on reconnect
- Permission enforcement in `HttpCommandServer`: loopback (127.0.0.1) always gets full access; registered clients get their stored permissions; unknown IPs get full access
- All endpoints gated by appropriate permission: `/run` requires `AllowShellRun`, `/capture`/`ocr` require `AllowCapture`, `/ai`/`chat` require `AllowAi`, `/scenes`/`editor` require `AllowScenes`, `/clients` require `AllowClients`, everything else requires `AllowAutomation`
- `ClientEditForm` redesigned with two tabs: "Connection" (existing fields) and "Permissions" (6 checkboxes with ShellRun/Clients highlighted in orange)
- `ClientStore.FindByHost(string host)` — case-insensitive lookup by hostname

#### AI Chat with API Tools
- `AiChatService.SetLocalServer(int port, string? apiKey)` and `ClearLocalServer()` — configure local HTTP server context for AI chat
- Agentic tool loop in `AiChatService.SendAsync()` — AI can issue ApexComputerUse API calls via `apex` code blocks
- System prompt auto-extended with API reference when server context is set, including endpoint list and example calls
- Loop executes up to 8 turns, executing calls and feeding results back until AI produces clean answer
- `ServerTabController.ToggleHttp()` calls `SetLocalServer()` on start and `ClearLocalServer()` on stop
- Parsing and system prompt generation exposed as `internal` for testing

#### Security Hardening
- Timing-safe API key comparison using `CryptographicOperations.FixedTimeEquals()` (replaced three separate `==` comparisons)
- Shell command execution in `/run` now uses `ProcessStartInfo.ArgumentList` instead of string concatenation to prevent injection
- `HttpCommandServer.Stop()` now explicitly closes `HttpListener` to immediately release port handles

#### Bug Fixes
- Fixed `MtmdInteractiveModeExecute` infinite loop with hardcoded test path — replaced with proper `Console.ReadLine()` loop
- Fixed `CommandProcessor` element ID lookup to use `Equals()` instead of `ReferenceEquals()` (FlaUI uses `IUIAutomation.CompareElements`)
- Added 50k-entry cap on `CommandProcessor._elementMap` to prevent unbounded growth during long sessions
- Fixed `Form1.SetupNetshIfNeeded()` blocking UI thread — made async with proper timeout
- Fixed `Form1.AutoLoadModelIfConfigured()` fire-and-forget — now logs async exceptions via `.ContinueWith()`
- `SceneEditorForm` canvas paint optimization — eliminated per-paint full-scene bitmap allocation during drag

### Changed

#### Program Structure
- `Program.IsClientInstance` — public static property detecting `--client` flag for UI gating
- Command-line arg parsing restructured to support flag-only arguments alongside key-value pairs

#### API & Configuration
- `HttpCommandServer` constructor now accepts optional `ClientStore? clientStore` parameter
- `HttpCommandServer.Port { get; private set; }` — made settable internally by `Start()` for auto-increment
- `RemoteClient.Permissions` — new property with `ClientPermissions` value

#### UI
- `Form1.Designer.cs` — added "Open Web UI" and "Launch Instance" buttons to Clients tab
- `ClientEditForm.Designer.cs` — complete redesign with TabControl (Connection / Permissions tabs)
- `ClientsTabController` constructor signature expanded with button references and port getter

### Testing
- New test file `ApexComputerUse.Tests/AiChatServiceTests.cs` with 22 tests covering apex call parsing and system prompt generation
- `ParseApexCalls` and `BuildApexSystemPrompt` exposed as `internal` via existing `InternalsVisibleTo` attribute
- All 171 tests passing (149 existing + 22 new)

### Known Limitations
- AI tool-use loop is non-streaming (full response assembled before delivery)
- IP-spoofing could bypass permission sandboxing on local network

---

## [0.13.0] — 2026-04-26

### Added
- **Clients tab — remote machine registry** — a new "Clients" tab (sixth tab in the main UI) lets users and AI maintain a persistent directory of other Apex-enabled machines. Each entry stores a friendly name, host/IP, port, API key, OS version, and description. Entries are listed in a six-column `ListView` and persisted to `<exe>/clients/{id}.json` using the same thread-safe JSON store pattern as scenes.
- **`ClientStore`** (`Clients/ClientStore.cs`) — thread-safe store that loads all client records from disk on startup and writes individual JSON files on every create, update, or delete.
- **`RemoteClient`** (`Clients/RemoteClient.cs`) — data model with `[JsonPropertyName]` attributes matching the project's snake_case serialization convention.
- **`ClientsTabController`** (`UI/ClientsTabController.cs`) — tab logic wired to Add, Edit, Remove, and Test buttons. Test Connection fires an async `GET /ping` against the selected client's host:port (with its API key if set) and updates a live Status column green/red in-place, with no UI blocking.
- **`ClientEditForm`** (`UI/ClientEditForm.cs` / `ClientEditForm.Designer.cs`) — fixed-size dialog for creating and editing client entries, with name/host required-field validation and port range validation.

---

## [0.12.0] — 2026-04-26

### Added
- **Embedded HTML chat in the Chat tab** — the Chat tab's RichTextBox, input field, and Send button have been replaced by an embedded `Microsoft.Web.WebView2` control hosting the existing `/chat` streaming page directly inside the app. Click **Load Chat** to navigate the WebView2 to `http://localhost:{port}/chat?apiKey=...`. The HTML page handles streaming, the "New chat" reset, and provider/model status display natively.
- **HTTP server auto-start on launch** — `HttpAutoStart` and `HttpBindAll` are now `true` by default in `appsettings.json`. The HTTP server starts and binds to all interfaces automatically when the app opens; no manual click on the Remote Control tab is required.
- **Model auto-load on launch** — if model and projector paths are saved in `settings.json`, the local vision model is loaded automatically at startup without opening the Model tab.
- **First-run netsh setup** — on the very first launch, the app checks whether the HTTP URL ACL (`http://+:8081/`) and the Windows Firewall inbound rule (`ApexComputerUse`) exist. If either is missing, a single elevated `cmd` session (one UAC prompt) runs both `netsh` commands. The result is persisted to `settings.json` (`NetshConfigured = true`) so the check never repeats.
- **Restart scripts** — `restart-apex.bat` and `restart-apex.ps1` at the repo root kill all running instances (`taskkill /F /IM ApexComputerUse.exe`) and relaunch the app. Both prefer the Release build, fall back to Debug, and fall back to `dotnet run` if no built exe is found.

### Changed
- `ChatTabController` — removed `_rtbChatHistory`, `_txtChatInput`, `_btnChatSend`, `AppendToChat`, `AppendColoredText`, `SendOrCancelAsync`, `ExecuteCommandsFromResponse`, and `CurlRx`. Constructor now accepts a `WebView2` instead. `OpenChat()` navigates the embedded WebView2; `ResetChat()` calls `Reload()`.
- `AppSettings` — added `NetshConfigured` bool field (persisted to `%APPDATA%\ApexComputerUse\settings.json`) for first-run netsh tracking.

---

## [0.11.0] — 2026-04-16

### Added
- **`/elements?match=<text>`** — case-insensitive substring search across `Name`, `AutomationId`, and `Value` pattern. Returns only branches containing matches, each wrapped in its ancestor path (non-matching siblings pruned). `depth` now controls how deep to render under each match, so one call replaces the repeated drill-down pattern of "fetch tree → spot candidate → fetch subtree". Composes with `type=` and `onscreen=true`.
- **`/elements?collapseChains=true`** — folds "1-in-1-in-1" wrapper chains that dominate web accessibility trees. A node is skipped only when it has exactly one child, no `Name`, no `AutomationId`, and its control type is `Pane`, `Group`, or `Custom`. Named containers and anything with an AutomationId are preserved. IDs of hoisted descendants are unchanged — follow-up `/elements?id=<id>` and `/execute id=<id>` calls continue to work against the real (unflattened) tree.
- **`/elements?includePath=true`** — every emitted node gains a `path` breadcrumb string (e.g. `"Chrome > Document > Main > Form"`) so an agent can orient itself without climbing back up the tree.
- **`/elements?properties=extra`** — opt-in per-node `value` (via Value pattern, when the element supports it) and `helpText` properties. Off by default so token budgets don't change silently; needed for web inputs whose `Name` is empty and whose visible content lives in the Value pattern.
- **`descendantCount` on truncated nodes** — nodes cut off by `depth` now emit `descendantCount: N` alongside the existing `childCount`, so an agent can decide whether a subtree is worth expanding without another round trip.
- **Structured `/find` response** — `/find` now populates a JSON `element` object on the response (id, controlType, name, automationId, className, frameworkId, isEnabled, isOffscreen, boundingRectangle, plus `value`/`helpText` when `properties=extra`) alongside the existing human-readable string in `message`. The element's numeric ID is recovered from the most recent `/elements` scan when available.
- **Tree-shape unit tests** (`ApexComputerUse.Tests/CommandProcessorTreeTests.cs`) — covers `FilterTreeByMatch` (case-insensitive, AutomationId + Value lookup, sibling pruning), `CollapseSingleChildChains` (identity-less-only collapse, multi-child preservation, ID stability), and `ElementNode` JSON round-trip for the new opt-in fields.

### Changed
- `CommandProcessor.ElementNode` / `BoundingRect` promoted from `private` to `internal sealed class` so the new in-process post-processors (`FilterTreeByMatch`, `CollapseSingleChildChains`) and the test project (`InternalsVisibleTo`) can exercise them directly.
- `ScanElementsIntoMap` now accepts a `ScanOptions` struct (IncludePath + IncludeExtra + depth) and threads the parent breadcrumb through recursion without changing call-site signatures for existing endpoints.

---

## [0.10.0] — 2026-04-16

### Added
- **AI Chat window** — Tools → AI Chat opens a standalone chat interface powered by the `AiMessagingCore` library. Supports 8 providers: OpenAI, Anthropic, DeepSeek, Grok, Groq, Duck, LM Studio, and LlamaSharp (local GGUF). Streams tokens in real-time; shows timing metrics (total tokens, tokens/second, time-to-first-token). Provider, model, system prompt, and sample query are persisted to `ai-settings.json` next to the executable.
- **`AIClients` solution integrated** — both `AiMessagingCore` (class library) and `AIClients` (standalone WinForms harness) are now included in `ApexComputerUse.sln` for single-solution editing. `AIClients.sln` and `AIClients.exe` remain fully independent and buildable on their own.
- **`ai-settings.json`** — starter settings file (copied to output on build) with placeholder API keys for all 8 providers. Replace placeholders with real keys to activate each provider.

### Fixed
- `ProviderSettings.ApiKey` and `AiLibrarySettings.DefaultProvider` changed from `init`-only to `set` so runtime configuration updates (provider switch, API key override) can be applied without reconstructing the settings objects.
- `HandleChatStatus` in `HttpCommandServer` now returns `Dictionary<string, string>` matching the `ApexResult.Data` contract; `sessionActive` is serialized as `"True"` / `"False"`.

---

## [0.9.0] — 2026-04-07

### Added
- **`capture` command** — returns screen captures inline as base64 PNG in the `data` response field. No file is written to disk. Four targets via `action=`:
  - `screen` — full display
  - `window` — current window (requires prior `find`)
  - `element` (default) — current element (requires prior `find`)
  - `elements value=id1,id2,...` — multiple elements by numeric ID, stitched vertically into one image
- **HTTP:** `POST /capture`
- **Named pipe / PowerShell:** `command=capture`; new `Invoke-FlaUICapture` cmdlet in `ApexComputerUse.psm1`
- **cmd.exe:** `apex capture [action=...] [value=...]` in `apex.cmd`
- **Telegram:** `/capture` — response delivered as a photo message, not text

---

## [0.8.0] — 2026-04-07

### Added
- **Persistent element ID map** — `elements` command now recursively scans the UI tree using `ElementIdGenerator` (SHA-256 hash-based, deterministic across sessions). Each element receives a stable numeric ID that survives app restarts.
- **Nested JSON element map output** — `elements` returns the full window tree as indented, nested JSON (`id`, `controlType`, `name`, `automationId`, `children`), replacing the flat string list.
- **Window map with persistent IDs** — `windows` command now returns a JSON array of `{id, title}` pairs. IDs are hash-based and stable for the same window across sessions.
- **Map-based lookup in `find`** — pass a numeric ID from either `windows` or `elements` as the `window=` or `id=` parameter; the element is resolved directly from the in-memory map without a fuzzy search.
- **Auto-focus on every `find`** — the matched window is brought into foreground focus automatically; no separate `focus` action required.
- **"Output UI Map" menu item** — Tools menu item captures the UI tree of the currently selected window and prints the nested JSON to the log.
- **Full `ElementOperations` parity** — all UIA patterns now covered by both `ApexHelper` and `CommandProcessor`:

#### New exec actions
| Action | Description |
|---|---|
| `mouse-click` | Force mouse left-click (bypasses Invoke/Toggle/SelectionItem) |
| `middle-click` | Middle-mouse-button click |
| `click-at value=x,y` | Click at pixel offset from element top-left |
| `drag value=x,y` | Drag element to screen coordinates |
| `highlight` | Draw orange highlight around element for 1 second |
| `isenabled` | Returns `True`/`False` |
| `isvisible` | Returns `True`/`False` |
| `clearvalue` | Set value to empty string (Value pattern) |
| `appendvalue` | Append text to current value |
| `getselectedtext` | Selected text via Text pattern |
| `setrange value=n` | Set RangeValue pattern |
| `getrange` | Read current RangeValue |
| `rangeinfo` | Min / max / smallChange / largeChange |
| `toggle-on` / `toggle-off` | Set toggle to a specific state |
| `gettoggle` | Read current toggle state (On / Off / Indeterminate) |
| `expandstate` | Read ExpandCollapse state |
| `select-item` | Select via SelectionItem pattern |
| `addselect` | Add element to multi-selection |
| `removeselect` | Remove element from selection |
| `isselected` | Check SelectionItem selected state |
| `getselection` | Get selected items from a Selection container |
| `select-index value=n` | Select ComboBox / ListBox item by zero-based index |
| `getitems` | List all items in a ComboBox or ListBox |
| `getselecteditem` | Get currently selected item text |
| `minimize` / `maximize` / `restore` | Window visual state |
| `windowstate` | Read current window visual state |
| `move value=x,y` | Move element via Transform pattern |
| `resize value=w,h` | Resize element via Transform pattern |
| `scroll-left` / `scroll-right value=n` | Horizontal mouse scroll |
| `scrollpercent value=h,v` | Scroll to h%/v% via Scroll pattern |
| `getscrollinfo` | Scroll position and scrollable flags |
| `griditem value=row,col` | Get element at grid cell |
| `gridinfo` | Row and column counts |
| `griditeminfo` | Row / column / span for a GridItem element |

#### Upgraded exec actions
| Action | Change |
|---|---|
| `click` | Now smart: Invoke → Toggle → SelectionItem → mouse fallback |
| `gettext` | Smart chain: Text pattern → Value → LegacyIAccessible → Name |
| `getvalue` | Smart chain: Value → Text → LegacyIAccessible → Name |
| `setvalue` | Smart chain: Value (if writable) → RangeValue (if numeric) → keyboard |
| `select` | Tries SelectionItem on list child first, then FlaUI wrappers |
| `keys` | Full `{KEY}` token notation (`{CTRL}`, `{F5}`, …) and `Ctrl+A` / `Alt+F4` combo syntax |

---

## [0.7.0] — 2026-04-06

### Added
- **`windows` command** returns a JSON array of `{id, title}` for all open windows, enabling the AI to select precisely without relying on fuzzy matching.

---

## [0.6.0] — 2026-04-06

### Added
- **Named-pipe server** (`PipeCommandServer`) — exposes the full command set over a Windows named pipe (default name `ApexComputerUse`). Each client connection is session-based (state is preserved across commands on the same connection). Accepts and returns newline-delimited JSON.
- **Pipe server UI** — new row in the Remote Control group box: configurable pipe name, Start/Stop button, and live status label.
- **`Scripts\ApexComputerUse.psm1`** — PowerShell module providing idiomatic cmdlets over the named pipe:
  `Connect-FlaUI`, `Disconnect-FlaUI`, `Send-FlaUICommand`, `Get-FlaUIWindows`, `Get-FlaUIStatus`, `Get-FlaUIHelp`, `Get-FlaUIElements`, `Find-FlaUIElement`, `Invoke-FlaUIAction`, `Invoke-FlaUIOcr`, `Invoke-FlaUIAi`.
- **`Scripts\apex.cmd`** — cmd.exe batch helper wrapping the HTTP server with simpler positional syntax (e.g. `apex find Notepad`, `apex exec click`, `apex ai describe`). Requires curl (built-in Windows 10+).

---

## [0.5.0] — 2026-04-06

### Added
- **AI multimodal command set** (`MtmdHelper` integration) — expose the existing `MtmdHelper` class through all remote interfaces.
- `CommandRequest` extended with `ModelPath`, `MmProjPath`, and `Prompt` fields.
- **`ai` command** in `CommandProcessor` with five sub-actions:
  - `init`     — load the LLM and multimodal projector from disk (`model=` + `proj=` paths).
  - `status`   — report whether the model is loaded and which modalities it supports.
  - `describe` — capture the current UI element and ask the vision model to describe it (optional `prompt=`).
  - `file`     — send an image or audio file from disk to the model (`value=<path>`, optional `prompt=`).
  - `ask`      — ask an arbitrary question about the current UI element (`prompt=` required).
- **HTTP endpoints** for AI commands: `GET /ai/status`; `POST /ai/init`, `/ai/describe`, `/ai/file`, `/ai/ask`.
- **Telegram `/ai` command** — same sub-action set via `action=<sub>` key-value syntax.
- Updated `help` command output to list all `ai` sub-actions.

---

## [0.4.0] — 2026-04-06

### Added
- **HTTP REST server** (`HttpCommandServer`) — control the application via curl on a configurable port (default 8080). Endpoints: `GET /status`, `/windows`, `/elements`, `/help`; `POST /find`, `/execute`, `/ocr`.
- **Telegram bot** (`TelegramController`) — same command set over Telegram. Supports `/find`, `/exec`, `/ocr`, `/status`, `/windows`, `/elements`, `/help`. Key=value argument syntax with quoted multi-word values.
- **CommandProcessor** — shared command engine used by both remote interfaces. Auto-accepts fuzzy window/element matches (no UI prompts in remote mode). Fires `OnLog` events forwarded to the form's status box.
- **Remote Control** group box in the UI — start/stop HTTP server and Telegram bot with live status indicators.
- `FlaUIHelper.ListWindowTitles()` — returns titles of all open windows.
- `FlaUIHelper.ListElements(Window, ControlType?)` — lists all elements in a window with optional ControlType filter.
- `README.md` — full usage documentation including curl examples and Telegram command reference.
- `CHANGELOG.md` — this file.

---

## [0.3.0] — 2026-04-06

### Added
- **OCR** (`OcrHelper`) — captures any UI element and runs Tesseract OCR on it.
  - `OcrElement` — capture and recognise.
  - `OcrElementAndSave` — capture, save image to disk, then recognise (useful for debugging).
  - `OcrElementRegion` — OCR a sub-rectangle of the element.
  - `OcrFile` — OCR an existing image file.
- `tessdata\eng.traineddata` bundled in project and copied to output on build.
- OCR actions available in the **Any Element** action group in the UI.

---

## [0.2.0] — 2026-04-06

### Added
- **Fuzzy window matching** — tries exact match, then contains, then Levenshtein closest. Prompts for approval on non-exact matches.
- **Fuzzy element matching** — same three-tier logic, applied to AutomationId or Name.
- **Search Type combo** — filter element search by `ControlType`. `All` searches every type without restriction. `All` is never passed as a `ControlType` value to FlaUI.
- Levenshtein distance implementation in `FlaUIHelper`.
- `FlaUIHelper.FindWindowFuzzy` and `FlaUIHelper.FindElementFuzzy` returning match metadata (exact vs fuzzy, matched value).

### Changed
- Form height extended to accommodate the new Search Type row.

---

## [0.1.0] — 2026-04-06

### Added
- Initial AI computer use application (WinForms) targeting .NET 10.
- **FlaUIHelper** class wrapping FlaUI UIA3 for all common WPF/WinForms control interactions:
  - Button, TextBox, PasswordBox, Label, ComboBox, CheckBox, RadioButton, ListBox, ListView, DataGrid, TreeView, Menu/MenuItem, TabControl, Slider, ProgressBar, Hyperlink.
  - Mouse operations: click, right-click, double-click, hover, drag & drop, scroll.
  - Keyboard: type, send key, shortcuts (Ctrl+A/C/X/V/Z).
  - Text: select all, copy, cut, paste, undo, clear, insert at caret.
  - Value/RangeValue patterns, ExpandCollapse, ScrollItem, Transform.
  - Screenshots via `FlaUI.Core.Capturing`.
  - `Retry.WhileNull` for waiting on dynamic elements.
  - Window operations: move, resize, minimize, maximize, restore, close.
  - Focus: `SetFocus`, `GetFocusedElement`.
- **Form UI** with:
  - Window Name, AutomationId, Element Name fields.
  - Control Type picker (action groups) and Action picker.
  - Value/Index field for parameterised actions.
  - Find Element, Execute Action, Clear Log buttons.
  - Timestamped output log.
- **Designer-compatible** `Form1.Designer.cs` (standard generated format, no lambdas or helpers inside `InitializeComponent`).