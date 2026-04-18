import subprocess
import json
import re

BASE = "http://localhost:8080"

def find_el(id):
    r = subprocess.run(["curl","-s","-X","POST",f"{BASE}/find",
                        "-H","Content-Type: application/json",
                        "-d",json.dumps({"window":"UI Torture Test","id":id})],
                       capture_output=True, text=True)
    return r.stdout

def exec_act(body):
    r = subprocess.run(["curl","-s","-X","POST",f"{BASE}/execute",
                        "-H","Content-Type: application/json",
                        "-d",json.dumps(body)],
                       capture_output=True, text=True)
    return r.stdout

def parse(response):
    try:
        m = re.search(r'id="apex-result">\s*(\{.*?\})\s*</script>', response, re.DOTALL)
        if m:
            return json.loads(m.group(1))
        return json.loads(response)
    except:
        return {"success": False, "error": "parse error", "data": {}}

results = []

def chk(label, response, expect_success=True):
    d = parse(response)
    success = d.get("success", False)
    result = ""
    if d.get("data"):
        result = d["data"].get("result", "")
    error = d.get("error", "")
    status = "PASS" if success == expect_success else "FAIL"
    entry = f"  {label}: {status} | result={result}"
    if error and error not in (None, "null", "") and status == "FAIL":
        entry += f" err={error}"
    results.append(entry)
    print(entry)
    return d

def section(name):
    s = f"\n=== {name} ==="
    results.append(s)
    print(s)

# ============================================================
section("Department ComboBox (401807748)")
chk("find", find_el(401807748))
chk("isenabled", exec_act({"action":"isenabled"}))
chk("getitems", exec_act({"action":"getitems"}))
chk("getselecteditem", exec_act({"action":"getselecteditem"}))
chk("select Engineering", exec_act({"action":"select","value":"Engineering"}))
chk("getselecteditem", exec_act({"action":"getselecteditem"}))
chk("select-index 0", exec_act({"action":"select-index","value":"0"}))
chk("getselecteditem", exec_act({"action":"getselecteditem"}))
chk("expandstate", exec_act({"action":"expandstate"}))
chk("expand", exec_act({"action":"expand"}))
chk("expandstate-open", exec_act({"action":"expandstate"}))
chk("collapse", exec_act({"action":"collapse"}))
chk("expandstate-closed", exec_act({"action":"expandstate"}))

section("Hire Date ComboBox (184686919)")
chk("find", find_el(184686919))
chk("getitems", exec_act({"action":"getitems"}))
chk("getselecteditem", exec_act({"action":"getselecteditem"}))
chk("select-index 0", exec_act({"action":"select-index","value":"0"}))
chk("getselecteditem", exec_act({"action":"getselecteditem"}))

section("Start Time ComboBox (1720374504)")
chk("find", find_el(1720374504))
chk("getitems", exec_act({"action":"getitems"}))
chk("getselecteditem", exec_act({"action":"getselecteditem"}))
chk("select-index 2", exec_act({"action":"select-index","value":"2"}))
chk("getselecteditem", exec_act({"action":"getselecteditem"}))

section("Status ComboBox (751774475)")
chk("find", find_el(751774475))
chk("getitems", exec_act({"action":"getitems"}))
chk("getselecteditem", exec_act({"action":"getselecteditem"}))
chk("select Inactive", exec_act({"action":"select","value":"Inactive"}))
chk("getselecteditem-Inactive", exec_act({"action":"getselecteditem"}))
chk("select Active", exec_act({"action":"select","value":"Active"}))
chk("getselecteditem-Active", exec_act({"action":"getselecteditem"}))

section("Location ComboBox editable (1083854938)")
chk("find", find_el(1083854938))
chk("getitems", exec_act({"action":"getitems"}))
chk("getselecteditem", exec_act({"action":"getselecteditem"}))
chk("type New York", exec_act({"action":"type","value":"New York"}))
chk("gettext", exec_act({"action":"gettext"}))
chk("select-index 0", exec_act({"action":"select-index","value":"0"}))
chk("getselecteditem", exec_act({"action":"getselecteditem"}))

section("Toolbar ComboBox (272108641)")
chk("find", find_el(272108641))
chk("getitems", exec_act({"action":"getitems"}))
chk("getselecteditem", exec_act({"action":"getselecteditem"}))
chk("select-index 1", exec_act({"action":"select-index","value":"1"}))
chk("getselecteditem", exec_act({"action":"getselecteditem"}))

section("Annual Salary Spinner (2099700214)")
chk("find", find_el(2099700214))
chk("isenabled", exec_act({"action":"isenabled"}))
chk("getrange", exec_act({"action":"getrange"}))
chk("rangeinfo", exec_act({"action":"rangeinfo"}))
chk("setrange 50000", exec_act({"action":"setrange","value":"50000"}))
chk("getrange", exec_act({"action":"getrange"}))

