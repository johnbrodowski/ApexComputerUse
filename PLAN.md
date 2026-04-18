# Plan: S&P 500 Top Gainers Task — DEBUG MODE Execution

## Context

Execute a multi-app Windows automation task via the ApexComputerUse REST API following DEBUG MODE rules:
- One step at a time, validated after each curl call
- Stop on failure, diagnose root cause, fix source code if needed
- No fabricated data — only real API responses count

**Confirmed pre-conditions:**
- Server running at `http://localhost:8081`
- API key (from AGENT_INSTRUCTIONS.md): `d4x7kiS8XeeWxJ0XFNdrs7k_upavnAOS`
- Edge is open with Gmail already authenticated

**Task:**
1. Search web for top 5 S&P 500 gainers (ticker, price, % change)
2. Take notes in Notepad as data is collected
3. Use Calculator to compute the average % gain
4. Send a Gmail summary to `johnbrodowski@gmail.com`

---

## Critical Known Constraints

| Constraint | Detail |
|---|---|
| `EnableShellRun` defaults to `false` | `/run` endpoint (needed to launch apps) is disabled |
| Browser window titles change on navigation | Must re-call `/windows` after each navigation to get fresh ID |
| `setvalue` required for address bar | Not `type` — Value automation pattern is the only reliable method |
| State rule | Every `/exec` targets the last-found element; re-run `/find` whenever context changes |

---

## Files to Modify

| File | Change |
|---|---|
| `ApexComputerUse/appsettings.json` | `"EnableShellRun": true` — enables `/run` endpoint for app launching |

---

## Step-by-Step Execution Plan

### Phase 0 — Enable Shell Execution

**Why:** `/run` endpoint is disabled by default. Needed to open Notepad and Calculator.

**Step 0.1 — Edit config:**
```json
// ApexComputerUse/appsettings.json — change:
"EnableShellRun": false
// to:
"EnableShellRun": true
```
File: `ApexComputerUse/appsettings.json`

**Step 0.2 — Restart server** (config read at startup only):
```bash
# Stop current server process, then:
dotnet run --project ApexComputerUse/ApexComputerUse.csproj
```

---

### Phase 1 — Server Verification

**Step 1.1 — Ping:**
```bash
curl -H "X-Api-Key: d4x7kiS8XeeWxJ0XFNdrs7k_upavnAOS" http://localhost:8081/ping
```
Expected: `{"success":true,"action":"ping",...}`

**Step 1.2 — List open windows (baseline):**
```bash
curl -H "X-Api-Key: d4x7kiS8XeeWxJ0XFNdrs7k_upavnAOS" http://localhost:8081/windows
```
Note the window IDs for Edge (containing Gmail). Check if Notepad/Calculator already open.

---

### Phase 2 — Launch Applications

**Step 2.1 — Open Notepad** (if not already open):
```bash
curl -H "X-Api-Key: d4x7kiS8XeeWxJ0XFNdrs7k_upavnAOS" "http://localhost:8081/run?cmd=notepad.exe"
```
Expected: `{"success":true,...,"data":{"exit_code":0}}`

**Step 2.2 — Open Calculator** (if not already open):
```bash
curl -H "X-Api-Key: d4x7kiS8XeeWxJ0XFNdrs7k_upavnAOS" "http://localhost:8081/run?cmd=calc.exe"
```

**Step 2.3 — Verify both are open:**
```bash
curl -H "X-Api-Key: d4x7kiS8XeeWxJ0XFNdrs7k_upavnAOS" http://localhost:8081/windows
```
All three targets (Notepad, Calculator, Edge) must appear in the list.

---

### Phase 3 — Fetch S&P 500 Top Gainers via Edge

**Step 3.1 — Find Edge window:**
```bash
curl -H "X-Api-Key: d4x7kiS8XeeWxJ0XFNdrs7k_upavnAOS" -X POST http://localhost:8081/find \
  -H "Content-Type: application/json" \
  -d '{"window":"Edge"}'
```

**Step 3.2 — Find and focus the address bar:**
```bash
curl -H "X-Api-Key: d4x7kiS8XeeWxJ0XFNdrs7k_upavnAOS" -X POST http://localhost:8081/find \
  -H "Content-Type: application/json" \
  -d '{"window":"Edge","name":"Address and search bar","type":"Edit"}'
```

