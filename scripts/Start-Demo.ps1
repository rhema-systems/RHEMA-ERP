<#
.SYNOPSIS
    Brings the whole HR and SHE demonstration up with one command, and proves it is right.

.DESCRIPTION
    Demo-day bring-up used to be four commands across three windows, in an order that mattered,
    plus a check. That is four chances to be on the wrong database at nine in the morning with an
    audience in the room -- and on 7 September exactly that happened. This does the lot and refuses
    to say READY unless every part of it is provably correct:

      1. stops any API already running (including one with no window, which is the one that bites)
      2. starts the document scanner stub on 3310, without which every HR upload is refused
      3. starts the API against the demo database, in Staging so refusals read as refusals
      4. PROVES which database that API is serving -- not by counting rows, which no longer
         discriminates, but by asking it for an employee id that exists only in the demo database
      5. starts the web app
      6. signs in as all nine demo personas

    Anything that fails stops the script with the one command that fixes it. Nothing is left
    half-up without you being told.

    Re-running is safe: each step checks whether that piece is already up and correct, and leaves it
    alone if it is.

    The manual four-step version is still in Book 0 section 3, and still works. Use it if you would
    rather see each piece start, or if this script reports something you want to unpick by hand.

.PARAMETER Stop
    Shut everything down again: web app, API, scanner. Use after the demo.

.PARAMETER Database
    Demo database. Defaults to ErpSystemDB_UAT.

.PARAMETER SkipWeb
    Do not start the web app (it is already running, or you only want the API).

.PARAMETER ShowWindows
    Start the API and the web app in their own visible windows, the way the four manual steps do,
    so you can watch their logs scroll. Without this they run minimised and their output goes to
    dev-harness\hr-demo-smoke\out\demo-api.log and demo-web.log, which is quieter for a demo but
    means nothing about them appears in your terminal.

.EXAMPLE
    powershell -File .\scripts\Start-Demo.ps1          # bring everything up
    powershell -File .\scripts\Start-Demo.ps1 -Stop    # take everything down
#>
[CmdletBinding()]
param(
    [switch]$Stop,
    [string]$Database = 'ErpSystemDB_UAT',
    [int]$Port = 5000,
    [int]$WebPort = 3000,
    [string]$Server = '.',
    [string]$UserId = 'sa',
    [string]$Password,
    [string]$HarnessDir,
    [switch]$SkipWeb,
    [switch]$SkipPersonas,
    [switch]$ShowWindows
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$apiDir = Join-Path $repoRoot 'src\ErpSystem.Api'
$webDir = Join-Path $repoRoot 'frontend'
$dll = Join-Path $apiDir 'bin\Debug\net8.0\ErpSystem.Api.dll'
if (-not $HarnessDir) { $HarnessDir = Join-Path (Split-Path -Parent $repoRoot) 'dev-harness\hr-demo-smoke' }

$stepNo = 0
$totalSteps = 6
if ($SkipWeb) { $totalSteps-- }
if ($SkipPersonas) { $totalSteps-- }

function Write-Step([string]$Label) {
    $script:stepNo++
    $line = "  [$script:stepNo/$totalSteps] $Label "
    Write-Host ($line.PadRight(58, '.')) -NoNewline
}
function Write-Ok([string]$Detail) { Write-Host " $Detail" -ForegroundColor Green }
function Write-Note([string]$Detail) { Write-Host " $Detail" -ForegroundColor DarkGray }
function Write-Bad([string]$Detail) { Write-Host " $Detail" -ForegroundColor Red }

function Get-ApiProcesses {
    Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" -ErrorAction SilentlyContinue |
        Where-Object { $_.CommandLine -like '*ErpSystem.Api*' }
}
function Get-WebProcesses {
    Get-CimInstance Win32_Process -ErrorAction SilentlyContinue |
        Where-Object { ($_.Name -eq 'node.exe' -and $_.CommandLine -like '*next*') -or
                       ($_.Name -eq 'cmd.exe'  -and $_.CommandLine -like '*npm run dev*') }
}
function Test-PortListening([int]$P) {
    return [bool](Get-NetTCPConnection -LocalPort $P -State Listen -ErrorAction SilentlyContinue)
}
function Test-Responding([string]$Url) {
    try { $null = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 5; return $true }
    catch {
        if ($_.Exception.PSObject.Properties['Response'] -and $_.Exception.Response) { return $true }
        return $false
    }
}

# ── shutting down ───────────────────────────────────────────────────────────────────────────────
if ($Stop) {
    Write-Host ""
    Write-Host "  Shutting the demonstration down" -ForegroundColor Cyan
    Write-Host ""

    $web = @(Get-WebProcesses)
    if ($web.Count) { $web | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }; Write-Host "    web app  stopped ($($web.Count) process(es))" }
    else { Write-Host "    web app  was not running" -ForegroundColor DarkGray }

    $api = @(Get-ApiProcesses)
    if ($api.Count) { $api | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }; Write-Host "    API      stopped ($($api.Count) process(es))" }
    else { Write-Host "    API      was not running" -ForegroundColor DarkGray }

    & powershell -NoProfile -File (Join-Path $PSScriptRoot 'Start-DemoVirusScanner.ps1') -Stop | Out-Null
    Write-Host "    scanner  stopped"

    Start-Sleep -Seconds 2
    Write-Host ""
    foreach ($p in @($WebPort, $Port, 3310)) {
        $state = 'closed'
        if (Test-PortListening $p) { $state = 'STILL IN USE' }
        Write-Host ("    port {0,-5} {1}" -f $p, $state)
    }
    Write-Host ""
    exit 0
}

