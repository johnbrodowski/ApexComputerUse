#Requires -Version 5.1
<#
.SYNOPSIS
    PowerShell module for ApexComputerUse - AI computer use automation.

.DESCRIPTION
    Communicates with ApexComputerUse via its named pipe server.
    Start the pipe server in the app's Remote Control group box, then:

        Import-Module .\Scripts\ApexComputerUse.psm1
        Connect-FlaUI
        Get-FlaUIWindows
        Find-FlaUIElement -Window "Notepad"
        Invoke-FlaUIAction -Action type -Value "Hello"
        Invoke-FlaUIOcr
        Disconnect-FlaUI

    All functions return a [PSCustomObject] with Success, Message, and Data
    properties (matching the server's JSON response), except helpers like
    Get-FlaUIWindows and Get-FlaUIElements which return string arrays.
#>

$script:Pipe     = $null
$script:Reader   = $null
$script:Writer   = $null
$script:PipeName = $null

# ── Connection ────────────────────────────────────────────────────────────────

function Connect-FlaUI {
    <#
    .SYNOPSIS  Connect to the ApexComputerUse named pipe server.
    .PARAMETER PipeName   Pipe name configured in the app (default: ApexComputerUse).
    .PARAMETER TimeoutMs  Connection timeout in milliseconds (default: 5000).
    #>
    param(
        [string]$PipeName   = 'ApexComputerUse',
        [int]   $TimeoutMs  = 5000
    )

    if ($script:Pipe -and $script:Pipe.IsConnected) {
        Write-Warning 'Already connected. Call Disconnect-FlaUI first.'
        return
    }

    $script:PipeName = $PipeName
    $script:Pipe     = [System.IO.Pipes.NamedPipeClientStream]::new('.', $PipeName, 'InOut')
    $script:Pipe.Connect($TimeoutMs)
    $script:Reader   = [System.IO.StreamReader]::new($script:Pipe, [System.Text.Encoding]::UTF8)
    $script:Writer   = [System.IO.StreamWriter]::new($script:Pipe, [System.Text.Encoding]::UTF8)
    $script:Writer.AutoFlush = $true
    Write-Host "Connected to \\.\pipe\$PipeName"
}

function Disconnect-FlaUI {
    <#.SYNOPSIS  Close the pipe connection.#>
    try { $script:Writer?.Close() } catch {}
    try { $script:Reader?.Close() } catch {}
    try { $script:Pipe?.Dispose()  } catch {}
    $script:Pipe     = $null
    $script:Reader   = $null
    $script:Writer   = $null
    $script:PipeName = $null
    Write-Host 'Disconnected.'
}

# ── Raw send/receive ──────────────────────────────────────────────────────────

function Send-FlaUICommand {
    <#
    .SYNOPSIS  Send a raw command hashtable and return the parsed JSON response.
    .EXAMPLE   Send-FlaUICommand @{ command='windows' }
    #>
    param([Parameter(Mandatory)][hashtable]$Request)

    if (-not $script:Pipe -or -not $script:Pipe.IsConnected) {
        throw 'Not connected. Call Connect-FlaUI first.'
    }

    $json = $Request | ConvertTo-Json -Compress -Depth 5
    $script:Writer.WriteLine($json)
    $line = $script:Reader.ReadLine()
    return $line | ConvertFrom-Json
}

# ── Discovery ─────────────────────────────────────────────────────────────────

function Get-FlaUIWindows {
    <#.SYNOPSIS  Return the titles of all open windows.#>
    $r = Send-FlaUICommand @{ command = 'windows' }
    if ($r.data) { return $r.data -split "`n" | Where-Object { $_ -ne '' } }
}

function Get-FlaUIStatus {
    <#.SYNOPSIS  Return the current window/element state.#>
    Send-FlaUICommand @{ command = 'status' }
}

function Get-FlaUIHelp {
    <#.SYNOPSIS  Return the server help text.#>
    (Send-FlaUICommand @{ command = 'help' }).data
}

function Get-FlaUIElements {
    <#
    .SYNOPSIS  List elements in the current window.
    .PARAMETER Type  Optional ControlType filter (e.g. Button, Edit). Default: all.
    #>
    param([string]$Type = 'All')
    $req = @{ command = 'elements' }
    if ($Type -ne 'All') { $req['searchType'] = $Type }
    $r = Send-FlaUICommand $req
    if ($r.data) { return $r.data -split "`n" | Where-Object { $_ -ne '' } }
}

# ── Find ──────────────────────────────────────────────────────────────────────

function Find-FlaUIElement {
    <#
    .SYNOPSIS  Find a window and optionally an element within it.
    .PARAMETER Window  Window title (partial match, fuzzy).
    .PARAMETER Id      Element AutomationId.
    .PARAMETER Name    Element Name property (fallback if -Id not given).
    .PARAMETER Type    ControlType filter (e.g. Button).
    .EXAMPLE   Find-FlaUIElement -Window 'Notepad' -Name 'Text Editor' -Type Edit
    #>
    param(
        [Parameter(Mandatory)][string]$Window,
        [string]$Id,
        [string]$Name,
        [string]$Type
    )
    $req = @{ command = 'find'; window = $Window }
    if ($Id)   { $req['automationId'] = $Id }
    if ($Name) { $req['elementName']  = $Name }
    if ($Type) { $req['searchType']   = $Type }
    Send-FlaUICommand $req
}

# ── Execute ───────────────────────────────────────────────────────────────────

function Invoke-FlaUIAction {
    <#
    .SYNOPSIS  Execute an action on the current element.
    .PARAMETER Action  Action name (click, type, gettext, toggle, screenshot, …).
    .PARAMETER Value   Optional value (text to type, index, coordinates, …).
    .EXAMPLE   Invoke-FlaUIAction -Action type  -Value 'Hello World'
    .EXAMPLE   Invoke-FlaUIAction -Action click
    #>
    param(
        [Parameter(Mandatory)][string]$Action,
        [string]$Value
    )
    $req = @{ command = 'execute'; action = $Action }
    if ($PSBoundParameters.ContainsKey('Value')) { $req['value'] = $Value }
    Send-FlaUICommand $req
}

# ── OCR ───────────────────────────────────────────────────────────────────────

function Invoke-FlaUIOcr {
    <#
    .SYNOPSIS  OCR the current element (or a sub-region).
    .PARAMETER Region  Optional region as "x,y,width,height".
    .EXAMPLE   Invoke-FlaUIOcr
    .EXAMPLE   Invoke-FlaUIOcr -Region '0,0,300,50'
    #>
    param([string]$Region)
    $req = @{ command = 'ocr' }
    if ($Region) { $req['value'] = $Region }
    Send-FlaUICommand $req
}

# ── Capture ───────────────────────────────────────────────────────────────────

function Invoke-FlaUICapture {
    <#
    .SYNOPSIS  Capture a screenshot of the screen, current window, element, or multiple elements.
    .PARAMETER Target  screen | window | element (default) | elements.
    .PARAMETER Value   Comma-separated numeric element IDs (for -Target elements).
    .EXAMPLE   Invoke-FlaUICapture
    .EXAMPLE   Invoke-FlaUICapture -Target screen
    .EXAMPLE   Invoke-FlaUICapture -Target elements -Value '42,105'
    #>
    param(
        [ValidateSet('screen','window','element','elements')]
        [string]$Target = 'element',
        [string]$Value
    )
    $req = @{ command = 'capture'; action = $Target }
    if ($PSBoundParameters.ContainsKey('Value')) { $req['value'] = $Value }
    Send-FlaUICommand $req
}

# ── AI (Multimodal) ───────────────────────────────────────────────────────────

function Invoke-FlaUIAi {
    <#
    .SYNOPSIS  Send an AI sub-command to the multimodal LLM.
    .PARAMETER SubCommand  One of: init, status, describe, file, ask.
    .PARAMETER Model   Path to the LLM .gguf file (for init).
    .PARAMETER Proj    Path to the multimodal projector .gguf file (for init).
    .PARAMETER Prompt  Question or instruction (for describe, ask).
    .PARAMETER Value   File path (for file sub-command).
    .EXAMPLE   Invoke-FlaUIAi -SubCommand init  -Model C:\m\v.gguf -Proj C:\m\p.gguf
    .EXAMPLE   Invoke-FlaUIAi -SubCommand describe -Prompt 'What buttons do you see?'
    .EXAMPLE   Invoke-FlaUIAi -SubCommand file  -Value C:\screen.png -Prompt 'Any errors?'
    .EXAMPLE   Invoke-FlaUIAi -SubCommand ask   -Prompt 'Is there a dialog open?'
    #>
    param(
        [Parameter(Mandatory)][ValidateSet('init','status','describe','file','ask')]
        [string]$SubCommand,
        [string]$Model,
        [string]$Proj,
        [string]$Prompt,
        [string]$Value
    )
    $req = @{ command = 'ai'; action = $SubCommand }
    if ($PSBoundParameters.ContainsKey('Model'))  { $req['model']  = $Model }
    if ($PSBoundParameters.ContainsKey('Proj'))   { $req['proj']   = $Proj }
    if ($PSBoundParameters.ContainsKey('Prompt')) { $req['prompt'] = $Prompt }
    if ($PSBoundParameters.ContainsKey('Value'))  { $req['value']  = $Value }
    Send-FlaUICommand $req
}

# ── Exports ───────────────────────────────────────────────────────────────────

Export-ModuleMember -Function @(
    'Connect-FlaUI', 'Disconnect-FlaUI', 'Send-FlaUICommand',
    'Get-FlaUIWindows', 'Get-FlaUIStatus', 'Get-FlaUIHelp', 'Get-FlaUIElements',
    'Find-FlaUIElement', 'Invoke-FlaUIAction', 'Invoke-FlaUIOcr', 'Invoke-FlaUICapture',
    'Invoke-FlaUIAi'
)
