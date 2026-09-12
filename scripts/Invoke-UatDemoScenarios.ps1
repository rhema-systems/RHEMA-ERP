<#
.SYNOPSIS
    Runs the transactional demo scenarios and the coverage checks against a demo database, bringing
    up (and afterwards taking down) whatever they need: the API and the scanner stub.

.DESCRIPTION
    The demonstration dataset has two layers. New-UatDatabase.ps1 installs the first with EF
    seeders: the organisation, the workforce, the lookups and the catalogues. The second layer is
    transactional -- leave requests, requisitions, cases, claims, anything with a number from a
    sequence or a workflow instance -- and that layer is built THROUGH THE API as the demo personas
    by dev-harness/hr-demo-smoke/scenarios.mjs, because a row written around the service is a row
    the screens then read wrongly.

    Before 2026-09-04 the second step was a separate manual command, and it was skipped: the
    database was rebuilt, the runbooks named records the scenarios create, and nothing had created
    them. This script exists so that can no longer happen -- New-UatDatabase.ps1 calls it, and it
    fails loudly when the scenarios or the coverage check fail.

    What it does, in order:
      1. Starts the scanner stub on 3310 unless something already answers there (uploads are
         scan-mandatory for every HR category; without it every document door returns 422).
      2. Starts the API against -Database on -Port in the Staging environment unless the port is
         already serving /health (in which case it assumes YOU started it against the right
         database -- it cannot tell).
      3. node scenarios.mjs        (every module under scenarios/, in order)
      4. node verify-tables.mjs    (every REQUIRED table in demo-coverage-manifest.csv holds a row)
      5. node verify-runbook.mjs   (every record the runbooks name exists), when the file exists
      6. Stops the API and the scanner stub IF it started them.

.PARAMETER Database
    The demo database. Defaults to ErpSystemDB_UAT. The development database is refused.

.PARAMETER Port
    Port for the API it starts. 5000 is what the frontend and the harness expect; pass another
    (e.g. 5010) to run alongside a development API.

.PARAMETER HarnessDir
    Where scenarios.mjs lives. Defaults to ..\dev-harness\hr-demo-smoke beside the repository.

.PARAMETER SkipVerify
    Run the scenarios but not the checks.

.PARAMETER VerifyOnly
    Run only the two checks (no scanner, no API, no scenarios). New-UatDatabase.ps1 uses this after
    its second 'seed-hr-demo' pass, so the verdict covers the tables that pass fills.

.EXAMPLE
    powershell -File ./scripts/Invoke-UatDemoScenarios.ps1
    powershell -File ./scripts/Invoke-UatDemoScenarios.ps1 -Database ErpSystemDB_DEMO2 -Port 5010
#>
[CmdletBinding()]
param(
    [string]$Database = 'ErpSystemDB_UAT',
    [int]$Port = 5000,
    [string]$HarnessDir,
    [string]$Server = '.',
    [string]$UserId = 'sa',
    [string]$Password,
    [switch]$SkipVerify,
    [switch]$VerifyOnly
)

$ErrorActionPreference = 'Stop'

