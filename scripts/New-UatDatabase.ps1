<#
.SYNOPSIS
    Builds the UAT / demo database from scratch and seeds it. Never touches the dev database.

.DESCRIPTION
    Stands up a second database alongside the development one so a demo can be given against
    clean data while development carries on against its own.

    !! WHY THIS SCRIPT EXISTS RATHER THAN `dotnet ef database update`
    The migration chain cannot build a database from scratch -- no migration ever CREATEs the
    Employees table -- so `database update` against an empty database fails. `rebuild-db` is the
    only route: it calls EnsureDeleted + EnsureCreated (schema straight from the EF model) and
    then stamps every migration as applied.

    !! THE PRICE OF THAT, AND IT IS PERMANENT
    EnsureCreated builds only what the EF model declares. Anything that exists solely inside
    migration SQL is never created -- Procurement's CK_ProcurementTenderControls_State check
    constraint and its lifecycle trigger are the known cases. The stamping then records the whole
    chain as applied, so the database CLAIMS to be current while missing those objects, and it can
    never afterwards be brought forward with `dotnet ef database update`. That is acceptable for a
    demo box that is rebuilt on demand. It is NOT acceptable for anything that becomes go-live.
    See docs/CROSS-MODULE-DEFECTS-FOR-FINALIZATION.md section 21.

    !! WHAT YOU GET, AND WHAT IS INVENTED
    'seed-hr-all' installs FACTS: the real organisation structure, the real positions, the reference
    lookups. It deliberately creates no employees, because employee seeding was reserved for the real
    employee-details file.

    'seed-hr-demo' then installs a DEMONSTRATION dataset on top: a workforce staffing that
    establishment, the leave vocabulary, the Ghanaian holiday calendar, and the nine area seeders
    (awards, benefits, medical, orientation, appraisal, SHE, emoluments, external associates) that
    were ported and then deferred. Every one of those is synthetic. The salary-grade bands are
    placeholder figures.

    That split is the point. Run 'seed-hr-all' anywhere; run 'seed-hr-demo' only on a database whose
    purpose is a demonstration.

    !! THE TRANSACTIONAL LAYER IS BUILT THROUGH THE API, AND THIS SCRIPT NOW RUNS IT
    Leave requests, requisitions, cases, claims, appraisals -- anything with a number from a
    sequence or a workflow instance -- cannot be seeded around the service without the screens
    reading it wrongly. They are created through the API as the demo personas by
    dev-harness/hr-demo-smoke/scenarios.mjs. Until 2026-09-04 that was a separate manual command
    and it was being skipped, so the runbooks named records that did not exist. Now
    Invoke-UatDemoScenarios.ps1 runs at the end of this script: it starts the API and the scanner
    stub, runs every scenario, checks that EVERY required HR/SHE table holds data
    (verify-tables.mjs) and that every record the runbooks name exists (verify-runbook.mjs), and
    stops what it started. A rebuild that ends in red is not fit for a demo.

.PARAMETER Database
    Target database name. Defaults to ErpSystemDB_UAT. The script REFUSES to target the
    development database -- that guard is the whole reason to use this rather than running
    rebuild-db by hand, because rebuild-db drops whatever it is pointed at without asking.

.PARAMETER SkipScenarios
    Stop after the EF seeders. Only for debugging a seeder; the result is NOT a demo database.

.PARAMETER ApiPort
    Port the transactional step starts the API on. 5000 by default; pass another to rebuild while a
    development API is running.

.EXAMPLE
    powershell -File ./scripts/New-UatDatabase.ps1
    powershell -File ./scripts/New-UatDatabase.ps1 -Database ErpSystemDB_DEMO2 -ApiPort 5010
#>
[CmdletBinding()]
param(
    [string]$Database = 'ErpSystemDB_UAT',
    [string]$Server = '.',
    [string]$UserId = 'sa',
    [string]$Password,
    [switch]$SkipConfirm,
    [switch]$SkipScenarios,
    [int]$ApiPort = 5000,
    [string]$HarnessDir
)

