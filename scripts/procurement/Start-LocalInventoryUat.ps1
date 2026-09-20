# Run from the regular checkout. The API uses its coherent regular bin output;
# the frontend deliberately uses the verified, frozen InventoryFinal production build.
# Default is read-only verification. Explicit -Start starts both main servers, never stops any process.
# Requires PowerShell 7, existing local user secrets, migration parity and passed checkpoint artifacts.
param([switch]$Start, [ValidateSet('InventoryFinal', 'InventoryLayout')][string]$FrontendRelease = 'InventoryFinal',
    [ValidateSet('InventoryFinal', 'TransferAutoComplete')][string]$ApiPhase = 'InventoryFinal')
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'Run this helper with pwsh (PowerShell 7).' }
$inventoryUatRepo = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
$inventoryUatApiProject = Join-Path $inventoryUatRepo 'src/ErpSystem.Api'
$inventoryUatApiBin = Join-Path $inventoryUatApiProject 'bin/Debug/net8.0'
$inventoryUatRelease = Join-Path $inventoryUatRepo 'local-artifacts/inventory-final-20260912'
$inventoryUatApiRelease = if ($ApiPhase -eq 'TransferAutoComplete') { Join-Path $inventoryUatRepo 'local-artifacts/inventory-transfer-auto-complete-20260912' } else { $inventoryUatRelease }
$inventoryUatMigrationCount = if ($ApiPhase -eq 'TransferAutoComplete') { 482 } else { 481 }
$inventoryUatMigrationId = if ($ApiPhase -eq 'TransferAutoComplete') { '20260912232000_InventoryTransferAutomaticCompletion' } else { '20260912231000_PurchaseReturnOptionalApproval' }
$inventoryUatMainRelease = Join-Path $inventoryUatRepo $(if ($FrontendRelease -eq 'InventoryLayout') { 'local-artifacts/inventory-final-layout-main-20260912' } else { 'local-artifacts/inventory-final-main-20260912' })
$inventoryUatFrontendSourceRelease = if ($FrontendRelease -eq 'InventoryLayout') { Join-Path $inventoryUatRepo 'local-artifacts/inventory-final-layout-20260912' } else { $inventoryUatRelease }
$inventoryUatFrontend = Join-Path $inventoryUatMainRelease 'frontend'
$inventoryUatFrontendResult = Get-Content -LiteralPath (Join-Path $inventoryUatMainRelease 'frontend-build-result.json') -Raw | ConvertFrom-Json
if ($inventoryUatFrontendResult.requiresApiPhase -and $inventoryUatFrontendResult.requiresApiPhase -ne $ApiPhase) { throw 'This frontend requires the explicit matching API phase; no server was started.' }
$inventoryUatBuildId = (Get-Content -LiteralPath (Join-Path $inventoryUatFrontend '.next/BUILD_ID') -Raw).Trim()
if ([IO.Path]::GetFullPath([string]$inventoryUatFrontendResult.appDir) -ne [IO.Path]::GetFullPath($inventoryUatFrontend) -or
    $inventoryUatFrontendResult.apiUrl -cne 'http://localhost:5000/api' -or
    $inventoryUatFrontendResult.phase -cne $FrontendRelease -or
    [string]::IsNullOrWhiteSpace($inventoryUatBuildId) -or $inventoryUatBuildId -eq 'development' -or
    $inventoryUatBuildId -cne $inventoryUatFrontendResult.buildId) {
    throw 'Main frontend path, API 5000, phase or production BUILD_ID does not match its verified release.'
}
$inventoryUatRehearsalResult = Get-Content -LiteralPath (Join-Path $inventoryUatFrontendSourceRelease 'frontend-build-result.json') -Raw | ConvertFrom-Json
if ($inventoryUatFrontendResult.sourceBuildId -cne $inventoryUatRehearsalResult.buildId) {
    throw 'Main frontend is not linked to the reviewed InventoryFinal source build.'
}
$inventoryUatHashes = @(Get-Content -LiteralPath (Join-Path $inventoryUatApiRelease 'api-stage-hashes.json') -Raw | ConvertFrom-Json)
$inventoryUatAssemblies = @('ErpSystem.Api','ErpSystem.Core','ErpSystem.Data','ErpSystem.Shared')
if ($inventoryUatHashes.Count -ne 4 -or
    (@($inventoryUatHashes.Assembly | Sort-Object) -join '|') -cne (@($inventoryUatAssemblies | Sort-Object) -join '|')) {
    throw 'The exact four-assembly API checkpoint manifest is required.'
}
foreach ($inventoryUatHash in $inventoryUatHashes) {
    if ($inventoryUatHash.SHA256 -notmatch '^[A-Fa-f0-9]{64}$' -or
        (Get-FileHash -LiteralPath (Join-Path $inventoryUatApiBin ($inventoryUatHash.Assembly + '.dll')) -Algorithm SHA256).Hash -ne $inventoryUatHash.SHA256) {
        throw 'Regular API dependencies no longer match the reviewed InventoryFinal assemblies. Do not start stale or newly untested binaries.'
    }
}
$inventoryUatTestCounts = [ordered]@{}
foreach ($inventoryUatTestKind in @('core','api')) {
    $inventoryUatTestProject = if ($inventoryUatTestKind -eq 'core') { 'ErpSystem.Core.Tests' } else { 'ErpSystem.Api.Tests' }
    $inventoryUatTrxPath = Join-Path $inventoryUatRepo "local-artifacts/inventory-final-results/inventory-final-$inventoryUatTestKind.trx"
    [xml]$inventoryUatTrx = Get-Content -LiteralPath $inventoryUatTrxPath -Raw
    $inventoryUatCounters = $inventoryUatTrx.SelectSingleNode("//*[local-name()='ResultSummary']/*[local-name()='Counters']")
    if (-not $inventoryUatCounters -or [int]$inventoryUatCounters.total -le 0 -or
        [int]$inventoryUatCounters.passed -ne [int]$inventoryUatCounters.total -or
        [int]$inventoryUatCounters.executed -ne [int]$inventoryUatCounters.total) {
        throw "The complete InventoryFinal $inventoryUatTestKind checkpoint has not passed."
    }
    foreach ($inventoryUatHash in $inventoryUatHashes) {
        if ($inventoryUatTestKind -eq 'core' -and $inventoryUatHash.Assembly -eq 'ErpSystem.Api') { continue }
        $inventoryUatTestAssembly = Join-Path $inventoryUatRepo "tests/$inventoryUatTestProject/bin/Debug/net8.0/$($inventoryUatHash.Assembly).dll"
        if ((Get-FileHash -LiteralPath $inventoryUatTestAssembly -Algorithm SHA256).Hash -ne $inventoryUatHash.SHA256 -or
            (Get-Item -LiteralPath $inventoryUatTestAssembly).LastWriteTimeUtc -gt (Get-Item -LiteralPath $inventoryUatTrxPath).LastWriteTimeUtc) {
            throw 'Test output does not prove the current reviewed API dependencies. Re-run the coherent checkpoint before starting.'
        }
    }
    $inventoryUatTestCounts[$inventoryUatTestKind] = [int]$inventoryUatCounters.passed
    if ($ApiPhase -eq 'TransferAutoComplete') {
        $inventoryUatMinimum = if ($inventoryUatTestKind -eq 'core') { 191 } else { 94 }
        if ([int]$inventoryUatCounters.passed -lt $inventoryUatMinimum) { throw 'The complete automatic-completion checkpoint is required before main startup.' }
    }
    if ($ApiPhase -eq 'TransferAutoComplete' -and $inventoryUatTestKind -eq 'core') {
        $inventoryUatMethods = @($inventoryUatTrx.TestRun.TestDefinitions.UnitTest.TestMethod | ForEach-Object name)
        foreach ($inventoryUatRequiredMethod in @('Final_good_receipt_completes_automatically_with_real_receiver_and_no_extra_stock_or_approval',
            'Missing_good_units_or_legacy_discrepancies_never_auto_complete',
            'Normal_receipt_accepts_only_good_quantities_and_does_not_book_damage_or_shortage_as_received',
            'Automatic_completion_migration_keeps_guards_and_requires_a_linked_good_receipt')) {
            if ($inventoryUatRequiredMethod -notin $inventoryUatMethods) { throw 'The main API checkpoint is missing automatic-completion coverage.' }
        }
    }
}