**Step 3.3 — Navigate to Yahoo Finance gainers page:**
```bash
# setvalue is required for address bars (Value pattern, not keyboard)
curl -H "X-Api-Key: d4x7kiS8XeeWxJ0XFNdrs7k_upavnAOS" -X POST http://localhost:8081/exec \
  -H "Content-Type: application/json" \
  -d '{"action":"setvalue","value":"https://finance.yahoo.com/markets/stocks/gainers/"}'

# Press Enter to navigate
curl -H "X-Api-Key: d4x7kiS8XeeWxJ0XFNdrs7k_upavnAOS" -X POST http://localhost:8081/exec \
  -H "Content-Type: application/json" \
  -d '{"action":"keys","value":"{ENTER}"}'
```

**Step 3.4 — Wait for page to load, then get updated window title:**
```bash
curl -H "X-Api-Key: d4x7kiS8XeeWxJ0XFNdrs7k_upavnAOS" http://localhost:8081/windows
```
Note the new window title (will now contain "Gainers" or "Yahoo Finance").

**Step 3.5 — Get visible page elements to locate stock data:**
```bash
curl -H "X-Api-Key: d4x7kiS8XeeWxJ0XFNdrs7k_upavnAOS" -X POST http://localhost:8081/find \
  -H "Content-Type: application/json" \
  -d '{"window":"Yahoo Finance"}'

curl -H "X-Api-Key: d4x7kiS8XeeWxJ0XFNdrs7k_upavnAOS" \
  "http://localhost:8081/elements?onscreen=true"
```

**Step 3.6 — Extract stock data:**
Strategy A (preferred): Read text elements from the gainers table via `gettext` on table rows.
Strategy B (fallback): OCR the visible page area if table elements don't expose text directly.

```bash
# If table rows are accessible as elements — use gettext on each row
# If not — OCR the page
curl -H "X-Api-Key: d4x7kiS8XeeWxJ0XFNdrs7k_upavnAOS" -X POST http://localhost:8081/capture \
  -H "Content-Type: application/json" \
  -d '{"action":"window"}'
# Then OCR:
curl -H "X-Api-Key: d4x7kiS8XeeWxJ0XFNdrs7k_upavnAOS" -X POST http://localhost:8081/ocr
```

**Data to record for each of 5 stocks:** ticker symbol, current price, % change.

---

### Phase 4 — Take Notes in Notepad

**Step 4.1 — Find Notepad text area:**
```bash
curl -H "X-Api-Key: d4x7kiS8XeeWxJ0XFNdrs7k_upavnAOS" -X POST http://localhost:8081/find \
  -H "Content-Type: application/json" \
  -d '{"window":"Notepad","type":"Edit"}'
```

**Step 4.2 — Type the stock data (repeat for each stock):**
```bash
curl -H "X-Api-Key: d4x7kiS8XeeWxJ0XFNdrs7k_upavnAOS" -X POST http://localhost:8081/exec \
  -H "Content-Type: application/json" \
  -d '{"action":"type","value":"S&P 500 Top Gainers - [DATE]\r\n1. TICKER  $PRICE  +X.XX%\r\n2. ...\r\n"}'
```

**Step 4.3 — Verify notes were entered correctly:**
```bash
curl -H "X-Api-Key: d4x7kiS8XeeWxJ0XFNdrs7k_upavnAOS" -X POST http://localhost:8081/exec \
  -H "Content-Type: application/json" \
  -d '{"action":"gettext"}'
```
Confirm the text matches what was entered. If not, stop and diagnose.

---

### Phase 5 — Compute Average in Calculator

**Step 5.1 — Find Calculator window:**
```bash
curl -H "X-Api-Key: d4x7kiS8XeeWxJ0XFNdrs7k_upavnAOS" -X POST http://localhost:8081/find \
  -H "Content-Type: application/json" \
  -d '{"window":"Calculator"}'
```

