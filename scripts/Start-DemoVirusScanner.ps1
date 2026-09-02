<#
.SYNOPSIS
    Starts a stand-in malware scanner on 127.0.0.1:3310 so document uploads work on a demo laptop.

.DESCRIPTION
    !! READ THIS BEFORE USING IT.

    This is NOT a virus scanner. It answers "clean" to every file it is shown. It exists for one
    reason: the controlled-upload gate makes a clean malware scan MANDATORY for all 29 HR upload
    categories -- employee documents, policy documents, medical claims, offer letters, separation
    files, SHE controlled documents, every one -- and a tenant policy cannot opt out of it. That is
    the correct design. The consequence is that on a machine with no ClamAV daemon, every document
    upload in the HR module is refused with:

        "Upload rejected because a clean virus-scan result was not obtained."

    A demonstration laptop rarely has ClamAV installed, so without this the whole document story --
    attaching a policy, uploading a certificate, adding evidence to a case -- cannot be shown at all.

    !! WHAT YOU MUST NOT SAY ON STAGE
    Do not tell an audience that files are being scanned while this is running. They are not. If
    somebody asks, the honest and perfectly good answer is: "Scanning is mandatory in the product and
    cannot be switched off per tenant -- every one of these upload types requires a clean result
    before the file is stored. On this laptop the scanner is stubbed so we can show you the flow; a
    real deployment points the same gate at a ClamAV daemon."

    !! NEVER RUN THIS ANYWHERE REAL
    Not on a shared server, not on UAT-for-users, not on anything reachable from a network. It makes
    the malware boundary answer yes to everything.

    THE REAL THING
    Install ClamAV and run clamd on 127.0.0.1:3310 and this script becomes unnecessary -- the API
    talks to whatever is listening on that port. Host and port are configurable under
    FileVirusScan:ClamAv (Host, Port, ConnectTimeoutSeconds, ScanTimeoutSeconds).

.PARAMETER Port
    Port to listen on. 3310 is the ClamAV default and what the API expects.

.PARAMETER Stop
    Stops a stub started by this script instead of starting one.

.EXAMPLE
    powershell -File ./scripts/Start-DemoVirusScanner.ps1
    powershell -File ./scripts/Start-DemoVirusScanner.ps1 -Stop
#>
[CmdletBinding()]
param(
    [int]$Port = 3310,
    [switch]$Stop
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$stub = Join-Path $repoRoot 'scripts\demo-virus-scanner-stub.mjs'

if (-not (Test-Path $stub)) { throw "Stub not found at $stub." }

function Get-StubProcesses {
    Get-CimInstance Win32_Process -Filter "Name='node.exe'" |
        Where-Object { $_.CommandLine -like '*demo-virus-scanner-stub.mjs*' -or $_.CommandLine -like '*clamd-stub.mjs*' }
}

if ($Stop) {
    $running = Get-StubProcesses
    if (-not $running) { Write-Host "  No scanner stub running." -ForegroundColor DarkGray; return }
    $running | ForEach-Object {
        Write-Host "  Stopping stub PID $($_.ProcessId)" -ForegroundColor Yellow
        Stop-Process -Id $_.ProcessId -Force
    }
    return
}

# Already answering? Then there is nothing to do -- and it might be a REAL clamd, which must not be
# disturbed. Probe with the protocol rather than assuming, and say which one it is.
$listening = $false
try {
    $client = New-Object System.Net.Sockets.TcpClient
    $client.Connect('127.0.0.1', $Port)
    $stream = $client.GetStream()
    $ping = [Text.Encoding]::ASCII.GetBytes("PING`0")
    $stream.Write($ping, 0, $ping.Length); $stream.Flush()
    Start-Sleep -Milliseconds 400
    $buffer = New-Object byte[] 64
    $read = $stream.Read($buffer, 0, 64)
    $reply = ([Text.Encoding]::ASCII.GetString($buffer, 0, $read)) -replace "`0", ""
    $client.Close()
    $listening = $reply -match 'PONG'
} catch { $listening = $false }

if ($listening) {
    $stubProc = Get-StubProcesses | Select-Object -First 1
    if ($stubProc) {
        Write-Host "  A STUB scanner is already answering on 127.0.0.1:$Port (PID $($stubProc.ProcessId))." -ForegroundColor Yellow
        Write-Host "  Uploads will work. Nothing is being scanned." -ForegroundColor Yellow
    } else {
        Write-Host "  Something already answers ClamAV PING on 127.0.0.1:$Port and it is NOT this stub." -ForegroundColor Green
        Write-Host "  That is probably a real clamd. Leaving it alone." -ForegroundColor Green
    }
    return
}

Write-Host ""
Write-Host "  Starting the DEMO scanner stub on 127.0.0.1:$Port" -ForegroundColor Cyan
Write-Host "  !! It reports every file as clean. Nothing is scanned." -ForegroundColor Yellow
Write-Host "  !! Demo laptops only. Never on a shared or networked machine." -ForegroundColor Yellow

# Windows PowerShell 5.1 has no -Environment on Start-Process, so the port is handed over through
# the parent's environment, which the child inherits. The stub reads CLAMD_PORT.
$env:CLAMD_PORT = "$Port"
# The repository path contains spaces, so the script path must reach node as ONE quoted argument.
# Unquoted, node is handed "D:\Rhema\TDC" and exits with "Cannot find module" before it ever listens.
Start-Process node -ArgumentList "`"$stub`"" -WindowStyle Minimized

for ($i = 0; $i -lt 20; $i++) {
    Start-Sleep -Milliseconds 500
    try {
        $c = New-Object System.Net.Sockets.TcpClient
        $c.Connect('127.0.0.1', $Port)
        $c.Close()
        Write-Host "  Ready. HR document uploads will now be accepted." -ForegroundColor Green
        Write-Host ""
        return
    } catch { }
}

throw "The stub did not start listening on port $Port."
