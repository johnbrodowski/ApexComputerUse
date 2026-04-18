param(
    [string]$Root = "../..",
    [string]$Out = "../../docs/project-map"
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectPath = Join-Path $scriptDir "ProjectMapBuilder.csproj"

$resolvedRoot = (Resolve-Path (Join-Path $scriptDir $Root)).Path
$resolvedOut = Join-Path $scriptDir $Out

Write-Host "Generating project map..."
Write-Host "Root: $resolvedRoot"
Write-Host "Out : $resolvedOut"

dotnet run --project $projectPath -- --root $resolvedRoot --out $resolvedOut