$ErrorActionPreference = 'Stop'

# The development database. Named here so the guard below is explicit rather than implied.
$ProtectedDatabases = @('ErpSystemDB')

if ($ProtectedDatabases -contains $Database) {
    throw "REFUSED: '$Database' is the development database. This script builds a SEPARATE " +
          "database and will not drop that one. Pass -Database with a different name."
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$apiDir = Join-Path $repoRoot 'src\ErpSystem.Api'
$dll = Join-Path $apiDir 'bin\Debug\net8.0\ErpSystem.Api.dll'

# The credential is never written down here. It comes from -Password, then ERP_DB_PASSWORD, then
# the gitignored appsettings.json the API itself uses. See scripts/ErpDbCredential.ps1.
. (Join-Path $PSScriptRoot 'ErpDbCredential.ps1')
$credential = Resolve-ErpDbCredential -UserId $UserId -Password $Password -RepoRoot $repoRoot
$UserId = $credential.UserId
$Password = $credential.Password

if (-not (Test-Path $dll)) {
    throw "Build output not found at $dll. Build the solution first."
}

$connection = "Server=$Server;Database=$Database;User Id=$UserId;Password=$Password;" +
              "TrustServerCertificate=True;Encrypt=False;MultipleActiveResultSets=True"

Write-Host ""
Write-Host "  Target database : $Database" -ForegroundColor Cyan
Write-Host "  Protected       : $($ProtectedDatabases -join ', ')" -ForegroundColor DarkGray
Write-Host ""
Write-Host "  This DROPS and rebuilds '$Database'. Any data in it is lost." -ForegroundColor Yellow

if (-not $SkipConfirm) {
    $answer = Read-Host "  Type the database name to continue"
    if ($answer -ne $Database) { throw "Aborted -- '$answer' does not match '$Database'." }
}

# Every step runs the API assembly with the connection string overridden by environment variable.
# Standard .NET configuration precedence puts environment variables above appsettings.json, so
# appsettings.json keeps pointing at the dev database and is never edited. Verified by pointing
# the override at a dead port and watching it fail there (error 10061) rather than on the dev box.
function Invoke-ApiCommand {
    param([string]$Command, [string]$Label)

    Write-Host "  -> $Label" -ForegroundColor Green
    $previous = $env:ConnectionStrings__DefaultConnection
    $previousEnv = $env:ASPNETCORE_ENVIRONMENT
    try {
        $env:ConnectionStrings__DefaultConnection = $connection
        $env:ASPNETCORE_ENVIRONMENT = 'Development'
        Push-Location $apiDir
        $output = & dotnet $dll $Command 2>&1
        if ($LASTEXITCODE -ne 0) {
            $output | Select-Object -Last 20 | ForEach-Object { Write-Host "      $_" -ForegroundColor DarkRed }
            throw "'$Command' failed with exit code $LASTEXITCODE."
        }
    }
    finally {
        Pop-Location
        $env:ConnectionStrings__DefaultConnection = $previous
        $env:ASPNETCORE_ENVIRONMENT = $previousEnv
    }
}

Invoke-ApiCommand -Command 'rebuild-db'             -Label 'Rebuilding schema from the EF model and running base seeders'
Invoke-ApiCommand -Command 'seed-workflows'         -Label 'Seeding workflow definitions (approvals refuse to submit without these)'
Invoke-ApiCommand -Command 'seed-hr-all'            -Label 'Seeding HR reference data and TDC organisation structure'

# The demonstration dataset: the workforce, the leave vocabulary and the nine area seeders.
Invoke-ApiCommand -Command 'seed-hr-demo'           -Label 'Seeding the DEMO workforce, leave calendar and HR/SHE sample data'

# !! 'seed-hr-org-authority' IS DELIBERATELY NOT RUN HERE.
# It fills unit heads and line managers wherever they are missing, which is the right thing to do on
# a database whose only employees are the estate module's. Here it is actively harmful: seed-hr-demo
# has already set a head on every unit it staffed and derived a manager for every employee from the
# organisation tree, so the only gaps left are the ones that SHOULD be gaps -- and the authority
# seeder fills them from the 24 estate fixtures. On the first run it made "Ama Estate" the manager of
# the Chief Internal Auditor. Add it back only for a database seeded WITHOUT seed-hr-demo.

# The transactional layer, through the API as the personas, plus the two checks. See the header.
$scenarioExit = 0
if ($SkipScenarios) {
    Write-Host ""
    Write-Host "  -SkipScenarios: the transactional layer was NOT built. This is not a demo database yet;" -ForegroundColor Yellow
    Write-Host "  run ./scripts/Invoke-UatDemoScenarios.ps1 -Database $Database to finish it." -ForegroundColor Yellow
} else {
    # One call does the scenarios, the second seeder pass and both checks, in that order. It starts
    # its own API on $ApiPort and REFUSES to run if that port is already taken -- on 2026-09-07 a
    # development API left on 5000 was silently adopted and the whole transactional layer went into
    # the wrong database. Stop any API first, or pass -ApiPort.
    Write-Host ""
    Write-Host "  -> Building the transactional layer through the API, then checking it" -ForegroundColor Green
    $scenarioArgs = @('-NoProfile', '-File', (Join-Path $PSScriptRoot 'Invoke-UatDemoScenarios.ps1'),
                      '-Database', $Database, '-Port', $ApiPort, '-Server', $Server, '-UserId', $UserId, '-Password', $Password)
    if ($HarnessDir) { $scenarioArgs += @('-HarnessDir', $HarnessDir) }
    & powershell @scenarioArgs
    $scenarioExit = $LASTEXITCODE
}

# Report the state rather than assume it. A seeder that silently no-ops is the failure mode this
# whole programme keeps meeting; counting the rows afterwards is the only thing that catches it.
$query = @"
SET NOCOUNT ON;
SELECT 'Employees             = ' + CAST(COUNT(*) AS varchar) FROM Employees WHERE IsDeleted = 0;
SELECT 'Leave types           = ' + CAST(COUNT(*) AS varchar) FROM LeaveTypes WHERE IsDeleted = 0;
SELECT 'Public holidays       = ' + CAST(COUNT(*) AS varchar) FROM PublicHolidays;
SELECT 'Positions             = ' + CAST(COUNT(*) AS varchar) FROM EmployeePositions WHERE IsDeleted = 0;
SELECT 'Organisation units    = ' + CAST(COUNT(*) AS varchar) FROM OrganizationUnits WHERE IsDeleted = 0;
SELECT 'Units with a head     = ' + CAST(COUNT(*) AS varchar) FROM OrganizationUnits WHERE IsDeleted = 0 AND HeadEmployeeId IS NOT NULL;
SELECT 'Active workflow defs  = ' + CAST(COUNT(*) AS varchar) FROM WorkflowDefinitions WHERE IsActive = 1;
SELECT 'Roles                 = ' + CAST(COUNT(*) AS varchar) FROM AspNetRoles;
SELECT '-- demo dataset --';
SELECT 'Demo staff (TDC/...)  = ' + CAST(COUNT(*) AS varchar) FROM Employees WHERE IsDeleted = 0 AND EmployeeNumber LIKE 'TDC/%';
SELECT 'With a line manager   = ' + CAST(COUNT(*) AS varchar) FROM Employees WHERE IsDeleted = 0 AND ManagerId IS NOT NULL;
SELECT 'Not on payroll        = ' + CAST(COUNT(*) AS varchar) FROM Employees WHERE IsDeleted = 0 AND EmployeeNumber LIKE 'TDC/%' AND IsOnPayroll = 0;
SELECT 'Payroll profiles      = ' + CAST(COUNT(*) AS varchar) FROM PayrollEmployeeProfiles WHERE IsDeleted = 0;
SELECT 'Safety incidents      = ' + CAST(COUNT(*) AS varchar) FROM SafetyIncidents WHERE IsDeleted = 0;
SELECT 'Healthcare facilities = ' + CAST(COUNT(*) AS varchar) FROM HealthcareFacilities WHERE IsDeleted = 0;
SELECT 'Award types           = ' + CAST(COUNT(*) AS varchar) FROM AwardTypes WHERE IsDeleted = 0;
SELECT 'Orientation programs  = ' + CAST(COUNT(*) AS varchar) FROM OrientationPrograms WHERE IsDeleted = 0;
SELECT 'Pay components        = ' + CAST(COUNT(*) AS varchar) FROM PayComponents WHERE IsDeleted = 0;
SELECT 'Benefit policies      = ' + CAST(COUNT(*) AS varchar) FROM BenefitPolicies WHERE IsDeleted = 0;
SELECT 'Demo logins (linked)  = ' + CAST(COUNT(*) AS varchar) FROM Users u JOIN Employees e ON e.Id = u.EmployeeId WHERE e.EmployeeNumber LIKE 'TDC/%';
SELECT 'HR workflow defs      = ' + CAST(COUNT(*) AS varchar) FROM WorkflowDefinitions d JOIN WorkflowEntityTypes et ON et.Id = d.EntityTypeId WHERE d.IsActive = 1 AND (et.Code LIKE 'STAFF[_]%' OR et.Code LIKE 'LEAVE[_]%' OR et.Code IN ('EMPLOYEE_SEPARATION','TRAINING_NOMINATION','JOB_OFFER','MANPOWER_BUDGET','PROBATION_PERIOD','SUCCESSION_PLAN','PERFORMANCE_IMPROVEMENT_PLAN'));
"@

Write-Host ""
Write-Host "  Contents of $Database" -ForegroundColor Cyan
& sqlcmd -S $Server -d $Database -U $UserId -P $Password -C -I -b -h -1 -W -Q $query |
    ForEach-Object { if ($_ -match '\S') { Write-Host "    $_" } }

Write-Host ""
Write-Host "  READ THE COUNTS ABOVE. A zero under 'demo dataset' means that seeder FAILED," -ForegroundColor Yellow
Write-Host "  not that it is deferred. Scroll up for the line reading 'FAILED:' and the reason." -ForegroundColor Yellow
Write-Host ""
Write-Host "  DEMO DATA IS SYNTHETIC. The staff, salaries, incidents and claims are invented," -ForegroundColor Yellow
Write-Host "  and the salary-grade bands are placeholder figures. Replace them from the real" -ForegroundColor Yellow
Write-Host "  employee file and HR questionnaire before this database is anything but a demo." -ForegroundColor Yellow
Write-Host ""
Write-Host "  STILL MISSING -- deferred, not broken:" -ForegroundColor Yellow
Write-Host "    - SHE reminder notification topics: created by the first SHE reminder sweep (she.manager:"
Write-Host "      Administration -> Safety (SHE) -> Reminder Engine -> Run now), not by the seed"
Write-Host "    - Email templates, unless the seed host registered the email catalogues"
Write-Host ""
if ($scenarioExit -ne 0) {
    Write-Host "  !! THE TRANSACTIONAL LAYER OR A COVERAGE CHECK FAILED (see the red lines above)." -ForegroundColor Red
    Write-Host "  !! This database is NOT ready for a demo. Fix the cause, then re-run:" -ForegroundColor Red
    Write-Host "       powershell -File ./scripts/Invoke-UatDemoScenarios.ps1 -Database $Database" -ForegroundColor Red
    Write-Host "     (safe to repeat -- every scenario is an 'ensure' step)." -ForegroundColor Red
    Write-Host ""
}
Write-Host "  Start the API against it with:" -ForegroundColor Cyan
Write-Host "    powershell -File ./scripts/Start-ErpApi.ps1 -Database Uat"
Write-Host ""
if ($scenarioExit -ne 0) { exit 1 }