# WebApplication.CreateBuilder in Development reads the existing project user secrets.
# Load them privately only to verify the exact main connection; never print or save values.
$inventoryUatRawSecrets = @(& dotnet user-secrets list --project (Join-Path $inventoryUatApiProject 'ErpSystem.Api.csproj') --json 2>&1)
if ($LASTEXITCODE -ne 0) { throw 'Could not read the existing local API user secrets.' }
$inventoryUatSecretText = ($inventoryUatRawSecrets | ForEach-Object { [string]$_ }) -join "`n"
$inventoryUatJsonStart = $inventoryUatSecretText.IndexOf('{')
$inventoryUatJsonEnd = $inventoryUatSecretText.LastIndexOf('}')
if ($inventoryUatJsonStart -lt 0 -or $inventoryUatJsonEnd -le $inventoryUatJsonStart) { throw 'Existing local configuration was not readable.' }
try {
    $inventoryUatSecrets = $inventoryUatSecretText.Substring($inventoryUatJsonStart,$inventoryUatJsonEnd-$inventoryUatJsonStart+1) | ConvertFrom-Json
} catch { throw 'Existing local configuration could not be parsed; no configuration values are logged.' }
Add-Type -AssemblyName System.Data
try {
    $inventoryUatConnectionBuilder = [System.Data.SqlClient.SqlConnectionStringBuilder]::new([string]$inventoryUatSecrets.'ConnectionStrings:DefaultConnection')
} catch { throw 'The existing local DefaultConnection is unavailable or invalid.' }
# Existing local secrets use .\SQL2017. Permit that one local alias only;
# SERVERPROPERTY below must still prove the exact physical server before startup.
if ($inventoryUatConnectionBuilder.DataSource -notin @('RHEMA-MICHAEL\SQL2017','.\SQL2017') -or $inventoryUatConnectionBuilder.InitialCatalog -cne 'RhemaERP') {
    throw 'Existing user secrets must target exactly local RHEMA-MICHAEL\SQL2017 / RhemaERP. No configuration was changed.'
}
$inventoryUatProbe = [System.Data.SqlClient.SqlConnection]::new($inventoryUatConnectionBuilder.ConnectionString)
try {
    $inventoryUatProbe.Open()
    $inventoryUatCommand = $inventoryUatProbe.CreateCommand()
    $inventoryUatCommand.CommandTimeout = 30
    $inventoryUatCommand.CommandText = @'
SELECT CASE WHEN DB_NAME()=N'RhemaERP'
 AND CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))=N'RHEMA-MICHAEL\SQL2017'
 AND (SELECT COUNT(*) FROM dbo.__EFMigrationsHistory)=@MigrationCount
 AND (SELECT MAX(MigrationId) FROM dbo.__EFMigrationsHistory)=@MigrationId
 AND (SELECT COUNT(*) FROM dbo.__EFMigrationsHistory WHERE MigrationId IN
 (N'20260912230000_InventoryTransferOptionalApproval',N'20260912231000_PurchaseReturnOptionalApproval'))=2
 THEN 1 ELSE 0 END;
