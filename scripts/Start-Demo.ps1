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
      5. starts the web app from a PRODUCTION BUILD, building it first if the build is missing or
         older than the frontend source. Production means every screen is compiled up front: no
         per-page compile on first visit, no dev overlay, no "8 Issues" badge in the corner.
         The build takes a few minutes, so run this (or -BuildWebOnly) the evening before.
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

.PARAMETER DevWeb
    Start the web app with "npm run dev" instead of the production build: the old behaviour, with
    hot reload and a compile on the first visit to each page. Use it during development, when you
    are changing frontend code and want to see the change without rebuilding.

.PARAMETER RebuildWeb
    Rebuild the production web bundle even if it looks current. Use when a build looks stale for a
    reason the source-file timestamps cannot see (a node_modules change, a doubtful earlier build).

.PARAMETER BuildWebOnly
    Build the production web bundle and stop. Touches nothing else: no scanner, no API, no
    personas. Run it the evening before so the morning bring-up finds the build ready.

.PARAMETER ShowWindows
    Start the API and the web app in their own visible windows, the way the four manual steps do,
    so you can watch their logs scroll. Without this they run minimised and their output goes to
    dev-harness\hr-demo-smoke\out\demo-api.log and demo-web.log, which is quieter for a demo but
    means nothing about them appears in your terminal.

.EXAMPLE
    powershell -File .\scripts\Start-Demo.ps1                # bring everything up (production web build)
    powershell -File .\scripts\Start-Demo.ps1 -BuildWebOnly  # the evening before: build the web app, nothing else
    powershell -File .\scripts\Start-Demo.ps1 -DevWeb        # bring it up with the dev-mode web app instead
    powershell -File .\scripts\Start-Demo.ps1 -Stop          # take everything down
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
    [switch]$ShowWindows,
    [switch]$DevWeb,
    [switch]$RebuildWeb,
    [switch]$BuildWebOnly
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$apiDir = Join-Path $repoRoot 'src\ErpSystem.Api'
$webDir = Join-Path $repoRoot 'frontend'
$dll = Join-Path $apiDir 'bin\Debug\net8.0\ErpSystem.Api.dll'
if (-not $HarnessDir) { $HarnessDir = Join-Path (Split-Path -Parent $repoRoot) 'dev-harness\hr-demo-smoke' }

