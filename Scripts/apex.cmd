@echo off
::
:: apex.cmd — cmd.exe helper for ApexComputerUse
::
:: Sends commands to the HTTP server via curl (built-in on Windows 10+).
:: Start the HTTP server in the app's Remote Control group first.
::
:: Usage:
::   apex windows
::   apex status
::   apex elements [<ControlType>]
::   apex find <window> [id=<id>] [name=<name>] [type=<type>]
::   apex exec <action> [value=<value>]
::   apex ocr [<x,y,w,h>]
::   apex ai status
::   apex ai init model=<path> proj=<path>
::   apex ai describe [prompt=<text>]
::   apex ai ask prompt=<text>
::   apex ai file value=<path> [prompt=<text>]
::   apex capture [action=screen|window|element|elements] [value=id1,id2,...]
::   apex help
::
:: Options:
::   Set APEX_PORT environment variable to override port (default: 8080).
::
:: Examples:
::   apex windows
::   apex find Notepad
::   apex find "My App" id=btnOK
::   apex exec click
::   apex exec type value=Hello
::   apex ocr
::   apex ocr 0,0,300,50
::   apex ai status
::   apex ai init model=C:\models\v.gguf proj=C:\models\p.gguf
::   apex ai describe prompt="What do you see?"

setlocal enabledelayedexpansion

if "%APEX_PORT%"=="" (set PORT=8080) else (set PORT=%APEX_PORT%)
set BASE=http://localhost:%PORT%

if "%~1"=="" goto :usage
set CMD=%~1

if /I "%CMD%"=="windows"  goto :do_windows
if /I "%CMD%"=="status"   goto :do_status
if /I "%CMD%"=="elements" goto :do_elements
if /I "%CMD%"=="help"     goto :do_help
if /I "%CMD%"=="find"     goto :do_find
if /I "%CMD%"=="exec"     goto :do_exec
if /I "%CMD%"=="execute"  goto :do_exec
if /I "%CMD%"=="ocr"      goto :do_ocr
if /I "%CMD%"=="ai"       goto :do_ai
if /I "%CMD%"=="capture"  goto :do_capture
echo Unknown command: %CMD%
goto :usage

:: ── Simple GETs ──────────────────────────────────────────────────────────────

:do_windows
curl -s %BASE%/windows
echo.
goto :eof

:do_status
curl -s %BASE%/status
echo.
goto :eof

:do_help
curl -s %BASE%/help
echo.
goto :eof

:do_elements
if "%~2"=="" (
    curl -s %BASE%/elements
) else (
    curl -s "%BASE%/elements?type=%~2"
)
echo.
goto :eof

:: ── find ─────────────────────────────────────────────────────────────────────
:: apex find <window> [id=X] [name=X] [type=X]

:do_find
if "%~2"=="" (echo Usage: apex find ^<window^> [id=X] [name=X] [type=X] & goto :eof)
set WIN=%~2
set FIND_JSON={"window":"%WIN%"
set ARG_IDX=3

:find_loop
if "%~3"=="" goto :find_send
for /f "tokens=1,2 delims==" %%K in ("%~3") do (
    if /I "%%K"=="id"   set FIND_JSON=!FIND_JSON!,"automationId":"%%L"
    if /I "%%K"=="name" set FIND_JSON=!FIND_JSON!,"elementName":"%%L"
    if /I "%%K"=="type" set FIND_JSON=!FIND_JSON!,"searchType":"%%L"
)
shift /3
goto :find_loop

:find_send
set FIND_JSON=%FIND_JSON%}
curl -s -X POST %BASE%/find -H "Content-Type: application/json" -d "%FIND_JSON%"
echo.
goto :eof

:: ── exec ─────────────────────────────────────────────────────────────────────
:: apex exec <action> [value=X]

:do_exec
if "%~2"=="" (echo Usage: apex exec ^<action^> [value=X] & goto :eof)
set ACTION=%~2
set EXEC_VAL=
if not "%~3"=="" (
    for /f "tokens=1,2 delims==" %%K in ("%~3") do (
        if /I "%%K"=="value" set EXEC_VAL=%%L
    )
)
if "%EXEC_VAL%"=="" (
    curl -s -X POST %BASE%/execute -H "Content-Type: application/json" -d "{\"action\":\"%ACTION%\"}"
) else (
    curl -s -X POST %BASE%/execute -H "Content-Type: application/json" -d "{\"action\":\"%ACTION%\",\"value\":\"%EXEC_VAL%\"}"
)
echo.
goto :eof

