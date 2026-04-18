# TestApplications

This folder contains a set of **intentionally UI-rich test targets** and **automation harnesses** used to validate ApexComputerUse/ApexUIBridge behavior against real desktop/web interaction patterns.

The contents are split into six main areas:

1. `WinFormsApplication` — primary Windows Forms UI target (simple form + large torture form).
2. `WpfApplication` — primary WPF UI target (simple window + large torture window).
3. `MenuTestApp` — focused WinForms menu/navigation test target.
4. `WebTestApp` — static HTML app that mirrors many control types for browser automation.
5. `ElementTester` — command-line UI element exerciser that scans windows and runs per-control tests.
6. `TestRunner` — orchestration app that repeatedly builds, launches, tests, reports, and tears down cycles.

---

## High-level architecture and intent

- **UI Targets** (`WinFormsApplication`, `WpfApplication`, `MenuTestApp`, `WebTestApp`) provide stable, known controls so automation code can test:
  - discovery (`LIST_WINDOWS`, `SCAN_WINDOW`)
  - interaction (`CLICK`, `TYPE`, `TOGGLE`, `SET_VALUE`, `EXPAND`, `COLLAPSE`, etc.)
  - state readback (`GET_TEXT`) and reversible actions.
- **Automation Clients** (`ElementTester`, `TestRunner`) treat these apps as black-box systems and interact only through the bridge API.
- **Torture forms/windows** are deliberately broad and dense (menus, tabs, lists, grids, expanders, dialogs, date/time controls, etc.) to stress different UI Automation patterns.

---

## Folder-by-folder map

## 1) `WinFormsApplication/`

### Purpose
A Windows Forms test target with:
- a classic sample form (`Form1`) and
- a very large dynamic test surface (`TortureTestForm`) containing many control categories.

`Program.cs` launches `TortureTestForm` by default, so automated runs go straight to the comprehensive surface. The simpler `Form1` still exists for compatibility/targeted checks.

### Key files

- `Program.cs`
  - Entry point (`[STAThread]`), initializes WinForms application config, runs `TortureTestForm`.
- `TortureTestForm.cs`
  - A large, programmatically-built UI.
  - Builds a menu strip, toolbar, status strip, and a multi-tab layout.
  - Tabs include identity, network, scheduler, layout, data, logs, display, dates, and dialogs/misc controls.
  - Useful for broad UIA coverage (buttons, checkboxes, radio buttons, combos, grids, list controls, trackbars, progress bars, rich text, date/time, masked inputs, dialogs, etc.).
- `Form1.cs` + `Form1.Designer.cs` + `Form1.resx`
  - Original smaller test form with basic controls and events (textbox/combo/list/grid/menu interactions).
  - Includes handler logic (e.g., selection change confirmation for specific item values).
- `Core/DataGridViewItem.cs`
  - POCO model for DataGrid binding in `Form1`.
- `TortureTestForm.resx`
  - Resource metadata for torture form.
- `App.config`
  - App configuration container.
- `WinFormsApplication.csproj`
  - `net10.0-windows`, `UseWindowsForms=true`.
- `WinFormsApplication.sln`
  - Local solution wrapper for this test app.
- `Properties/*`
  - Auto-generated settings/resources scaffolding.
- `WinFormsApp.ico`
  - App icon.

### Why it exists
- Gives deterministic WinForms controls with known labels/automation IDs and complex container patterns.
- Supports robust testing of WinForms-specific UIA behavior and pattern availability.

---

## 2) `WpfApplication/`

### Purpose
WPF counterpart to the WinForms target.
Contains both:
- a baseline WPF window (`MainWindow`) and
- a feature-rich WPF torture window (`TortureTestWindow`) that heavily uses bindings and nested controls.

### Key files

- `App.xaml` + `App.xaml.cs`
  - WPF application bootstrap.
