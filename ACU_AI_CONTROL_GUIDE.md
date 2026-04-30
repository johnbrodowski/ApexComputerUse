# ApexComputerUse AI Control Guide

Use this document as the single prompt/reference for controlling ApexComputerUse over HTTP.
It includes only the control surface an AI agent needs.

## 1) Core Model

- Base URL: `http://localhost:8080` (default; may shift if 8080 is occupied)
- Auth: required on every route except `GET /health`
- State is session-based:
  - `find` sets the current window and current element
  - `exec` acts on that current element unless an element ID is provided directly

If context may have changed, run `find` again before `exec`.

## 2) Hard Rules

1. Always request JSON output for machine parsing.
2. Always scope `find` with `window`.
3. Prefer numeric IDs from `windows` and `elements` over fuzzy names.
4. For web pages, inspect with `elements?onscreen=true` before guessing element names.
5. Browser URL navigation requires two steps: `setvalue` then `keys {ENTER}`.
6. After browser navigation, refresh `windows` and re-find target elements.

## 3) Required Request Pattern

Use these headers on authenticated calls:

```bash
-H "X-Api-Key: <key>" -H "Accept: application/json"
```

For explicit format control, add `?format=json` to URLs.

Quick connectivity checks:

```bash
# Unauthenticated liveness
curl "http://localhost:8080/health?format=json"

# Authenticated check
curl -H "X-Api-Key: <key>" -H "Accept: application/json" \
  "http://localhost:8080/ping?format=json"
```

## 4) Minimal Endpoint Set

### `GET /windows`
Returns open windows as JSON with stable numeric IDs (in `data.result`):

```json
[{"id":123456789,"title":"Window Title"}]
```

### `POST /find` (or `GET /find`)
Selects the window and optionally an element. This updates current context.

Request fields:
- `window` (required): window title fragment or numeric window ID
- `id` / `automationId`: element numeric map ID or AutomationId
- `name` / `elementName`: element name
- `type` / `searchType`: control type filter like `Button`, `Edit`
- `properties` (optional): set to `extra` to include value/helpText

Examples:

```bash
# Select window only
curl -X POST "http://localhost:8080/find?format=json" \
  -H "X-Api-Key: <key>" -H "Accept: application/json" -H "Content-Type: application/json" \
  -d '{"window":"Notepad"}'

# Select a button inside a specific window
curl -X POST "http://localhost:8080/find?format=json" \
  -H "X-Api-Key: <key>" -H "Accept: application/json" -H "Content-Type: application/json" \
  -d '{"window":123456789,"name":"Save","type":"Button"}'
```

### `GET /elements`
Scans the current window and returns the element tree with numeric IDs.
Requires a prior `find` call that selected a window.

Most useful query params:
- `onscreen=true`: hide offscreen branches (recommended default for browser pages)
- `depth=<n>`: limit tree depth
- `id=<elementId>`: expand from a mapped subtree root
- `type=<ControlType>`: filter by control type
- `match=<text>`: filter branches by text match
- `collapseChains=true`: collapse wrapper chains (`Pane/Group/Custom`)
- `includePath=true`: include ancestry breadcrumbs
- `properties=extra`: include `value` and `helpText`

Example:

```bash
curl -H "X-Api-Key: <key>" -H "Accept: application/json" \
  "http://localhost:8080/elements?onscreen=true&depth=2&format=json"
```

### `POST /exec` (alias: `/execute`)
Runs an action on the current element, or directly on a mapped element via ID.

Request fields:
- `action` (required)
- `value` (optional; action input)
- `element` / `id` / `automationId` (optional): numeric mapped element ID override from a prior `elements` scan

Example:

```bash
curl -X POST "http://localhost:8080/exec?format=json" \
  -H "X-Api-Key: <key>" -H "Accept: application/json" -H "Content-Type: application/json" \
  -d '{"action":"click"}'
```

## 5) Actions AI Usually Needs

Interaction:
- `click`, `double-click`, `right-click`, `hover`, `click-at`, `drag`

Keyboard and text:
- `type`, `keys`, `setvalue`, `insert`
- `gettext`, `getvalue`, `getselectedtext`
- `clearvalue`, `appendvalue`, `selectall`, `copy`, `cut`, `paste`, `undo`, `clear`

Selection/list:
- `select`, `select-index`, `getitems`, `getselecteditem`

State/diagnostics:
- `focus`, `describe`, `patterns`, `bounds`, `isenabled`, `isvisible`

Window:
- `minimize`, `maximize`, `restore`, `windowstate`, `move`, `resize`

Scroll:
- `scroll-up`, `scroll-down`, `scroll-left`, `scroll-right`, `scrollinto`, `scrollpercent`, `getscrollinfo`

## 6) Control Loop (Recommended)

1. `GET /ping`
2. `GET /windows`
3. `POST /find` with `window` + (`id` or `name`)
4. `POST /exec`
5. Verify result (`gettext`, `getvalue`, `windows`, or `elements`)
6. Repeat from step 3 whenever context changes

## 7) Browser Automation Rules

Navigate URL:

1. `find` browser window + address bar
2. `exec setvalue` with URL
3. `exec keys` with `{ENTER}`
4. `windows` again (title often changes)
5. `find` and continue

Do not rely on stale element references after navigation or major page updates.

## 8) Result Format

With JSON output, responses follow:

```json
{
  "success": true,
  "action": "find",
  "data": {
    "result": "...",
    "message": "..."
  },
  "error": null
}
```

Notes:
- `data.result` is often a JSON string payload for structured data (parse it again when needed).
- Some responses contain only `data.message`; do not assume `data.result` is always present.
- `success=false` responses return HTTP 400 for command errors.
- Missing/invalid API key returns HTTP 401.

## 9) Fast Recovery

- `No window found`: refresh `windows`, use the current title or window ID.
- `No element found`: call `elements?onscreen=true`, then search by ID or exact exposed name.
- Action hits wrong target: run `find` again in the intended window before `exec`.
- Nothing happens after `setvalue` in browser: send `keys` with `{ENTER}`.
