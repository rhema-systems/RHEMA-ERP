[CmdletBinding()]
param(
    [string]$SqlServer,
    [string]$CanonicalDatabase,
    [string]$LegacyDatabase
)
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
. (Join-Path $PSScriptRoot 'New-RhemaVpsPreflightHelper.ps1')
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('rhema-preflight-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot | Out-Null
$output = Join-Path $testRoot 'Invoke-RhemaVpsRemote.ps1'
try {
    $manifest = Get-Content -Raw `
        (Join-Path $PSScriptRoot 'CanonicalMigrationPreflight.json') | ConvertFrom-Json
    $expectedProbeCount = @($manifest.probes).Count
    $expectedCoverageCount = @($manifest.probes.migrations).Count
    New-RhemaVpsPreflightHelper -RepositoryRoot $repositoryRoot -OutputPath $output | Out-Null
    $errors = $null; $tokens = $null
    $ast = [Management.Automation.Language.Parser]::ParseFile($output, [ref]$tokens, [ref]$errors)
    if ($errors.Count) { throw 'Packaged helper does not parse.' }
    $node = $ast.Find({ param($n) $n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -eq 'Invoke-CanonicalMigrationPreflight' }, $true)
    # Load only this pure orchestration function, never the service-action script.
    Invoke-Expression $node.Extent.Text
    function Assert-True { param([bool]$Condition,[string]$Message) if(-not $Condition){throw $Message} }
    $script:probeCalls = 0
    $script:blockProbe = $false
    function Invoke-DatabaseTable {
        param([string]$Query)
        $script:probeCalls++
        if ($Query -notmatch 'SELECT CheckName') { throw 'Probe result contract missing.' }
        if ($script:blockProbe -and $script:probeCalls -eq 1) {
            [pscustomobject]@{CheckName='Test retained transaction';AffectedRows=3}
        }
    }
    $result = @(Invoke-CanonicalMigrationPreflight)
    $coverage = @($result | Where-Object { $_ -like 'GUARD_COVERAGE|*' })
    if ($script:probeCalls -ne $expectedProbeCount -or
        $coverage.Count -ne $expectedCoverageCount -or
        @($coverage | Select-Object -Unique).Count -ne $expectedCoverageCount) {
        throw 'Successful preflight did not execute all probes and report exact coverage.'
    }
    $script:probeCalls=0; $script:blockProbe=$true; $blocked=$false
    try { Invoke-CanonicalMigrationPreflight | Out-Null } catch {
        if ($_.Exception.Message -notlike '*Test retained transaction=3*') { throw }
        $blocked=$true
    }
    if (-not $blocked -or $script:probeCalls -ne $expectedProbeCount) {
        throw 'Retained data did not fail closed after all probes.'
    }

    # Verify a changed migration cannot inherit a previously reviewed coverage ID.
    $fixture = Join-Path $testRoot 'fixture'
    New-Item -ItemType Directory -Path (Join-Path $fixture 'scripts\vps'),(Join-Path $fixture 'src\ErpSystem.Data\Migrations') -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'CanonicalMigrationPreflight.json'),(Join-Path $PSScriptRoot 'Invoke-RhemaVpsRemote.ps1') -Destination (Join-Path $fixture 'scripts\vps')
    foreach($probe in $manifest.probes) {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot $probe.sqlFile) -Destination (Join-Path $fixture 'scripts\vps')
        foreach($migration in $probe.migrations) {
            Copy-Item -LiteralPath (Join-Path $repositoryRoot "src\ErpSystem.Data\Migrations\$($migration.id).cs") -Destination (Join-Path $fixture 'src\ErpSystem.Data\Migrations')
            foreach($guard in @($migration.guardSources)) {
                if($null -ne $guard) {
                    Copy-Item -LiteralPath (Join-Path $repositoryRoot "src\ErpSystem.Data\Migrations\$($guard.file)") -Destination (Join-Path $fixture 'src\ErpSystem.Data\Migrations')
                }
            }
        }
    }
    $firstId = $manifest.probes[0].migrations[0].id
    Add-Content -LiteralPath (Join-Path $fixture "src\ErpSystem.Data\Migrations\$firstId.cs") -Value '// Unreviewed change'
    $blocked=$false
    try { New-RhemaVpsPreflightHelper -RepositoryRoot $fixture -OutputPath $output | Out-Null } catch {
        if ($_.Exception.Message -notlike '*changed since its preflight review*') { throw }
        $blocked=$true
    }
    if (-not $blocked) { throw 'Changed migration was not rejected.' }
    Copy-Item -LiteralPath (Join-Path $repositoryRoot "src\ErpSystem.Data\Migrations\$firstId.cs") -Destination (Join-Path $fixture 'src\ErpSystem.Data\Migrations') -Force
    $guardFile = 'InventoryIssueOptionalApprovalGuards.cs'
    Add-Content -LiteralPath (Join-Path $fixture "src\ErpSystem.Data\Migrations\$guardFile") -Value '// Unreviewed guard change'
    $blocked=$false
    try { New-RhemaVpsPreflightHelper -RepositoryRoot $fixture -OutputPath $output | Out-Null } catch {
        if ($_.Exception.Message -notlike '*guard InventoryIssueOptionalApprovalGuards.cs changed since its preflight review*') { throw }
        $blocked=$true
    }
    if (-not $blocked) { throw 'Changed helper guard was not rejected.' }
    Copy-Item -LiteralPath `
        (Join-Path $repositoryRoot "src\ErpSystem.Data\Migrations\$guardFile") `
        -Destination (Join-Path $fixture 'src\ErpSystem.Data\Migrations') -Force
    [IO.File]::WriteAllText(
        (Join-Path $fixture 'src\ErpSystem.Data\Migrations\20991231235959_UnreviewedGuard.cs'),
        'migrationBuilder.Sql("THROW 59999, ''fixture'', 1;");')
    $blocked=$false
    try { Assert-RhemaMigrationGuardCoverage -RepositoryRoot $fixture | Out-Null } catch {
        if ($_.Exception.Message -notlike '*20991231235959_UnreviewedGuard*') { throw }
        $blocked=$true
    }
    if (-not $blocked) { throw 'Unreviewed guarded migration was not rejected before release build.' }

    $ghanaEstateSql = [IO.File]::ReadAllText(
        (Join-Path $PSScriptRoot 'GhanaEstateMigrationPreflight.sql'))
    foreach ($contract in @(
            'SET @GhanaWhtApplied = CASE WHEN EXISTS',
            'EstateLandReference.UnresolvedAfterBackfill',
            'EstateLandReference.DuplicateProjectedReference',
            'DATALENGTH(ProjectedReference) > 240',
            'parcel.ParentDemarcationId IS NULL AND asset.Id IS NOT NULL')) {
        if (-not $ghanaEstateSql.Contains($contract)) {
            throw "Ghana/Estate preflight is missing contract: $contract"
        }
    }
    Write-Host "PASS: packaged PowerShell helper, all $expectedCoverageCount coverage IDs, retained-data rejection, stale-review rejection, and pre-build guard-coverage enforcement."

    if ($SqlServer) {
        if (-not $CanonicalDatabase -or -not $LegacyDatabase) { throw 'Both test database names are required.' }
        foreach($database in @($CanonicalDatabase,$LegacyDatabase)) {
            $builder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder
            $builder['Data Source']=$SqlServer; $builder['Initial Catalog']=$database
            $builder['Integrated Security']=$true
            $connection=New-Object System.Data.SqlClient.SqlConnection $builder.ConnectionString
            $connection.Open()
            try {
                $counts=@{}
                foreach($probe in $manifest.probes) {
                    $command=$connection.CreateCommand()
                    $command.CommandText=[IO.File]::ReadAllText((Join-Path $PSScriptRoot $probe.sqlFile))
                    $reader=$command.ExecuteReader(); $table=New-Object System.Data.DataTable
                    $table.Load($reader); $command.Dispose()
                    $counts[$probe.sqlFile]=$table.Rows.Count
                }
                if ($database -eq $CanonicalDatabase -and ($counts.Values | Where-Object {$_ -ne 0})) { throw 'Applied canonical database unexpectedly blocked.' }
                if ($database -eq $LegacyDatabase -and $counts['FinanceCanonicalMigrationPreflight.sql'] -lt 1) { throw 'Retained legacy Finance transactions were not blocked.' }
                Write-Host "PASS: read-only SQL preflight on $database; blockers=$($counts.Values | Measure-Object -Sum | Select-Object -ExpandProperty Sum)."
                if ($database -eq $CanonicalDatabase) {
                    # Exercise the pending predecessor branch with read-only catalog
                    # projections. Neither actual history nor schema is modified.
                    $sql=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'CanonicalTriggerPeriodMigrationPreflight.sql'))
                    $sql=$sql.Replace("SELECT MigrationId FROM dbo.__EFMigrationsHistory'", "SELECT MigrationId FROM dbo.__EFMigrationsHistory WHERE MigrationId NOT IN (N''20260921174134_AccountingBookTranslationEvidence'',N''20260925210000_ReconcileAccountingBookPeriodInitializationSchema'')'")
                    $sql=$sql.Replace('sys.columns', '(SELECT * FROM sys.columns WHERE name NOT LIKE N''Translation%'')')
                    $sql=$sql.Replace('sys.key_constraints', '(SELECT * FROM sys.key_constraints WHERE name <> N''AK_ExchangeRates_TenantId_Id'') AS fixtureKeys')
                    $command=$connection.CreateCommand(); $command.CommandText=$sql
                    $table=New-Object System.Data.DataTable; $table.Load($command.ExecuteReader()); $command.Dispose()
                    if($table.Rows.Count -ne 0) { throw 'Pending translation predecessor incorrectly reported final-schema drift.' }
                    Write-Host 'PASS: read-only pre-translation catalog simulation; no false final-schema blocker.'
                }
            } finally { $connection.Dispose() }
        }
    }
} finally {
    # Remove only this run's newly created temporary fixture tree.
    $resolved = [IO.Path]::GetFullPath($testRoot)
    $tempParent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    if ($resolved.StartsWith($tempParent,[StringComparison]::OrdinalIgnoreCase) -and (Split-Path $resolved -Leaf) -match '^rhema-preflight-[a-f0-9]{32}$') {
        Remove-Item -LiteralPath $resolved -Recurse -Force
    } else { throw 'Unsafe temporary fixture cleanup path.' }
}