- `MainWindow.xaml` + `MainWindow.xaml.cs`
  - Baseline WPF target window (`ApexUIBridge Test Application - WPF`).
  - Includes tabs with simple and complex controls (textbox/password/combobox/listbox/check/radio/progress/slider/tree/listview/datagrid/calendar/datepicker etc.).
  - Includes menu actions like opening the torture test window and a command-bound invokable button path.
- `MainViewModel.cs`
  - View model for baseline window.
  - Provides `DataGridItems`, command-backed button text mutation (`InvokeButtonCommand` changes text to `Invoked!`).
- `TortureTestWindow.xaml` + `TortureTestWindow.xaml.cs`
  - Very large WPF test surface with AutomationIds on many elements.
  - Includes menu, toolbar, status bar, and a main tab control with dense content.
  - Event handlers cover key interactions (increment/decrement field logic, clear log/ink, menu exit, open/save dialogs, folder chooser trick, WinForms color/font dialogs hosted from WPF, message boxes).
- `TortureTestViewModel.cs`
  - Backing VM for torture window.
  - Seeds realistic sample data for jobs/services tables and live-like state fields.
  - Includes `JobItem` and `ServiceItem` models.
- `Infrastructure/ObservableObject.cs`
  - MVVM base class with `INotifyPropertyChanged` helpers and internal backing-value dictionary.
- `Infrastructure/RelayCommand.cs`
  - `ICommand` implementation used by view models.
- `DataGridItem.cs`, `ListViewItem.cs`
  - Small model classes for bound items.
- `WpfApplication.csproj`
  - Legacy-style .NET Framework WPF project (`TargetFrameworkVersion v4.8`).
- `App.config`, `Properties/*`, `WPFApp.ico`
  - Standard WPF app metadata/resources.

### Why it exists
- Exercises WPF-specific automation behavior, including data binding, command routing, tab virtualization/loading behavior, expanders, nested controls, and dialog interactions.

---

## 3) `MenuTestApp/`

### Purpose
A focused WinForms app dedicated to menu structure and menu event validation.

### Key files

- `Program.cs`
  - Standard WinForms entry point launching `Form1`.
- `Form1.Designer.cs`
  - Defines a deep menu hierarchy:
    - File (new/open/save/save as/exit)
    - Edit (copy/paste/find submenus)
    - View (zoom submenu, status bar toggle)
    - Help (contents/about)
  - Also includes a status strip and main label area.
- `Form1.cs`
  - Minimal code-behind:
    - exit handler closes window
    - about handler shows info message box
- `MenuTestApp.csproj`
  - `net10.0-windows`, `UseWindowsForms=true`.

### Why it exists
- Provides a compact target for menu traversal and keyboard accelerator scenarios without the noise of larger forms.

---

## 4) `WebTestApp/`

### Purpose
A static HTML/CSS/JS test page mirroring many desktop controls and interaction patterns.

### Key files

- `index.html`
  - Contains:
    - menu bar with nested submenus
    - tabbed interface
    - simple controls (text/password/select/checkbox/radio/slider/progress)
    - listbox/tree/listview/data grid style structures
    - context menu and scrollable regions
  - Structured with semantic roles (`role=menu`, `role=tab`, `role=tree`, `role=grid`, etc.) to aid automated selection and accessibility-driven targeting.

### Why it exists
- Enables browser/UI automation parity testing for control concepts that also exist in desktop targets.

---

## 5) `ElementTester/`

### Purpose
CLI tool for **element-type driven testing** against running bridge + UI targets.

It can:
- list available test types,
- scan windows,
- run one element test type or all types,
- target WPF, WinForms, or both.

### Key files

- `Program.cs`
  - Command router and execution loop.
  - Modes: `list`, `scan`, specific element type, or `all`.
  - Resolves target windows by expected title patterns.
  - Uses `TestContext` to aggregate pass/fail output and return non-zero on failures.