:: ── ocr ──────────────────────────────────────────────────────────────────────
:: apex ocr [x,y,w,h]

:do_ocr
if "%~2"=="" (
    curl -s -X POST %BASE%/ocr
) else (
    curl -s -X POST %BASE%/ocr -H "Content-Type: application/json" -d "{\"value\":\"%~2\"}"
)
echo.
goto :eof

:: ── ai ───────────────────────────────────────────────────────────────────────
:: apex ai <sub> [key=value ...]

:do_ai
if "%~2"=="" (echo Usage: apex ai ^<status^|init^|describe^|file^|ask^> [key=value ...] & goto :eof)
set SUB=%~2

if /I "%SUB%"=="status" (
    curl -s %BASE%/ai/status
    echo.
    goto :eof
)

set AI_JSON={}
set M=
set P=
set PROMPT=
set VAL=

:ai_loop
if "%~3"=="" goto :ai_send
for /f "tokens=1,2 delims==" %%K in ("%~3") do (
    if /I "%%K"=="model"  set M=%%L
    if /I "%%K"=="proj"   set P=%%L
    if /I "%%K"=="prompt" set PROMPT=%%L
    if /I "%%K"=="value"  set VAL=%%L
)
shift /3
goto :ai_loop

:ai_send
set AI_JSON={
if not "%M%"==""      set AI_JSON=%AI_JSON%"model":"%M%",
if not "%P%"==""      set AI_JSON=%AI_JSON%"proj":"%P%",
if not "%PROMPT%"=="", set AI_JSON=%AI_JSON%"prompt":"%PROMPT%",
if not "%VAL%"==""    set AI_JSON=%AI_JSON%"value":"%VAL%",
:: trim trailing comma and close
for /l %%i in (1,1,1) do (
    set LAST=!AI_JSON:~-1!
    if "!LAST!"=="," set AI_JSON=!AI_JSON:~0,-1!
)
set AI_JSON=%AI_JSON%}
curl -s -X POST %BASE%/ai/%SUB% -H "Content-Type: application/json" -d "%AI_JSON%"
echo.
goto :eof

:: ── capture ───────────────────────────────────────────────────────────────────
:: apex capture [action=screen|window|element|elements] [value=id1,id2,...]

:do_capture
set CAP_ACTION=
set CAP_VALUE=
:cap_loop
if "%~2"=="" goto :cap_send
for /f "tokens=1,2 delims==" %%K in ("%~2") do (
    if /I "%%K"=="action" set CAP_ACTION=%%L
    if /I "%%K"=="value"  set CAP_VALUE=%%L
)
shift /2
goto :cap_loop

:cap_send
if "%CAP_ACTION%"=="" if "%CAP_VALUE%"=="" (
    curl -s -X POST %BASE%/capture
) else if "%CAP_VALUE%"=="" (
    curl -s -X POST %BASE%/capture -H "Content-Type: application/json" -d "{\"action\":\"%CAP_ACTION%\"}"
) else (
    curl -s -X POST %BASE%/capture -H "Content-Type: application/json" -d "{\"action\":\"%CAP_ACTION%\",\"value\":\"%CAP_VALUE%\"}"
)
echo.
goto :eof

:: ── usage ─────────────────────────────────────────────────────────────────────

:usage
echo.
echo  ApexComputerUse — AI computer use automation via cmd.exe
echo  Requires the HTTP server to be running (default port 8080).
echo  Set APEX_PORT to override.
echo.
echo  Usage:
echo    apex windows
echo    apex status
echo    apex elements [^<ControlType^>]
echo    apex find ^<window^> [id=X] [name=X] [type=X]
echo    apex exec ^<action^> [value=X]
echo    apex ocr [^<x,y,w,h^>]
echo    apex ai status
echo    apex ai init model=^<path^> proj=^<path^>
echo    apex ai describe [prompt=^<text^>]
echo    apex ai ask prompt=^<text^>
echo    apex ai file value=^<path^> [prompt=^<text^>]
echo    apex capture [action=screen^|window^|element^|elements] [value=id1,id2,...]
echo    apex help
echo.
goto :eof