section("Years of Service Spinner (1084437216)")
chk("find", find_el(1084437216))
chk("getrange", exec_act({"action":"getrange"}))
chk("rangeinfo", exec_act({"action":"rangeinfo"}))
chk("setrange 5", exec_act({"action":"setrange","value":"5"}))
chk("getrange", exec_act({"action":"getrange"}))

section("Title Spinner (1941804456)")
chk("find", find_el(1941804456))
chk("getrange", exec_act({"action":"getrange"}))
chk("rangeinfo", exec_act({"action":"rangeinfo"}))
chk("setrange 3", exec_act({"action":"setrange","value":"3"}))
chk("getrange", exec_act({"action":"getrange"}))

section("Access Level Slider (892882131)")
chk("find", find_el(892882131))
chk("isenabled", exec_act({"action":"isenabled"}))
chk("getrange", exec_act({"action":"getrange"}))
chk("rangeinfo", exec_act({"action":"rangeinfo"}))
chk("setrange 7", exec_act({"action":"setrange","value":"7"}))
chk("getrange-7", exec_act({"action":"getrange"}))
chk("setrange 0", exec_act({"action":"setrange","value":"0"}))
chk("getrange-0", exec_act({"action":"getrange"}))

section("CheckBox AD Sync (1373727514)")
chk("find", find_el(1373727514))
chk("isenabled", exec_act({"action":"isenabled"}))
chk("gettoggle", exec_act({"action":"gettoggle"}))
chk("toggle-on", exec_act({"action":"toggle-on"}))
chk("gettoggle-on", exec_act({"action":"gettoggle"}))
chk("toggle-off", exec_act({"action":"toggle-off"}))
chk("gettoggle-off", exec_act({"action":"gettoggle"}))
chk("toggle", exec_act({"action":"toggle"}))
chk("gettoggle", exec_act({"action":"gettoggle"}))

section("CheckBox MFA Enabled (1080859953)")
chk("find", find_el(1080859953))
chk("gettoggle", exec_act({"action":"gettoggle"}))
chk("toggle-on", exec_act({"action":"toggle-on"}))
chk("gettoggle-on", exec_act({"action":"gettoggle"}))
chk("toggle-off", exec_act({"action":"toggle-off"}))
chk("gettoggle-off", exec_act({"action":"gettoggle"}))

section("CheckBox VPN Access (2119139710)")
chk("find", find_el(2119139710))
chk("gettoggle", exec_act({"action":"gettoggle"}))
chk("toggle-on", exec_act({"action":"toggle-on"}))
chk("gettoggle-on", exec_act({"action":"gettoggle"}))
chk("toggle-off", exec_act({"action":"toggle-off"}))
chk("gettoggle-off", exec_act({"action":"gettoggle"}))

section("CheckBox Remote Desktop (1647672661)")
chk("find", find_el(1647672661))
chk("gettoggle", exec_act({"action":"gettoggle"}))
chk("toggle-on", exec_act({"action":"toggle-on"}))
chk("gettoggle-on", exec_act({"action":"gettoggle"}))
chk("toggle-off", exec_act({"action":"toggle-off"}))
chk("gettoggle-off", exec_act({"action":"gettoggle"}))

section("CheckBox Admin Override (830643707)")
chk("find", find_el(830643707))
chk("gettoggle", exec_act({"action":"gettoggle"}))
chk("toggle-on", exec_act({"action":"toggle-on"}))
chk("gettoggle-on", exec_act({"action":"gettoggle"}))
chk("toggle-off", exec_act({"action":"toggle-off"}))
chk("gettoggle-off", exec_act({"action":"gettoggle"}))

section("Notes Document (511810706)")
chk("find", find_el(511810706))
chk("isenabled", exec_act({"action":"isenabled"}))
chk("gettext", exec_act({"action":"gettext"}))
chk("type Test note", exec_act({"action":"type","value":"Test note content"}))
chk("gettext", exec_act({"action":"gettext"}))
chk("clearvalue", exec_act({"action":"clearvalue"}))
chk("gettext-cleared", exec_act({"action":"gettext"}))

section("ProgressBar (356827106)")
chk("find", find_el(356827106))
chk("isenabled", exec_act({"action":"isenabled"}))
chk("isvisible", exec_act({"action":"isvisible"}))
chk("getrange", exec_act({"action":"getrange"}))
chk("rangeinfo", exec_act({"action":"rangeinfo"}))
chk("patterns", exec_act({"action":"patterns"}))

section("Toolbar Button New (429412511)")
chk("find", find_el(429412511))
chk("isenabled", exec_act({"action":"isenabled"}))
chk("click", exec_act({"action":"click"}))

section("Toolbar Button Open (83280353)")
chk("find", find_el(83280353))
chk("isenabled", exec_act({"action":"isenabled"}))
chk("click", exec_act({"action":"click"}))

section("Toolbar Button Save (1405240437)")
chk("find", find_el(1405240437))
chk("isenabled", exec_act({"action":"isenabled"}))
chk("click", exec_act({"action":"click"}))

print("\n\nDone with Identity tab main controls.")
