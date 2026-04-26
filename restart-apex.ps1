$root = Split-Path $MyInvocation.MyCommand.Path

Write-Host "Stopping ApexComputerUse..."
Get-Process -Name "ApexComputerUse" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

$release = Join-Path $root "ApexComputerUse\bin\Release\net10.0-windows\win-x64\ApexComputerUse.exe"
$debug   = Join-Path $root "ApexComputerUse\bin\Debug\net10.0-windows\win-x64\ApexComputerUse.exe"

if (Test-Path $release) {
    Write-Host "Starting ApexComputerUse [Release]..."
    Start-Process $release
} elseif (Test-Path $debug) {
    Write-Host "Starting ApexComputerUse [Debug]..."
    Start-Process $debug
} else {
    Write-Host "No built exe found. Running via dotnet..."
    $proj = Join-Path $root "ApexComputerUse\ApexComputerUse.csproj"
    Start-Process "dotnet" "run --project `"$proj`""
}
