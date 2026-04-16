# Changelog

All notable changes to ApexComputerUse are documented here.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

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
