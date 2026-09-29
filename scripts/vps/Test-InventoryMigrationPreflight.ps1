[CmdletBinding()]
param(
    [string]$SqlServer,
    [string]$Migration60Database,
    [string]$Migration61Database,
    [string]$Migration62Database
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$migration61 = '20260927141036_InventoryControlledWorkflowsAndAccounting'
$migration62 = '20260927211546_InventoryIssueOptionalWorkflowApproval'
$temporaryRoot = Join-Path ([IO.Path]::GetTempPath()) ('rhema-inventory-preflight-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temporaryRoot | Out-Null

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
}

function Get-FunctionSource {
    param([string]$Path, [string]$Name)
    $tokens = $null; $parseErrors = $null
    $ast = [Management.Automation.Language.Parser]::ParseFile($Path, [ref]$tokens, [ref]$parseErrors)
    Assert-True ($parseErrors.Count -eq 0) "Script does not parse: $Path"
    $functionNode = $ast.Find({ param($node)
        $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq $Name
    }, $true)
    Assert-True ($null -ne $functionNode) "Function $Name was not found."
    return $functionNode.Extent.Text
}

function Invoke-ReadOnlyProbe {
    param([System.Data.SqlClient.SqlConnection]$Connection, [string]$Query)
    $command = $Connection.CreateCommand()
    $command.CommandText = $Query
    $command.CommandTimeout = 60
    try {
        $table = New-Object System.Data.DataTable
        $reader = $command.ExecuteReader()
        try { $table.Load($reader) } finally { $reader.Dispose() }
        foreach ($row in $table.Rows) {
            $result = [ordered]@{}
            foreach ($column in $table.Columns) { $result[$column.ColumnName] = $row[$column] }
            [pscustomobject]$result
        }
    } finally { $command.Dispose() }
}

function Open-FixtureConnection {
    param([string]$Database)
    $builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
    $builder['Data Source'] = $SqlServer
    $builder['Initial Catalog'] = $Database
    $builder['Integrated Security'] = $true
    $builder['Application Name'] = 'Rhema read-only Inventory migration preflight regression'
    $connection = New-Object System.Data.SqlClient.SqlConnection $builder.ConnectionString
    $connection.Open()
    return $connection
}

function Assert-ProbeResult {
    param([System.Data.SqlClient.SqlConnection]$Connection, [string]$Query, [string]$Case, [string]$ExpectedBlocker)
    $rows = @(Invoke-ReadOnlyProbe $Connection $Query)
    if ($ExpectedBlocker) {
        Assert-True ($rows.Count -eq 1 -and $rows[0].CheckName -eq $ExpectedBlocker -and [long]$rows[0].AffectedRows -gt 0) "Case '$Case' did not return exactly $ExpectedBlocker; found $($rows | ConvertTo-Json -Compress)."
    } else { Assert-True ($rows.Count -eq 0) "Case '$Case' unexpectedly blocked: $($rows | ConvertTo-Json -Compress)." }
    $script:sqlCaseCount++
    Write-Host "PASS: $Case."
}

function Replace-ExactlyOnce {
    param([string]$Source, [string]$Before, [string]$After)
    Assert-True ([regex]::Matches($Source, [regex]::Escape($Before)).Count -eq 1) "Read-only fixture projection no longer matches one source expression: $Before"
    return $Source.Replace($Before, $After)
}

function Set-ReadOnlyTriggerProjection {
    param([string]$Source, [string]$Trigger, [string]$Predicate, [ValidateSet('Missing', 'Repeated', 'Absent')] [string]$Mode)
    $definition = "OBJECT_DEFINITION(OBJECT_ID(N'dbo.'+QUOTENAME(p.TriggerName),N'TR'))"
    $literal = "N'" + $Predicate.Replace("'", "''") + "'"
    $projection = switch ($Mode) {
        'Missing' { "REPLACE($definition,$literal,N'')" }
        'Repeated' { "($definition+N' '+$literal)" }
        'Absent' { 'CAST(NULL AS nvarchar(max))' }
    }
    return Replace-ExactlyOnce $Source $definition "(CASE WHEN p.TriggerName=N'$Trigger' THEN $projection ELSE $definition END)"
}

try {
    . (Join-Path $PSScriptRoot 'New-RhemaVpsPreflightHelper.ps1')
    $packagedHelper = Join-Path $temporaryRoot 'Invoke-RhemaVpsRemote.ps1'
    New-RhemaVpsPreflightHelper -RepositoryRoot $repositoryRoot -OutputPath $packagedHelper | Out-Null
    # Extract functions only. Do not dot-source the deployment/service action entry points.
    Invoke-Expression (Get-FunctionSource $packagedHelper 'Invoke-CanonicalMigrationPreflight')
    $deploymentPath = Join-Path $repositoryRoot 'scripts\Deploy-RhemaVps.ps1'
    Invoke-Expression (Get-FunctionSource $deploymentPath 'Get-LocalMigrationIds')
    Invoke-Expression (Get-FunctionSource $deploymentPath 'Compare-MigrationState')

    $manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'CanonicalMigrationPreflight.json') -Raw | ConvertFrom-Json
    $inventoryProbe = @($manifest.probes | Where-Object { $_.sqlFile -eq 'InventoryControlledMigrationPreflight.sql' })
    Assert-True ($inventoryProbe.Count -eq 1) 'The packaged manifest must contain one Inventory probe.'
    $inventoryIds = @($inventoryProbe[0].migrations | ForEach-Object { $_.id })
    Assert-True ($inventoryIds.Count -eq 2 -and $migration61 -in $inventoryIds -and $migration62 -in $inventoryIds) 'The Inventory probe must cover both pending migrations.'
    $expectedCoverage = @($manifest.probes | ForEach-Object { $_.migrations } | ForEach-Object { "GUARD_COVERAGE|$($_.id)" })
    $script:probeCalls = 0
    function Invoke-DatabaseTable {
        param([string]$Query)
        $script:probeCalls++
        Assert-True ($Query -match 'SELECT CheckName') 'Packaged probe result contract is missing.'
    }
    $coverage = @(Invoke-CanonicalMigrationPreflight | Where-Object { $_ -like 'GUARD_COVERAGE|*' })
    Assert-True ($script:probeCalls -eq $manifest.probes.Count) 'Not every packaged preflight probe executed.'
    Assert-True (@(Compare-Object $expectedCoverage $coverage).Count -eq 0) 'The packaged helper omitted or duplicated reviewed coverage.'
    $localIds = @(Get-LocalMigrationIds)
    $remoteHistory = @($localIds | Where-Object { $_ -notin @($migration61, $migration62) } | ForEach-Object { "MIGRATION_ID|$_" })
    $comparison = Compare-MigrationState -RemoteOutput ($remoteHistory + $coverage) -RequireCurrent $false
    Assert-True (@($comparison.pending).Count -eq 2 -and $migration61 -in $comparison.pending -and $migration62 -in $comparison.pending) 'The release preflight did not accept exactly the pending Inventory migrations.'
    foreach ($missingCoverage in @($migration61, $migration62)) {
        $blocked = $false
        try { Compare-MigrationState -RemoteOutput ($remoteHistory + @($coverage | Where-Object { $_ -ne "GUARD_COVERAGE|$missingCoverage" })) -RequireCurrent $false | Out-Null }
        catch {
            if ($_.Exception.Message -notlike '*Pending guarded migrations lack*') { throw }
            $blocked = $true
        }
        Assert-True $blocked "Pending Inventory migration $missingCoverage was accepted without reviewed coverage."
    }
    $blocked = $false
    try { Compare-MigrationState -RemoteOutput ($remoteHistory + $coverage) -RequireCurrent $true | Out-Null }
    catch {
        if ($_.Exception.Message -notlike '*VPS remains behind by 2*') { throw }
        $blocked = $true
    }
    Assert-True $blocked 'The final migration parity gate accepted pending migrations.'
    Write-Host 'PASS: packaged coverage, 60-to-62 release comparison, missing-coverage rejection and final parity gate.'

    if ($SqlServer) {
        Assert-True (-not [string]::IsNullOrWhiteSpace($Migration60Database) -and -not [string]::IsNullOrWhiteSpace($Migration61Database) -and -not [string]::IsNullOrWhiteSpace($Migration62Database)) 'Provide the three isolated fixture database names with -SqlServer.'
        $probeSql = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'InventoryControlledMigrationPreflight.sql'))
        $script:sqlCaseCount = 0
        foreach ($fixture in @(
            @{ Database = $Migration60Database; Count = 60 },
            @{ Database = $Migration61Database; Count = 61 },
            @{ Database = $Migration62Database; Count = 62 }
        )) {
            $connection = Open-FixtureConnection $fixture.Database
            try {
                $history = @(Invoke-ReadOnlyProbe $connection 'SELECT MigrationId FROM dbo.__EFMigrationsHistory ORDER BY MigrationId;')
                Assert-True ($history.Count -eq $fixture.Count) "Unexpected migration count for $($fixture.Database)."
                Assert-ProbeResult $connection $probeSql "clean migration-$($fixture.Count) fixture $($fixture.Database)"

                if ($fixture.Count -eq 60) {
                    # SQL projections replace only SELECT sources/metadata expressions in memory.
                    # No INSERT/UPDATE/DELETE/DDL reaches a fixture table or its migration history.
                    $sameTenantDuplicate = '(SELECT CAST(0x01000000000000000000000000000000 AS uniqueidentifier) TenantId, CAST(0x02000000000000000000000000000000 AS uniqueidentifier) InvoiceId UNION ALL SELECT CAST(0x01000000000000000000000000000000 AS uniqueidentifier), CAST(0x02000000000000000000000000000000 AS uniqueidentifier)) FixtureSalesOrders'
                    $query = Replace-ExactlyOnce $probeSql 'FROM dbo.SalesOrders WHERE InvoiceId' "FROM $sameTenantDuplicate WHERE InvoiceId"
                    Assert-ProbeResult $connection $query 'duplicate invoice links within one tenant are blocked' 'Inventory61:DuplicateSalesInvoiceLinks'
                    $differentTenants = $sameTenantDuplicate.Replace('UNION ALL SELECT CAST(0x01000000000000000000000000000000', 'UNION ALL SELECT CAST(0x03000000000000000000000000000000')
                    $query = Replace-ExactlyOnce $probeSql 'FROM dbo.SalesOrders WHERE InvoiceId' "FROM $differentTenants WHERE InvoiceId"
                    Assert-ProbeResult $connection $query 'same invoice ID in different tenants does not falsely block'
                    $unlinkedOrders = $sameTenantDuplicate.Replace('CAST(0x02000000000000000000000000000000 AS uniqueidentifier)', 'CAST(NULL AS uniqueidentifier)')
                    $query = Replace-ExactlyOnce $probeSql 'FROM dbo.SalesOrders WHERE InvoiceId' "FROM $unlinkedOrders WHERE InvoiceId"
                    Assert-ProbeResult $connection $query 'multiple unlinked Sales orders do not falsely block'

                    $receiptDuplicates = '(SELECT 1 TenantId, 2 VendorInvoiceLineItemId, 3 GoodsReceiptNoteItemId UNION ALL SELECT 1, 2, 3) FixtureReceiptAllocations'
                    $query = Replace-ExactlyOnce $probeSql 'FROM dbo.VendorInvoiceReceiptAllocations' "FROM $receiptDuplicates"
                    Assert-ProbeResult $connection $query 'duplicate receipt allocations are blocked' 'Inventory61:DuplicateReceiptAllocations'
                    $separateReceipts = $receiptDuplicates.Replace('UNION ALL SELECT 1, 2, 3', 'UNION ALL SELECT 1, 2, 4')
                    $query = Replace-ExactlyOnce $probeSql 'FROM dbo.VendorInvoiceReceiptAllocations' "FROM $separateReceipts"
                    Assert-ProbeResult $connection $query 'one invoice line allocated to distinct receipts does not falsely block'
                    $query = Replace-ExactlyOnce $probeSql 'FROM dbo.PhysicalCountItems WHERE CountedQuantity' 'FROM (SELECT CAST(-1 AS decimal(18,4)) CountedQuantity) FixtureCountItems WHERE CountedQuantity'
                    Assert-ProbeResult $connection $query 'negative retained counted quantity is blocked' 'Inventory61:NegativeCountedQuantity'
                    $query = Replace-ExactlyOnce $probeSql 'FROM dbo.PhysicalCountActions WHERE ActionType' 'FROM (SELECT 18 ActionType) FixtureCountActions WHERE ActionType'
                    Assert-ProbeResult $connection $query 'action outside the widened count range is blocked' 'Inventory61:InvalidCountActionType'
                    $query = Replace-ExactlyOnce $probeSql 'FROM dbo.PhysicalCountActions WHERE ActionType' 'FROM (SELECT 17 ActionType) FixtureCountActions WHERE ActionType'
                    Assert-ProbeResult $connection $query 'newly supported count action 17 does not falsely block'

                    foreach ($trigger in @(
                        'TR_StockMovements_GovernedInventoryTransfer', 'TR_PhysicalCountActions_AppendOnly',
                        'TR_PhysicalCountItems_ControlledMutation', 'TR_PhysicalCounts_ControlledLifecycle',
                        'TR_SupplierDebitNotes_InventoryReturnCreditGuard', 'TR_WarehouseQuantities_PhysicalCountFreeze',
                        'TR_InventoryItems_PhysicalCountFreeze', 'TR_StockMovements_PhysicalCountFreeze'
                    )) {
                        $query = Set-ReadOnlyTriggerProjection $probeSql $trigger '' 'Absent'
                        Assert-ProbeResult $connection $query "migration 61 missing trigger $trigger is blocked" "Inventory61:TriggerPredicate:$trigger"
                    }
                    foreach ($case in @(
                        @{ Trigger = 'TR_StockMovements_GovernedInventoryTransfer'; Predicate = "WHERE i.MovementType IN (N'TransferOut', N'TransferIn', N'TransferReversal', N'TransferDiscrepancyReturn', N'TransferReplacementIn')" },
                        @{ Trigger = 'TR_SupplierDebitNotes_InventoryReturnCreditGuard'; Predicate = 'WHERE i.InventoryPurchaseReturnId IS NOT NULL AND (r.Id IS NULL' },
                        @{ Trigger = 'TR_SupplierDebitNotes_InventoryReturnCreditGuard'; Predicate = 'WHERE i.DirectInvoiceAppliedAmount>0 AND (p.Id IS NULL' }
                    )) {
                        foreach ($mode in @('Missing', 'Repeated')) {
                            $query = Set-ReadOnlyTriggerProjection $probeSql $case.Trigger $case.Predicate $mode
                            Assert-ProbeResult $connection $query "migration 61 $mode predicate in $($case.Trigger) is blocked" "Inventory61:TriggerPredicate:$($case.Trigger)"
                        }
                    }
                    $columnExpression = "COL_LENGTH(N'dbo.'+QUOTENAME(a.TableName),a.ColumnName)"
                    $query = Replace-ExactlyOnce $probeSql $columnExpression "(CASE WHEN a.TableName=N'StockMovements' AND a.ColumnName=N'TransferLeg' THEN 40 ELSE $columnExpression END)"
                    Assert-ProbeResult $connection $query 'partially added migration 61 column is blocked' 'Inventory61:UnexpectedPendingSchema:StockMovements.TransferLeg'
                    $tableCheck = "a.ColumnName IS NULL AND OBJECT_ID(N'dbo.'+QUOTENAME(a.TableName),N'U') IS NOT NULL"
                    $query = Replace-ExactlyOnce $probeSql $tableCheck "a.ColumnName IS NULL AND (a.TableName=N'PhysicalCountCounters' OR OBJECT_ID(N'dbo.'+QUOTENAME(a.TableName),N'U') IS NOT NULL)"
                    Assert-ProbeResult $connection $query 'partially added migration 61 table is blocked' 'Inventory61:UnexpectedPendingSchema:PhysicalCountCounters.<table>'

                }
                if ($fixture.Count -eq 61) {
                    $trigger = 'TR_InventoryIssueVouchers_ControlledLifecycle'
                    Assert-ProbeResult $connection (Set-ReadOnlyTriggerProjection $probeSql $trigger '' 'Absent') 'migration 62 missing trigger is blocked' "Inventory62:TriggerPredicate:$trigger"
                    foreach ($predicate in @(
                        'i.ApprovedById <> d.ApprovedById', 'i.ApprovedById = d.ApprovedById',
                        'r.ApprovedById <> i.ApprovedById',
                        'VALUES (i.RequestedById),(i.ApprovedById),(i.IssuedById),(i.ReceiverUserId)',
                        'SET NOCOUNT ON;'
                    )) {
                        foreach ($mode in @('Missing', 'Repeated')) {
                            $query = Set-ReadOnlyTriggerProjection $probeSql $trigger $predicate $mode
                            Assert-ProbeResult $connection $query "migration 62 $mode predicate '$predicate' is blocked" "Inventory62:TriggerPredicate:$trigger"
                        }
                    }
                    $query = Replace-ExactlyOnce $probeSql "AND name=N'ApprovedById' AND is_nullable=1" "AND name=N'ApprovedById' AND is_nullable IN (0,1)"
                    Assert-ProbeResult $connection $query 'unrecorded nullable approver schema is blocked' 'Inventory62:UnexpectedNullableApprover'
                }
                # Execute every real packaged SQL probe, then feed its actual coverage and
                # database history into the same deployment gate that stopped the VPS.
                $script:fixtureConnection = $connection
                function Invoke-DatabaseTable {
                    param([string]$Query)
                    Invoke-ReadOnlyProbe $script:fixtureConnection $Query
                }
                $actualOutput = @(Invoke-CanonicalMigrationPreflight)
                $actualHistory = @($history | ForEach-Object { "MIGRATION_ID|$($_.MigrationId)" })
                $expectedPending = 62 - $fixture.Count
                $actualComparison = Compare-MigrationState -RemoteOutput ($actualHistory + $actualOutput) -RequireCurrent ($expectedPending -eq 0)
                Assert-True ($actualComparison.pending.Count -eq $expectedPending) 'Real packaged probe/SQL history comparison returned unexpected pending migrations.'
                Write-Host "PASS: all packaged SQL probes and deployment comparison on migration $($fixture.Count); pending=$expectedPending."
                $afterHistory = @(Invoke-ReadOnlyProbe $connection 'SELECT MigrationId FROM dbo.__EFMigrationsHistory ORDER BY MigrationId;')
                Assert-True (@(Compare-Object @($history.MigrationId) @($afterHistory.MigrationId)).Count -eq 0) 'Read-only tests changed migration history.'
            } finally { $connection.Dispose() }
        }
        Write-Host "PASS: $script:sqlCaseCount read-only SQL cases; no application or fixture data was mutated."
    }
} finally {
    # This is the only deletion: the uniquely named temporary packaging directory created above.
    $resolvedPath = [IO.Path]::GetFullPath($temporaryRoot)
    $temporaryParent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    if ($resolvedPath.StartsWith($temporaryParent, [StringComparison]::OrdinalIgnoreCase) -and (Split-Path $resolvedPath -Leaf) -match '^rhema-inventory-preflight-[a-f0-9]{32}$') {
        Remove-Item -LiteralPath $resolvedPath -Recurse -Force
    } else { throw 'Unsafe temporary fixture cleanup path.' }
}