- `ElementTests.cs`
  - Registry + implementation for per-control-type tests.
  - Includes handlers for types such as:
    - TextBox, CheckBox, RadioButton, Button, ComboBox
    - Slider/TrackBar, ProgressBar, TabItem, Expander, ToggleButton
    - TreeView, Menu, RichTextBox, PasswordBox, DatePicker, ScrollBar
    - ListBox, DataGrid, ListView
  - Uses conservative limits (`Take(n)`) and restore steps to keep test runs stable/readable.
  - Avoids destructive actions by filtering unsafe labels (e.g., exit/close/delete terms).
- `ScanHelper.cs`
  - Parses `SCAN_WINDOW` output lines into structured elements.
  - Extracts IDs, names, automation IDs, control-type hints.
  - Provides lookup helpers (`FindId`, `FindByType`).
- `BridgeClient.cs`
  - Thin HTTP client for `/command` and readiness checks.
- `TestContext.cs`
  - Unified test result recorder with colored console output and summary statistics.
- `ElementTester.csproj`
  - `net10.0-windows` console executable.

### Why it exists
- Fast, operator-friendly exploratory and regression testing for element interactions without running full cycle orchestration.

---

## 6) `TestRunner/`

### Purpose
End-to-end **build + launch + bridge readiness + test suite + reporting** orchestrator.
Designed for repeated cycles and long-running reliability checks.

### Key files

- `Program.cs`
  - Main orchestrator loop:
    1. load config (`runner-config.json`)
    2. support cancellation (Ctrl+C + stop-flag file)
    3. optionally skip previously-passed tests (`RunOnlyFailed`)
    4. launch WPF/WinForms targets once
    5. per cycle: build bridge, launch bridge, wait for API, run suite, persist results, teardown bridge
    6. periodic and final Telegram reporting
- `RunnerConfig.cs`
  - Config contract for cycle counts, paths, API settings, build config, Telegram credentials, result persistence, stop-flag coordination.
- `TestSuite.cs`
  - Black-box functional suite that discovers dynamic element IDs from scans and executes commands/assertions.
  - Includes:
    - baseline tests for both main app windows
    - specialized WinForms torture and WPF torture test flows
    - helper methods for repeated toggle/type/click patterns
    - result model types (`TestResult`, `CycleResult`)
- `BuildRunner.cs`
  - Async `dotnet build` wrapper with captured stdout/stderr.
- `ProcessManager.cs`
  - Starts/stops external processes (GUI and non-GUI), including async disposal.
- `BridgeClient.cs`
  - HTTP command sender + readiness polling.
- `TelegramNotifier.cs`
  - Telegram Bot API notifications with safe fallback/logging behavior.
- `TestRunner.csproj`
  - `net10.0-windows` console executable.

### Why it exists
- Provides repeatable, production-like automation cycles and reportable outcomes for reliability tracking.

---

## Project/framework mix (important)

- `WinFormsApplication`, `MenuTestApp`, `ElementTester`, `TestRunner` target **`net10.0-windows`**.
- `WpfApplication` targets **.NET Framework 4.8** (legacy-style csproj).
- `WebTestApp` is plain static web assets.

This mixed setup is intentional: it tests bridge behavior across both modern .NET desktop apps and legacy WPF project style.

---

## Typical usage flow in this folder

1. Launch UI targets (`WinFormsApplication`, `WpfApplication`), or let `TestRunner` launch them.
2. Start Apex bridge service.
3. Use `ElementTester` for focused/manual-like checks, or `TestRunner` for full multi-cycle automation.
4. Use `WebTestApp` when browser-side control mapping comparisons are needed.

---

## Notes on generated/support files

- `*.Designer.cs`, `*.resx`, `Properties/*.Designer.cs`, settings/resources files are largely framework-generated scaffolding and should usually only change through designers/tools.
- `*.ico` files are icon resources.
- `WinFormsApplication.csproj.DotSettings` is IDE tooling metadata.