'@
    [void]$inventoryUatCommand.Parameters.Add('@MigrationCount',[System.Data.SqlDbType]::Int)
    $inventoryUatCommand.Parameters['@MigrationCount'].Value=$inventoryUatMigrationCount
    [void]$inventoryUatCommand.Parameters.Add('@MigrationId',[System.Data.SqlDbType]::NVarChar,150)
    $inventoryUatCommand.Parameters['@MigrationId'].Value=$inventoryUatMigrationId
    if ([int]$inventoryUatCommand.ExecuteScalar() -ne 1) { throw 'The exact main UAT migration checkpoint is not present.' }
} catch { throw 'Main UAT database identity or migration verification failed; no server was started.' }
finally { if ($inventoryUatCommand) { $inventoryUatCommand.Dispose() }; $inventoryUatProbe.Dispose() }

foreach ($inventoryUatPort in @(5000,3000)) {
    if (Get-NetTCPConnection -LocalPort $inventoryUatPort -State Listen -ErrorAction SilentlyContinue) {
        throw "Main port $inventoryUatPort is occupied. No existing process will be stopped."
    }
}
$inventoryUatApiExe = Join-Path $inventoryUatApiBin 'ErpSystem.Api.exe'
$inventoryUatNext = Join-Path $inventoryUatFrontend 'node_modules/next/dist/bin/next'
if (-not (Test-Path -LiteralPath $inventoryUatApiExe -PathType Leaf) -or -not (Test-Path -LiteralPath $inventoryUatNext -PathType Leaf)) {
    throw 'The verified main API apphost or frontend dependency runtime is missing.'
}
if (-not $Start) {
    [pscustomobject]@{Verified=$true;Started=$false;Database='RhemaERP';MigrationCount=$inventoryUatMigrationCount;ApiPhase=$ApiPhase;BuildId=$inventoryUatBuildId;
        ApiDirectory=$inventoryUatApiBin;FrozenFrontendDirectory=$inventoryUatFrontend;PassedTests=$inventoryUatTestCounts;
        FullTypecheckPassed=$inventoryUatFrontendResult.typecheckPassed;Next='Re-run with -Start when main UAT should start.'} | ConvertTo-Json -Depth 4
    return
}
$inventoryUatStamp = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff')
$inventoryUatApiOut = Join-Path $inventoryUatMainRelease "main-api-$inventoryUatStamp.stdout.log"
$inventoryUatApiErr = Join-Path $inventoryUatMainRelease "main-api-$inventoryUatStamp.stderr.log"
$inventoryUatFrontendOut = Join-Path $inventoryUatMainRelease "main-frontend-$inventoryUatStamp.stdout.log"
$inventoryUatFrontendErr = Join-Path $inventoryUatMainRelease "main-frontend-$inventoryUatStamp.stderr.log"
$inventoryUatEnvironment = @{
    ASPNETCORE_ENVIRONMENT='Development'; DOTNET_ENVIRONMENT='Development'; ASPNETCORE_URLS='http://localhost:5000';
    SkipStartupInitialization='true'; StartupInitialization__SeedWorkflowDefinitions='false'; StartupInitialization__SeedDevelopmentData='false';
    BackgroundServices__Enabled='false'; ConnectionStrings__DefaultConnection=$inventoryUatConnectionBuilder.ConnectionString;
    NODE_ENV='production'; NEXT_PUBLIC_API_URL='http://localhost:5000/api'; NEXTAUTH_URL='http://localhost:3000';
    NODE_OPTIONS='--max-old-space-size=8192'
}
$inventoryUatPriorEnvironment = @{}
$inventoryUatApiProcess = $null
$inventoryUatFrontendProcess = $null
try {
    foreach ($inventoryUatEntry in $inventoryUatEnvironment.GetEnumerator()) {
        $inventoryUatPriorEnvironment[$inventoryUatEntry.Key] = [Environment]::GetEnvironmentVariable($inventoryUatEntry.Key,'Process')
        [Environment]::SetEnvironmentVariable($inventoryUatEntry.Key,[string]$inventoryUatEntry.Value,'Process')
    }
    $inventoryUatApiProcess = Start-Process -FilePath $inventoryUatApiExe -ArgumentList '--urls','http://localhost:5000','--SkipStartupInitialization','true' `
        -WorkingDirectory $inventoryUatApiProject -WindowStyle Hidden -PassThru -RedirectStandardOutput $inventoryUatApiOut -RedirectStandardError $inventoryUatApiErr
    $inventoryUatApiReady = $false
    for ($inventoryUatAttempt=0; $inventoryUatAttempt -lt 20; $inventoryUatAttempt++) {
        if ($inventoryUatApiProcess.HasExited) { throw 'Main API exited before readiness; inspect its local logs.' }
        try {
            if ((Invoke-WebRequest 'http://localhost:5000/health/live' -TimeoutSec 2 -NoProxy).StatusCode -eq 200) { $inventoryUatApiReady=$true; break }
        } catch { }
        Start-Sleep -Milliseconds 500
    }
    if (-not $inventoryUatApiReady) { throw 'Main API is not ready yet. Frontend was not started; inspect the returned API PID/logs before retrying.' }
    if (Get-NetTCPConnection -LocalPort 3000 -State Listen -ErrorAction SilentlyContinue) { throw 'Port3000 became occupied; no frontend process was started.' }
    $inventoryUatFrontendProcess = Start-Process -FilePath (Get-Command node -ErrorAction Stop).Source `
        -ArgumentList @(('"'+$inventoryUatNext+'"'),'start','--hostname','localhost','--port','3000') `
        -WorkingDirectory $inventoryUatFrontend -WindowStyle Hidden -PassThru -RedirectStandardOutput $inventoryUatFrontendOut -RedirectStandardError $inventoryUatFrontendErr
    [pscustomobject]@{Started=$true;Url='http://localhost:3000';ApiUrl='http://localhost:5000';Database='RhemaERP';BuildId=$inventoryUatBuildId;
        ApiPid=$inventoryUatApiProcess.Id;FrontendPid=$inventoryUatFrontendProcess.Id;ApiDirectory=$inventoryUatApiBin;
        FrozenFrontendDirectory=$inventoryUatFrontend;ApiLog=$inventoryUatApiOut;ApiErrorLog=$inventoryUatApiErr;
        FrontendLog=$inventoryUatFrontendOut;FrontendErrorLog=$inventoryUatFrontendErr;StartupDatabaseInitialization=$false;
        RehearsalUntouched=$true;BrowserVerificationRequired=$true} | ConvertTo-Json -Depth 4
} catch {
    $inventoryUatStartedApiId = if ($inventoryUatApiProcess) { $inventoryUatApiProcess.Id } else { 'none' }
    $inventoryUatStartedFrontendId = if ($inventoryUatFrontendProcess) { $inventoryUatFrontendProcess.Id } else { 'none' }
    throw "Main UAT launch did not finish. Existing processes were not stopped. New API PID=$inventoryUatStartedApiId; frontend PID=$inventoryUatStartedFrontendId. Inspect $inventoryUatApiErr and $inventoryUatFrontendErr before another launch."
} finally {
    foreach ($inventoryUatEntry in $inventoryUatPriorEnvironment.GetEnumerator()) {
        [Environment]::SetEnvironmentVariable($inventoryUatEntry.Key,$inventoryUatEntry.Value,'Process')
    }
}