if ($Database -eq 'ErpSystemDB') {
    throw "REFUSED: '$Database' is the development database. The scenarios create demo records; run them only on a demo database."
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$apiDir = Join-Path $repoRoot 'src\ErpSystem.Api'
$dll = Join-Path $apiDir 'bin\Debug\net8.0\ErpSystem.Api.dll'
if (-not (Test-Path $dll)) { throw "Build output not found at $dll. Build the solution first." }

# The credential is never written down here. It comes from -Password, then ERP_DB_PASSWORD, then
# the gitignored appsettings.json the API itself uses. See scripts/ErpDbCredential.ps1.
. (Join-Path $PSScriptRoot 'ErpDbCredential.ps1')
$credential = Resolve-ErpDbCredential -UserId $UserId -Password $Password -RepoRoot $repoRoot
$UserId = $credential.UserId
$Password = $credential.Password

if (-not $HarnessDir) { $HarnessDir = Join-Path (Split-Path -Parent $repoRoot) 'dev-harness\hr-demo-smoke' }
if (-not (Test-Path (Join-Path $HarnessDir 'scenarios.mjs'))) { throw "scenarios.mjs not found under '$HarnessDir'. Pass -HarnessDir." }

if (-not (Get-Command node -ErrorAction SilentlyContinue)) { throw "node is not on PATH; the scenarios are Node scripts." }

# "Is the API answering?", NOT "is the API healthy?".
#
# /health aggregates every registered check, and it reports 503 whenever the file-virus-scanner
# check cannot reach ClamAV on 3310 -- which is most of the time on a machine that is not mid-demo.
# Gating readiness on a 200 therefore made this script refuse an API that was serving perfectly
# (logins and every endpoint answering) and time out waiting for one it had just started. Any HTTP
# response at all means Kestrel is up and the pipeline is running, which is the only thing the
# steps below need to know.
function Test-Http([string]$Url) {
    try {
        $null = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 5
        return $true
    }
    catch {
        # An HTTP error response carries a Response object; a refused connection does not.
        if ($_.Exception.PSObject.Properties['Response'] -and $_.Exception.Response) { return $true }
        return $false
    }
}
function Test-PortListening([int]$P) {
    return [bool](Get-NetTCPConnection -LocalPort $P -State Listen -ErrorAction SilentlyContinue)
}

$startedScanner = $false
$apiProcess = $null

$scenarioFailures = 0
$coverageGaps = 0
$runbookGaps = 0

try {
  if ($VerifyOnly) {
    Write-Host "  -VerifyOnly: running the two checks against $Database without touching the API." -ForegroundColor DarkGray
  } else {
    # ── 1. scanner stub ─────────────────────────────────────────────────────────────────────────
    if (Test-PortListening 3310) {
        Write-Host "  -> Something already answers on 3310 (clamd or the stub); leaving it alone." -ForegroundColor DarkGray
    } else {
        Write-Host "  -> Starting the demo scanner stub on 3310 (document uploads are scan-mandatory)" -ForegroundColor Green
        & powershell -NoProfile -File (Join-Path $PSScriptRoot 'Start-DemoVirusScanner.ps1') | Out-Null
        $deadline = (Get-Date).AddSeconds(20)
        while (-not (Test-PortListening 3310) -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 500 }
        if (-not (Test-PortListening 3310)) { throw "The scanner stub did not come up on 3310." }
        $startedScanner = $true
    }

    # ── 2. the API ──────────────────────────────────────────────────────────────────────────────
    $health = "http://localhost:$Port/health"
    if (Test-Http $health) {
        Write-Host "  -> An API is already serving $health; using it. (It must be pointed at $Database -- this script cannot check.)" -ForegroundColor Yellow
    } else {
        if (Test-PortListening $Port) { throw "Port $Port is in use but /health does not answer. Stop whatever holds it, or pass -Port." }

        # Staging has no user-secrets, so the JWT signing key has to be passed in or every login
        # 400s with 'IDX10703: key length is zero' -- the same lookup Start-ErpApi.ps1 does.
        if (-not $env:JwtSettings__SecretKey) {
            Push-Location $apiDir
            try {
                $line = (dotnet user-secrets list | Select-String '^JwtSettings:SecretKey = ')
            } finally { Pop-Location }
            if (-not $line) { throw "JwtSettings:SecretKey not found in user-secrets; set JwtSettings__SecretKey yourself." }
            $env:JwtSettings__SecretKey = $line.ToString().Substring($line.ToString().IndexOf(' = ') + 3)
        }

        Write-Host "  -> Starting the API against $Database on port $Port (Staging)" -ForegroundColor Green
        $env:ConnectionStrings__DefaultConnection = "Server=$Server;Database=$Database;User Id=$UserId;Password=$Password;TrustServerCertificate=True;Encrypt=False;MultipleActiveResultSets=True"
        $env:ASPNETCORE_ENVIRONMENT = 'Staging'
        $env:ASPNETCORE_URLS = "http://localhost:$Port"
        $log = Join-Path $HarnessDir 'out\api-rebuild.log'
        New-Item -ItemType Directory -Force (Split-Path $log) | Out-Null
        # Start-Process inherits the environment set above. Output goes to a file so a crash is
        # readable afterwards rather than lost with the window.
        $apiProcess = Start-Process dotnet -ArgumentList "`"$dll`"" -WorkingDirectory $apiDir -WindowStyle Minimized -RedirectStandardOutput $log -PassThru

        $deadline = (Get-Date).AddSeconds(150)
        while (-not (Test-Http $health) -and (Get-Date) -lt $deadline) {
            if ($apiProcess.HasExited) { throw "The API exited before it was healthy. See $log" }
            Start-Sleep -Seconds 2
        }
        if (-not (Test-Http $health)) { throw "The API did not answer $health within 150 s. See $log" }
        Write-Host "     API is up." -ForegroundColor DarkGray
    }
  }

    # ── 3. scenarios ────────────────────────────────────────────────────────────────────────────
    $env:DEMO_API = "http://localhost:$Port/api"
    $env:DEMO_DB = $Database
    $env:DEMO_SQL = $Server
    $env:ERP_DB_USER = $UserId
    $env:ERP_DB_PASSWORD = $Password

    Push-Location $HarnessDir
    try {
        if (-not $VerifyOnly) {
            Write-Host ""
            Write-Host "  -> Running the transactional scenarios as the demo personas" -ForegroundColor Green
            & node scenarios.mjs 2>&1 | Tee-Object -FilePath (Join-Path $HarnessDir 'out\scenarios-last-run.log') | ForEach-Object { Write-Host "     $_" }
            $scenarioFailures = $LASTEXITCODE
            if ($scenarioFailures -ne 0) { Write-Host "  !! $scenarioFailures scenario(s) failed -- see above." -ForegroundColor Red }
        }

        if (-not $SkipVerify) {
            Write-Host ""
            Write-Host "  -> Coverage: every required HR/SHE table holds data?" -ForegroundColor Green
            & node verify-tables.mjs 2>&1 | ForEach-Object { Write-Host "     $_" }
            $coverageGaps = $LASTEXITCODE

            $runbookGaps = 0
            if (Test-Path (Join-Path $HarnessDir 'verify-runbook.mjs')) {
                Write-Host ""
                Write-Host "  -> Runbooks: every record the books name exists?" -ForegroundColor Green
                & node verify-runbook.mjs 2>&1 | ForEach-Object { Write-Host "     $_" }
                $runbookGaps = $LASTEXITCODE
            }
        }
    } finally { Pop-Location }

    Write-Host ""
    if ($SkipVerify) {
        if ($scenarioFailures -eq 0) { Write-Host "  Scenarios ok. Coverage and runbook checks were skipped (-SkipVerify)." -ForegroundColor Green }
        else { Write-Host "  $scenarioFailures scenario failure(s). Checks skipped (-SkipVerify)." -ForegroundColor Red; $script:exitCode = 1 }
    } elseif ($scenarioFailures -eq 0 -and -not $coverageGaps -and -not $runbookGaps) {
        Write-Host "  DEMO DATASET COMPLETE: scenarios ok, every required table holds data, runbooks consistent." -ForegroundColor Green
    } else {
        Write-Host "  DEMO DATASET INCOMPLETE: $scenarioFailures scenario failure(s), $coverageGaps empty required table(s), $runbookGaps runbook claim(s) unmet." -ForegroundColor Red
        Write-Host "  Re-running this script is safe -- every scenario is an 'ensure' step." -ForegroundColor Yellow
        $script:exitCode = 1
    }
}
finally {
    # ── 6. take down only what this script brought up ──────────────────────────────────────────
    if ($apiProcess -and -not $apiProcess.HasExited) {
        Write-Host "  -> Stopping the API it started (pid $($apiProcess.Id))" -ForegroundColor DarkGray
        Stop-Process -Id $apiProcess.Id -Force -ErrorAction SilentlyContinue
    }
    if ($startedScanner) {
        Write-Host "  -> Stopping the scanner stub it started" -ForegroundColor DarkGray
        & powershell -NoProfile -File (Join-Path $PSScriptRoot 'Start-DemoVirusScanner.ps1') -Stop | Out-Null
    }
    $env:ConnectionStrings__DefaultConnection = $null
    $env:ASPNETCORE_URLS = $null
    $env:DEMO_API = $null; $env:DEMO_DB = $null; $env:DEMO_SQL = $null
    $env:ERP_DB_USER = $null; $env:ERP_DB_PASSWORD = $null
}

if ($script:exitCode) { exit $script:exitCode }