**Step 5.2 — Input calculation via keys** (sum of 5 % changes, then divide by 5):
```bash
# Example: (2.34 + 1.56 + 3.21 + 0.89 + 4.12) / 5
# Type the expression using calculator keys
curl -H "X-Api-Key: d4x7kiS8XeeWxJ0XFNdrs7k_upavnAOS" -X POST http://localhost:8081/exec \
  -H "Content-Type: application/json" \
  -d '{"action":"keys","value":"2.34+1.56+3.21+0.89+4.12"}'

# Press = to get sum
curl -H "X-Api-Key: d4x7kiS8XeeWxJ0XFNdrs7k_upavnAOS" -X POST http://localhost:8081/exec \
  -H "Content-Type: application/json" \
  -d '{"action":"keys","value":"="}'

# Divide by 5
curl -H "X-Api-Key: d4x7kiS8XeeWxJ0XFNdrs7k_upavnAOS" -X POST http://localhost:8081/exec \
  -H "Content-Type: application/json" \
  -d '{"action":"keys","value":"/5="}'
```

**Step 5.3 — Read the result from the Calculator display:**
```bash
curl -H "X-Api-Key: d4x7kiS8XeeWxJ0XFNdrs7k_upavnAOS" -X POST http://localhost:8081/find \
  -H "Content-Type: application/json" \
  -d '{"window":"Calculator","automationId":"CalculatorResults"}'

curl -H "X-Api-Key: d4x7kiS8XeeWxJ0XFNdrs7k_upavnAOS" -X POST http://localhost:8081/exec \
  -H "Content-Type: application/json" \
  -d '{"action":"gettext"}'
```
Record this value as the average % gain.

**Step 5.4 — Add average to Notepad notes:**
```bash
# Re-find Notepad edit area
curl -H "X-Api-Key: d4x7kiS8XeeWxJ0XFNdrs7k_upavnAOS" -X POST http://localhost:8081/find \
  -H "Content-Type: application/json" \
  -d '{"window":"Notepad","type":"Edit"}'

curl -H "X-Api-Key: d4x7kiS8XeeWxJ0XFNdrs7k_upavnAOS" -X POST http://localhost:8081/exec \
  -H "Content-Type: application/json" \
  -d '{"action":"keys","value":"{CTRL}{END}"}'

curl -H "X-Api-Key: d4x7kiS8XeeWxJ0XFNdrs7k_upavnAOS" -X POST http://localhost:8081/exec \
  -H "Content-Type: application/json" \
  -d '{"action":"type","value":"\r\nAverage % Gain: X.XX%"}'
```

---

### Phase 6 — Send Gmail Summary via Edge

**Step 6.1 — Find Edge, navigate back to Gmail:**
```bash
curl -H "X-Api-Key: d4x7kiS8XeeWxJ0XFNdrs7k_upavnAOS" -X POST http://localhost:8081/find \
  -H "Content-Type: application/json" \
  -d '{"window":"Edge","name":"Address and search bar","type":"Edit"}'

curl -H "X-Api-Key: d4x7kiS8XeeWxJ0XFNdrs7k_upavnAOS" -X POST http://localhost:8081/exec \
  -H "Content-Type: application/json" \
  -d '{"action":"setvalue","value":"https://mail.google.com"}'

curl -H "X-Api-Key: d4x7kiS8XeeWxJ0XFNdrs7k_upavnAOS" -X POST http://localhost:8081/exec \
  -H "Content-Type: application/json" \
  -d '{"action":"keys","value":"{ENTER}"}'
```

**Step 6.2 — Wait for Gmail to load, find Compose button:**
```bash
curl -H "X-Api-Key: d4x7kiS8XeeWxJ0XFNdrs7k_upavnAOS" http://localhost:8081/windows

curl -H "X-Api-Key: d4x7kiS8XeeWxJ0XFNdrs7k_upavnAOS" -X POST http://localhost:8081/find \
  -H "Content-Type: application/json" \
  -d '{"window":"Gmail","name":"Compose","type":"Button"}'

curl -H "X-Api-Key: d4x7kiS8XeeWxJ0XFNdrs7k_upavnAOS" -X POST http://localhost:8081/exec \
  -H "Content-Type: application/json" \
  -d '{"action":"click"}'
```

