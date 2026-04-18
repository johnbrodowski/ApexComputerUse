# TestApplications

This folder is now consolidated around **three UI test apps**:

1. **WinForms app**: `WinFormsApplication`
2. **WPF app**: `WpfApplication`
3. **Web page app**: `WebTestApp`

Each UI test app is intended to expose at least one instance of every major control type used by automation (text input, button, checkbox, radio, combo, list controls, grid/table, tree, tabs, menus, date/time, slider/progress, scrollable containers, and dialogs/context menus where applicable).

In addition to those test apps, this folder also includes two harness tools:

- `ElementTester` (focused per-control interaction tests against running windows)
- `TestRunner` (cycle-based build/launch/test orchestration)

---

## Consolidated UI apps

## 1) `WinFormsApplication/`

Primary desktop WinForms target.

- Entry point launches `TortureTestForm` for broad control coverage.
- `TortureTestForm.cs` contains the comprehensive control surface (menu, toolbar, status bar, tabbed pages, and many core control patterns).
- `Form1` remains as a smaller baseline form, but `TortureTestForm` is the canonical automation target.

Key files:
- `Program.cs`
- `TortureTestForm.cs`
- `Form1.cs`, `Form1.Designer.cs`, `Form1.resx`
- `Core/DataGridViewItem.cs`
- `WinFormsApplication.csproj`

## 2) `WpfApplication/`

Primary desktop WPF target.

- `MainWindow` includes baseline controls and menu paths.
- `TortureTestWindow` provides broad, dense control coverage with explicit automation IDs.
- View models provide stable data and command-driven state changes used by automated assertions.

Key files:
- `App.xaml`, `App.xaml.cs`
- `MainWindow.xaml`, `MainWindow.xaml.cs`
- `TortureTestWindow.xaml`, `TortureTestWindow.xaml.cs`
- `MainViewModel.cs`, `TortureTestViewModel.cs`
- `Infrastructure/ObservableObject.cs`, `Infrastructure/RelayCommand.cs`
- `WpfApplication.csproj`

## 3) `WebTestApp/`

Primary web-page test target.

- `index.html` contains menu patterns, tabs, form controls, list/tree/grid style structures, context menu behavior, and scrollable regions.
- Mirrors common desktop control semantics for browser automation parity checks.

Key file:
- `index.html`

---

## Harness tools (kept in this folder)

## `ElementTester/`

CLI tool for targeted control-type testing.

- Scans live windows through bridge commands.
- Runs per-control tests (TextBox, Button, CheckBox, ComboBox, Slider, TreeView, ListView, DataGrid, etc.).
- Useful for focused validation of one control class at a time.

## `TestRunner/`

Cycle-based orchestrator.

- Handles build/launch/test/teardown loops.
- Launches WinForms + WPF targets, waits for bridge readiness, runs suite, reports progress/final status.
- Supports skip-passed mode and persisted test results.

---

## What changed in consolidation

- Removed the separate `MenuTestApp` project.
- Kept one WinForms app, one WPF app, and one web test app as the consolidated UI targets.
- Retained `ElementTester` and `TestRunner` as automation harnesses (not standalone UI targets).