# ── bringing it up ──────────────────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "  RHEMA ERP - HR and SHE demonstration bring-up" -ForegroundColor Cyan
Write-Host "  Database: $Database" -ForegroundColor DarkGray
Write-Host ""

if (-not (Test-Path $dll)) {
    Write-Bad ''
    throw "Build output not found at $dll. Build the solution, then run this again."
}

. (Join-Path $PSScriptRoot 'ErpDbCredential.ps1')
$credential = Resolve-ErpDbCredential -UserId $UserId -Password $Password -RepoRoot $repoRoot
$UserId = $credential.UserId
$Password = $credential.Password

# Step 0 (silent): is there a demo dataset in there at all? Better to say so now than to have the
# presenter discover empty screens.
$staff = & sqlcmd -S $Server -d $Database -U $UserId -P $Password -C -I -b -h -1 -W -Q `
    "SET NOCOUNT ON; SELECT COUNT(*) FROM Employees WHERE IsDeleted = 0 AND EmployeeNumber LIKE 'TDC/%'" 2>$null
if ($LASTEXITCODE -ne 0) {
    throw "Could not reach '$Database' on '$Server'. Has it been built? See Book 0 section 2."
}
$staffCount = 0
if ($staff) { [int]::TryParse(($staff | Select-Object -First 1).Trim(), [ref]$staffCount) | Out-Null }
if ($staffCount -lt 100) {
    throw ("'$Database' holds only $staffCount TDC staff, so the demonstration data is not in it. " +
           "Rebuild it: powershell -File .\scripts\New-UatDatabase.ps1")
}

# ── 1. clear the decks ──────────────────────────────────────────────────────────────────────────
Write-Step "Clearing anything already running"
$existingApi = @(Get-ApiProcesses)
if ($existingApi.Count) {
    $existingApi | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
    Start-Sleep -Seconds 3
    Write-Ok "stopped $($existingApi.Count) API process(es)"
} else {
    Write-Note "nothing to stop"
}
if (Test-PortListening $Port) {
    Write-Host ""
    throw "Port $Port is still in use by something that is not an ErpSystem API. Free it, then run this again."
}

# ── 2. the document scanner ─────────────────────────────────────────────────────────────────────
Write-Step "Document scanner (port 3310)"
if (Test-PortListening 3310) {
    Write-Note "already answering; left alone"
} else {
    & powershell -NoProfile -File (Join-Path $PSScriptRoot 'Start-DemoVirusScanner.ps1') | Out-Null
    $deadline = (Get-Date).AddSeconds(20)
    while (-not (Test-PortListening 3310) -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 500 }
    if (-not (Test-PortListening 3310)) { Write-Bad "did not start"; throw "The scanner stub did not come up on 3310. Without it every HR document upload is refused." }
    Write-Ok "started"
}

# ── 3. the API ──────────────────────────────────────────────────────────────────────────────────
Write-Step "API on $Database (port $Port)"
if (-not $env:JwtSettings__SecretKey) {
    Push-Location $apiDir
    try { $line = (dotnet user-secrets list | Select-String '^JwtSettings:SecretKey = ') } finally { Pop-Location }
    if (-not $line) { Write-Bad "no signing key"; throw "JwtSettings:SecretKey is not in user-secrets; without it every login fails. Set `$env:JwtSettings__SecretKey yourself." }
    $env:JwtSettings__SecretKey = $line.ToString().Substring($line.ToString().IndexOf(' = ') + 3)
}
$apiLog = Join-Path $HarnessDir 'out\demo-api.log'
$apiErr = Join-Path $HarnessDir 'out\demo-api.err.log'
New-Item -ItemType Directory -Force (Split-Path $apiLog) | Out-Null
$env:ConnectionStrings__DefaultConnection = "Server=$Server;Database=$Database;User Id=$UserId;Password=$Password;TrustServerCertificate=True;Encrypt=False;MultipleActiveResultSets=True"
$env:ASPNETCORE_ENVIRONMENT = 'Staging'
$env:ASPNETCORE_URLS = "http://localhost:$Port"
$started = Get-Date
# Two ways to run it. Minimised with both streams to files is the default: quiet, and a crash is
# still readable afterwards. -ShowWindows gives it a visible window instead, like the manual steps,
# for when you want to watch the request log during a rehearsal -- you cannot have both, because
# redirected output does not also reach the window.
$apiStart = @{ FilePath = 'dotnet'; ArgumentList = "`"$dll`""; WorkingDirectory = $apiDir; PassThru = $true }
if ($ShowWindows) {
    $apiStart.WindowStyle = 'Normal'
} else {
    $apiStart.WindowStyle = 'Minimized'
    $apiStart.RedirectStandardOutput = $apiLog
    $apiStart.RedirectStandardError = $apiErr
}
$apiProcess = Start-Process @apiStart
$health = "http://localhost:$Port/health"
$deadline = (Get-Date).AddSeconds(300)
while (-not (Test-Responding $health) -and (Get-Date) -lt $deadline) {
    if ($apiProcess.HasExited) {
        Write-Bad "it exited during start-up"
        if ($ShowWindows) {
            throw "The API exited before it was ready. Its window has the reason; re-run without -ShowWindows to capture it to a file."
        }
        Get-Content $apiLog -ErrorAction SilentlyContinue | Select-Object -Last 6 | ForEach-Object { Write-Host "        $_" -ForegroundColor DarkRed }
        Get-Content $apiErr -ErrorAction SilentlyContinue | Select-Object -Last 10 | ForEach-Object { Write-Host "        $_" -ForegroundColor DarkRed }
        throw "The API exited before it was ready. Full output: $apiLog"
    }
    Start-Sleep -Seconds 2
}
if (-not (Test-Responding $health)) { Write-Bad "no answer after 5 minutes"; throw "The API did not come up. See $apiLog" }
Write-Ok ("started, ready in {0:n0} s" -f ((Get-Date) - $started).TotalSeconds)

# ── 4. prove the database ───────────────────────────────────────────────────────────────────────
Write-Step "Which database is it really serving?"
. (Join-Path $PSScriptRoot 'ErpApiProbe.ps1')
$probe = Test-ErpApiServesDatabase -Port $Port -Database $Database -Server $Server -UserId $UserId -Password $Password
if (-not $probe.Serves) {
    Write-Bad "NOT $Database"
    Write-Host "        $($probe.Reason)" -ForegroundColor Yellow
    throw "The API on port $Port is not serving $Database. Do not demonstrate from this."
}
Write-Ok "$Database - correct"

# ── 5. the web app ──────────────────────────────────────────────────────────────────────────────
if (-not $SkipWeb) {
    Write-Step "Web app (port $WebPort)"
    if (Test-Responding "http://localhost:$WebPort") {
        Write-Note "already running; left alone"
    } else {
        $webLog = Join-Path $HarnessDir 'out\demo-web.log'
        $started = Get-Date
        $webStart = @{ FilePath = 'cmd'; ArgumentList = @('/c', 'npm run dev'); WorkingDirectory = $webDir; PassThru = $true }
        if ($ShowWindows) {
            $webStart.WindowStyle = 'Normal'
        } else {
            $webStart.WindowStyle = 'Minimized'
            $webStart.RedirectStandardOutput = $webLog
        }
        $null = Start-Process @webStart
        $deadline = (Get-Date).AddSeconds(180)
        while (-not (Test-Responding "http://localhost:$WebPort") -and (Get-Date) -lt $deadline) { Start-Sleep -Seconds 2 }
        if (-not (Test-Responding "http://localhost:$WebPort")) { Write-Bad "did not come up"; throw "The web app did not start. See $webLog" }
        Write-Ok ("ready in {0:n0} s" -f ((Get-Date) - $started).TotalSeconds)
    }
}

# ── 6. the personas ─────────────────────────────────────────────────────────────────────────────
$personaNote = ''
if (-not $SkipPersonas) {
    Write-Step "The nine demo logins"
    if (-not (Test-Path (Join-Path $HarnessDir 'personas.mjs'))) {
        Write-Note "personas.mjs not found; skipped"
    } elseif (-not (Get-Command node -ErrorAction SilentlyContinue)) {
        Write-Note "node not on PATH; skipped"
    } else {
        Push-Location $HarnessDir
        try { $out = & node personas.mjs 2>&1 } finally { Pop-Location }
        $ok = @($out | Select-String 'login OK').Count
        $failed = @($out | Select-String 'LOGIN FAILED').Count
        if ($ok -eq 9 -and $failed -eq 0) {
            Write-Ok "all nine sign in"
        } else {
            Write-Bad "$ok of 9 signed in, $failed failed"
            $out | Select-Object -Last 12 | ForEach-Object { Write-Host "        $_" -ForegroundColor DarkRed }
            $personaNote = "  !! Only $ok of the nine personas can sign in. The permission demonstration will not work."
        }
    }
}

# ── the verdict ─────────────────────────────────────────────────────────────────────────────────
Write-Host ""
if ($personaNote) {
    Write-Host "  NOT READY" -ForegroundColor Red
    Write-Host $personaNote -ForegroundColor Red
    Write-Host "  Rebuild the database (Book 0 section 2) or investigate before you present." -ForegroundColor Yellow
    Write-Host ""
    exit 1
}

Write-Host "  READY" -ForegroundColor Green
Write-Host ""
Write-Host "    Open        http://localhost:$WebPort"
Write-Host "    Sign in as  hr.head  /  Demo123!        (Book 0 section 1 lists all nine)"
Write-Host "    Books       $HarnessDir\runbook\"
Write-Host ""
if ($ShowWindows) {
    Write-Host "    The API and the web app have windows of their own - their logs scroll there." -ForegroundColor DarkGray
} else {
    Write-Host "    The API and the web app run minimised, so nothing more of theirs appears here." -ForegroundColor DarkGray
    Write-Host "    Their logs are being written to:" -ForegroundColor DarkGray
    Write-Host "      API      $apiLog" -ForegroundColor DarkGray
    Write-Host "      Web app  $(Join-Path $HarnessDir 'out\demo-web.log')" -ForegroundColor DarkGray
    Write-Host "    Watch one live in another window:" -ForegroundColor DarkGray
    Write-Host "      Get-Content `"$apiLog`" -Tail 20 -Wait" -ForegroundColor DarkGray
    Write-Host "    Or start with -ShowWindows next time to have them scroll in their own windows." -ForegroundColor DarkGray
}
Write-Host ""
Write-Host "    Open the first few screens now, before the audience arrives - the first visit to" -ForegroundColor DarkGray
Write-Host "    each page compiles it and takes a second or two." -ForegroundColor DarkGray
Write-Host ""
Write-Host "    Afterwards:  powershell -File .\scripts\Start-Demo.ps1 -Stop" -ForegroundColor Cyan
Write-Host ""