# The production web build. next.config.js sets output: 'standalone', so "next build" emits a
# self-contained server under .next\standalone that the Dockerfile already runs in containers;
# this script runs the same thing on the laptop. BUILD_ID is written last, so its timestamp is
# the build's timestamp.
$webBuildDir = Join-Path $webDir '.next'
$webBuildId = Join-Path $webBuildDir 'BUILD_ID'
$webStandaloneDir = Join-Path $webBuildDir 'standalone'
$webBuildLog = Join-Path $HarnessDir 'out\demo-web-build.log'

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
    # Both shapes of the web app: dev mode (cmd running "npm run dev" and the node it spawns) and
    # the production standalone server (node running .next\standalone\server.js).
    Get-CimInstance Win32_Process -ErrorAction SilentlyContinue |
        Where-Object { ($_.Name -eq 'node.exe' -and ($_.CommandLine -like '*next*' -or $_.CommandLine -like '*standalone*server.js*')) -or
                       ($_.Name -eq 'cmd.exe'  -and ($_.CommandLine -like '*npm run dev*' -or $_.CommandLine -like '*npm run start*')) }
}
function Get-DevWebProcesses {
    # Only the dev-mode shape, for when production was asked for and a dev server is in the way.
    Get-WebProcesses | Where-Object { $_.CommandLine -match 'npm run dev|[\\/]next["'']?\s+dev(\s|$)' }
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

# ── the production web build ────────────────────────────────────────────────────────────────────
function Get-WebSourceStamp {
    # The newest write time across everything that feeds "next build". public\syncfusion is
    # excluded because the build's own prebuild step rewrites it, which would make every build
    # look stale the moment it finished; src\logs is a stray API log, not source.
    $newest = [datetime]::MinValue
    $roots = @('src', 'public', 'package.json', 'package-lock.json', 'next.config.js', 'tsconfig.json',
               'postcss.config.mjs', 'components.json', '.env', '.env.local', '.env.production') |
        ForEach-Object { Join-Path $webDir $_ } | Where-Object { Test-Path -LiteralPath $_ }
    $excluded = @((Join-Path $webDir 'public\syncfusion'), (Join-Path $webDir 'src\logs'))
    foreach ($root in $roots) {
        Get-ChildItem -LiteralPath $root -Recurse -File -ErrorAction SilentlyContinue |
            Where-Object { $f = $_.FullName; -not ($excluded | Where-Object { $f.StartsWith($_, [System.StringComparison]::OrdinalIgnoreCase) }) } |
            ForEach-Object { if ($_.LastWriteTimeUtc -gt $newest) { $newest = $_.LastWriteTimeUtc } }
    }
    return $newest
}
function Find-WebServer {
    # next build puts server.js at .next\standalone\server.js, or one folder down if it decided the
    # workspace root is above frontend\. Look for it rather than assume.
    if (-not (Test-Path -LiteralPath $webStandaloneDir)) { return $null }
    $direct = Join-Path $webStandaloneDir 'server.js'
    if (Test-Path -LiteralPath $direct) { return $direct }
    $found = Get-ChildItem -LiteralPath $webStandaloneDir -Filter server.js -Recurse -Depth 2 -File -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($found) { return $found.FullName }
    return $null
}
function Get-WebBuildState {
    # 'missing' | 'stale' | 'current', plus the reason, so the step line can say which.
    $server = Find-WebServer
    if (-not (Test-Path -LiteralPath $webBuildId) -or -not $server) {
        return @{ State = 'missing'; Reason = 'no production build yet' }
    }
    $built = (Get-Item -LiteralPath $webBuildId).LastWriteTimeUtc
    $source = Get-WebSourceStamp
    if ($source -gt $built) {
        return @{ State = 'stale'; Reason = ("frontend source changed {0:HH:mm} on {0:d MMM}, build is from {1:HH:mm} on {1:d MMM}" -f $source.ToLocalTime(), $built.ToLocalTime()) }
    }
    return @{ State = 'current'; Reason = ("built {0:HH:mm} on {0:d MMM}" -f $built.ToLocalTime()) }
}
function Invoke-WebBuild {
    # Foreground, output on screen AND in the log: a build is minutes long and a silent one looks
    # hung. stderr is merged by cmd, not PowerShell, because under $ErrorActionPreference = 'Stop'
    # a native command's stderr line would otherwise terminate the script.
    $dev = @(Get-DevWebProcesses)
    if ($dev.Count) {
        # A dev server shares .next with the build. Building under it corrupts both.
        $dev | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
        Start-Sleep -Seconds 2
        Write-Host "      stopped a dev-mode web app ($($dev.Count) process(es)) that was sharing the build folder" -ForegroundColor DarkGray
    }
    New-Item -ItemType Directory -Force (Split-Path $webBuildLog) | Out-Null
    if (Test-Path -LiteralPath $webBuildLog) { Remove-Item -LiteralPath $webBuildLog -Force }
    $started = Get-Date
    Write-Host ""
    Write-Host "      Building the production web app. This takes a few minutes; the output below is also in" -ForegroundColor DarkGray
    Write-Host "      $webBuildLog" -ForegroundColor DarkGray
    Write-Host ""
    # The bundler needs more heap than Node's default (about 4 GB): the first run on this laptop died
    # at 3.9 GB with "JavaScript heap out of memory" after four minutes. 8 GB is what
    # Deploy-RhemaVps.ps1 builds the same frontend with. Set for the build only, then put back.
    $savedNodeOptions = $env:NODE_OPTIONS
    $savedTelemetry = $env:NEXT_TELEMETRY_DISABLED
    $env:NODE_OPTIONS = '--max-old-space-size=8192'
    $env:NEXT_TELEMETRY_DISABLED = '1'
    # next build prints UTF-8 glyphs (the ▲ logo, ✓ ticks, the ○ ƒ ├ └ route markers). PowerShell
    # 5.1 decodes a native command's output with the console's legacy code page (850 here), which
    # turns ▲ into "Ôû▓" and ✓ into "Ô£ô" on screen and in the log. Decode as UTF-8 for the build
    # only, then put the console back.
    $savedConsoleEncoding = [Console]::OutputEncoding
    [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
    Push-Location $webDir
    try {
        # Tee-Object would write the log as UTF-16 under PowerShell 5.1, which reads as spaced-out
        # letters in most viewers; Add-Content with an explicit encoding does not.
        & cmd /c "npm run build 2>&1" | ForEach-Object {
            Write-Host "      $_" -ForegroundColor DarkGray
            Add-Content -LiteralPath $webBuildLog -Value $_ -Encoding UTF8
        }
        $exit = $LASTEXITCODE
    } finally {
        Pop-Location
        [Console]::OutputEncoding = $savedConsoleEncoding
        $env:NODE_OPTIONS = $savedNodeOptions
        $env:NEXT_TELEMETRY_DISABLED = $savedTelemetry
    }
    if ($exit -ne 0) {
        $outOfMemory = [bool](Select-String -LiteralPath $webBuildLog -Pattern 'heap out of memory' -Quiet -ErrorAction SilentlyContinue)
        if ($outOfMemory) {
            $freeGb = [math]::Round((Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory / 1MB, 1)
            throw ("The production web build ran out of memory even with an 8 GB heap ($freeGb GB of RAM is free right now). " +
                   "Close other applications (browsers, IDEs, the API) and run this again. Full output: $webBuildLog. " +
                   "If it still fails, bring the demo up with -DevWeb.")
        }
        throw "The production web build failed (exit $exit). Full output: $webBuildLog. Fix the build, or bring the demo up with -DevWeb."
    }
    $server = Find-WebServer
    if (-not $server) {
        throw "The build finished but no standalone server.js appeared under $webStandaloneDir. Is output: 'standalone' still set in frontend\next.config.js? Full output: $webBuildLog"
    }
    # The standalone folder does not include the static chunks or public\, by design (the
    # Dockerfile copies them in the same way). Replace, not merge, so nothing from an older build
    # lingers.
    $serverRoot = Split-Path -Parent $server
    foreach ($pair in @(
        @{ From = (Join-Path $webBuildDir 'static'); To = (Join-Path $serverRoot '.next\static') },
        @{ From = (Join-Path $webDir 'public');       To = (Join-Path $serverRoot 'public') })) {
        if (Test-Path -LiteralPath $pair.To) { Remove-Item -LiteralPath $pair.To -Recurse -Force }
        New-Item -ItemType Directory -Force (Split-Path -Parent $pair.To) | Out-Null
        Copy-Item -LiteralPath $pair.From -Destination $pair.To -Recurse -Force
    }
    Write-Host ""
    return ((Get-Date) - $started).TotalSeconds
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

# ── the evening before: just the web build ──────────────────────────────────────────────────────
if ($BuildWebOnly) {
    Write-Host ""
    Write-Host "  RHEMA ERP - building the production web app" -ForegroundColor Cyan
    Write-Host ""
    $state = Get-WebBuildState
    if ($state.State -eq 'current' -and -not $RebuildWeb) {
        Write-Host "    Already current ($($state.Reason)). Nothing to do; add -RebuildWeb to build anyway." -ForegroundColor Green
        Write-Host ""
        exit 0
    }
    Write-Host "    $($state.Reason)" -ForegroundColor DarkGray
    $seconds = Invoke-WebBuild
    Write-Host ("    Built in {0:n0} s. The morning bring-up will find it ready." -f $seconds) -ForegroundColor Green
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
$webMode = 'production'
if ($DevWeb) { $webMode = 'dev' }
if (-not $SkipWeb) {
    Write-Step "Web app (port $WebPort, $webMode)"
    $devRunning = @(Get-DevWebProcesses)
    if ((Test-Responding "http://localhost:$WebPort") -and ($DevWeb -or $devRunning.Count -eq 0)) {
        # Whatever is answering is the mode that was asked for (or dev was asked for, and anything
        # answering will do). Leave it.
        Write-Note "already running; left alone"
    } else {
        if ((Test-Responding "http://localhost:$WebPort") -and $devRunning.Count) {
            # Production was asked for and a dev server is answering. That is the slow mode the
            # audience must not see, so it is replaced, not left alone.
            $devRunning | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
            Start-Sleep -Seconds 3
            Write-Note "replaced a dev-mode web app"
            Write-Host ("  " + "".PadRight(58, ' ')) -NoNewline
        }
        $webLog = Join-Path $HarnessDir 'out\demo-web.log'
        $buildSeconds = 0
        if ($DevWeb) {
            $webStart = @{ FilePath = 'cmd'; ArgumentList = @('/c', 'npm run dev'); WorkingDirectory = $webDir; PassThru = $true }
        } else {
            $state = Get-WebBuildState
            if ($RebuildWeb -or $state.State -ne 'current') {
                if ($RebuildWeb) { Write-Note "rebuild requested" } else { Write-Note $state.Reason }
                $buildSeconds = Invoke-WebBuild
                Write-Host ("  " + "".PadRight(58, ' ')) -NoNewline
            }
            $server = Find-WebServer
            # server.js reads PORT and HOSTNAME, exactly as the Dockerfile sets them. 0.0.0.0 matches
            # what "npm run dev" binds, so a second laptop on the LAN can still reach it.
            $env:PORT = "$WebPort"
            $env:HOSTNAME = '0.0.0.0'
            $env:NODE_ENV = 'production'
            $webStart = @{ FilePath = 'node'; ArgumentList = "`"$server`""; WorkingDirectory = (Split-Path -Parent $server); PassThru = $true }
        }
        $started = Get-Date
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
        if ($buildSeconds -gt 0) {
            Write-Ok ("built in {0:n0} s, ready in {1:n0} s" -f $buildSeconds, ((Get-Date) - $started).TotalSeconds)
        } else {
            Write-Ok ("ready in {0:n0} s" -f ((Get-Date) - $started).TotalSeconds)
        }
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
if ($DevWeb) {
    Write-Host "    The web app is in dev mode. Open the first few screens now, before the audience" -ForegroundColor DarkGray
    Write-Host "    arrives - the first visit to each page compiles it and takes a second or two." -ForegroundColor DarkGray
} elseif (-not $SkipWeb) {
    Write-Host "    The web app is the production build: every screen is compiled already. Open the" -ForegroundColor DarkGray
    Write-Host "    dashboard once anyway - it gathers its figures from every module on first open." -ForegroundColor DarkGray
    Write-Host "    Changed frontend code since? Re-run this script; it rebuilds when the source is newer." -ForegroundColor DarkGray
}
Write-Host ""
Write-Host "    Afterwards:  powershell -File .\scripts\Start-Demo.ps1 -Stop" -ForegroundColor Cyan
Write-Host ""
