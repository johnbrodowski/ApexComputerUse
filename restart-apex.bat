@echo off
echo Stopping ApexComputerUse...
taskkill /F /IM ApexComputerUse.exe /T 2>nul
timeout /t 1 /nobreak >nul

set RELEASE=%~dp0ApexComputerUse\bin\Release\net10.0-windows\win-x64\ApexComputerUse.exe
set DEBUG=%~dp0ApexComputerUse\bin\Debug\net10.0-windows\win-x64\ApexComputerUse.exe

if exist "%RELEASE%" (
    echo Starting ApexComputerUse [Release]...
    start "" "%RELEASE%"
    goto :done
)

if exist "%DEBUG%" (
    echo Starting ApexComputerUse [Debug]...
    start "" "%DEBUG%"
    goto :done
)

echo No built exe found. Running via dotnet...
start "" cmd /k "dotnet run --project %~dp0ApexComputerUse\ApexComputerUse.csproj"

:done