**Step 6.3 — Fill To: field:**
```bash
curl -H "X-Api-Key: d4x7kiS8XeeWxJ0XFNdrs7k_upavnAOS" -X POST http://localhost:8081/find \
  -H "Content-Type: application/json" \
  -d '{"window":"Gmail","name":"To","type":"Edit"}'

curl -H "X-Api-Key: d4x7kiS8XeeWxJ0XFNdrs7k_upavnAOS" -X POST http://localhost:8081/exec \
  -H "Content-Type: application/json" \
  -d '{"action":"type","value":"johnbrodowski@gmail.com"}'

curl -H "X-Api-Key: d4x7kiS8XeeWxJ0XFNdrs7k_upavnAOS" -X POST http://localhost:8081/exec \
  -H "Content-Type: application/json" \
  -d '{"action":"keys","value":"{TAB}"}'
```

**Step 6.4 — Fill Subject:**
```bash
curl -H "X-Api-Key: d4x7kiS8XeeWxJ0XFNdrs7k_upavnAOS" -X POST http://localhost:8081/find \
  -H "Content-Type: application/json" \
  -d '{"window":"Gmail","name":"Subject","type":"Edit"}'

curl -H "X-Api-Key: d4x7kiS8XeeWxJ0XFNdrs7k_upavnAOS" -X POST http://localhost:8081/exec \
  -H "Content-Type: application/json" \
  -d '{"action":"type","value":"S&P 500 Top Movers"}'
```

**Step 6.5 — Fill body with stock data + average:**
```bash
curl -H "X-Api-Key: d4x7kiS8XeeWxJ0XFNdrs7k_upavnAOS" -X POST http://localhost:8081/find \
  -H "Content-Type: application/json" \
  -d '{"window":"Gmail","name":"Message Body","type":"Document"}'

curl -H "X-Api-Key: d4x7kiS8XeeWxJ0XFNdrs7k_upavnAOS" -X POST http://localhost:8081/exec \
  -H "Content-Type: application/json" \
  -d '{"action":"type","value":"Top 5 S&P 500 Gainers Today:\n\n1. TICKER $PRICE +X.XX%\n2. ...\n\nAverage % Gain: X.XX%"}'
```

**Step 6.6 — Send email (Ctrl+Enter):**
```bash
curl -H "X-Api-Key: d4x7kiS8XeeWxJ0XFNdrs7k_upavnAOS" -X POST http://localhost:8081/exec \
  -H "Content-Type: application/json" \
  -d '{"action":"keys","value":"Ctrl+{ENTER}"}'
```

**Step 6.7 — Verify send by checking for "Message sent" confirmation:**
```bash
curl -H "X-Api-Key: d4x7kiS8XeeWxJ0XFNdrs7k_upavnAOS" -X POST http://localhost:8081/find \
  -H "Content-Type: application/json" \
  -d '{"window":"Gmail","name":"Message sent"}'
```

---

## Bug Fix Protocol (DEBUG MODE)

If any step fails:
1. Inspect the `"error"` field in the API response
2. Determine if it's an API usage issue (re-examine AGENT_INSTRUCTIONS.md) or a source code bug
3. If a source code bug:
   - Identify the file and method (based on codebase map above)
   - Apply the minimal fix
   - Rebuild: `dotnet build ApexComputerUse/ApexComputerUse.csproj`
   - Re-test the failed step before continuing

**Common failure scenarios and recovery:**
| Failure | Root cause | Fix |
|---|---|---|
| `/run` returns 400 "disabled" | EnableShellRun still false | Edit appsettings.json, restart |
| `/find` "not found" for Edge address bar | Window title changed mid-navigation | Re-call `/windows`, use updated title |
| `gettext` on Calculator returns empty | Wrong element targeted | Use `?onscreen=true` element scan, find `CalculatorResults` by automationId |
| Gmail compose fields not found | Page not fully loaded, wrong element names | Check `/elements?onscreen=true`, adjust element name in `/find` |
| email not sent via Ctrl+Enter | Focus not on compose body | Re-find and click body element first |

---

## Verification Checklist

- [ ] Phase 0: appsettings.json has `EnableShellRun: true`, server restarted and ping returns success
- [ ] Phase 1: All 3 app windows visible in `/windows` response  
- [ ] Phase 3: 5 stocks extracted with ticker, price, % change (all verified non-fabricated from API response)
- [ ] Phase 4: `gettext` on Notepad reads back the correct stock data  
- [ ] Phase 5: Calculator display shows the computed average (read via `gettext`)
- [ ] Phase 6: Gmail shows "Message sent" confirmation after `Ctrl+Enter`
