[CmdletBinding()]
param(
    [ValidateSet('Preview', 'Rehearse', 'Apply')][string]$Mode = 'Preview',
    [string]$ExpectedFingerprint,
    [string]$ReportPath = 'local-artifacts/uat-reset-20260905/preview.json'
)

# One-off, explicitly authorized LOCAL UAT reset. Never deploy/run on a server.
# Reads the existing development connection without printing or persisting secrets.
# No backup: the operator explicitly confirmed that a backup already exists.
# Preserve security, configuration, customers, non-demo projects, and unrelated Finance/HR.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$tenant = '00000000-0000-0000-0000-000000000001'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$reportFullPath = [IO.Path]::GetFullPath((Join-Path $repo $ReportPath))
if (-not $reportFullPath.StartsWith((Join-Path $repo 'local-artifacts/'), [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Reports must stay inside this checkout/local-artifacts.'
}
$settings = Get-Content -LiteralPath (Join-Path $env:APPDATA 'Microsoft/UserSecrets/10483e62-e5b2-4652-8963-50f9500d3d5d/secrets.json') -Raw | ConvertFrom-Json
$connection = [System.Data.SqlClient.SqlConnection]::new([string]$settings.'ConnectionStrings:DefaultConnection')
$transaction = $null
$tables = @{}

function Query([string]$sql) {
    $command = $connection.CreateCommand()
    $command.CommandText = $sql
    $command.CommandTimeout = 120
    if ($null -ne $script:transaction) { $command.Transaction = $script:transaction }
    $reader = $command.ExecuteReader()
    try {
        while ($reader.Read()) {
            $record = [ordered]@{}
            for ($i = 0; $i -lt $reader.FieldCount; $i++) {
                $record[$reader.GetName($i)] = if ($reader.IsDBNull($i)) { $null } else { $reader.GetValue($i) }
            }
            [pscustomobject]$record
        }
    } finally { $reader.Dispose(); $command.Dispose() }
}

function Execute([string]$sql) {
    $command = $connection.CreateCommand(); $command.CommandText = $sql; $command.CommandTimeout = 120
    if ($null -ne $script:transaction) { $command.Transaction = $script:transaction }
    try { [void]$command.ExecuteNonQuery() } finally { $command.Dispose() }
}

function Quote([string]$value) { '[' + $value.Replace(']', ']]') + ']' }
function FullName($table) { (Quote $table.Schema) + '.' + (Quote $table.Name) }
function KeyJoin($table, [string]$left = 't', [string]$right = 's') {
    ($table.Keys | ForEach-Object { "$left.$(Quote $_)=$right.$(Quote $_)" }) -join ' AND '
}
function HasColumn($table, [string]$column) { $table.Columns.ContainsKey($column) }
function TenantFilter($table) { if (HasColumn $table 'TenantId') { "t.[TenantId]='$tenant'" } else { '1=1' } }

function AddScope([string]$name, [string]$predicate) {
    if (-not $tables.ContainsKey($name)) { return 0 }
    $table = $tables[$name]
    if ($table.Rows -eq 0) { return 0 }
    if ($table.Keys.Count -eq 0) { throw "No audited primary key for $name" }
    if (-not $table.Staged) {
        $columns = ($table.Keys | ForEach-Object { Quote $_ }) -join ','
        Execute "SELECT TOP (0) $columns INTO $($table.Temp) FROM $(FullName $table);"
        $table.Staged = $true
    }
    $selectKeys = ($table.Keys | ForEach-Object { 't.' + (Quote $_) }) -join ','
    $result = @(Query "INSERT INTO $($table.Temp) SELECT DISTINCT $selectKeys FROM $(FullName $table) t WHERE ($(TenantFilter $table)) AND ($predicate) AND NOT EXISTS(SELECT 1 FROM $($table.Temp) s WHERE $(KeyJoin $table)); SELECT @@ROWCOUNT Added;")
    $added = [int]$result[0].Added
    $table.Count += $added
    return $added
}

function IsAllowedChild([string]$name) {
    if ($name -match 'Configuration|Policy|Settings|Template|^AspNet|^User|^Role|^Permission|^Tenant|^Payroll|^Hr|^Employee') { return $false }
    if ($name -match '^Procurement(?!Responsibility|CalendarProfile|CalendarRule|SupplierEvidencePack|SupplierEvidenceRequirement)') { return $true }
    if ($name -match '^(Purchase|RequestForQuotation|Tender|Contract|BusinessPartner(?!s$)|Blacklist|Supplier(?!s$)|EmergencyProcurement|EmergencySupplier|GoodsReceipt|LandedCost|PhysicalCount|Stock|ItemSupplier|PriceHistor|QualityIncident|PerformanceReview|ConsignmentSettlement)') { return $true }
    if ($name -match '^Inventory(?!Items$|Categories$|LabelProfiles$|Locations$|IssueAccountingRules$|ReplenishmentPolicies$)') { return $true }
    if ($name -match '^Project(?!Catalog|Types$|Priorities$|Programs$|Portfolios$|StageGateRules$|UnitType)') { return $true }
    if ($name -match '^QuantitySurvey(?!RateLibrar)') { return $true }
    if ($name -match '^(VendorInvoice|VendorPayment|SupplierDebit|SupplierReturn|SubledgerSettlement|FinanceBudgetReservation|FinanceBudgetOverride|FinanceSourceDimension|FinanceControlledDocument|WithholdingTaxCertificate|WithholdingTaxRemittanceLine)') { return $true }
    return $name -in @(
        'ApSupplierIdentityLinks', 'WorkflowInstances', 'WorkflowStepInstances', 'WorkflowApprovals',
        'WorkflowActivityLogs', 'WorkflowEvidenceDocuments', 'WorkflowChecklistEvidence', 'WorkflowSignatureEvidence',
        'WorkflowIntegrationExecutions', 'WorkflowExecutionQueueItems', 'WorkflowActionReceipts', 'WorkflowApprovalActions',
        'WorkflowDelegations', 'WorkflowSlaEvents', 'WorkflowTaskActions', 'WorkflowTaskAssignments',
        'WorkflowCorrectionRequests', 'WorkflowEscalationExecutions',
        'JournalEntries', 'JournalEntryLines', 'JournalEntryAttachments', 'AccountTransactions', 'FinancePostingEvents',
        'Notifications', 'NotificationDeliveryAttempts', 'DocumentNumberReservations',
        'FinanceDimensionSnapshots', 'CivilEngineeringMaintenanceExecutionLinks', 'CivilEngineeringMaintenanceCostingHandoffs'
    )
}

function TableHash($table, [switch]$Remaining, [string[]]$ExcludeColumns = @()) {
    $columns = @($table.ColumnOrder | Where-Object { $_ -notin $ExcludeColumns })
    $selection = ($columns | ForEach-Object { 't.' + (Quote $_) }) -join ','
    $where = if ($Remaining -and $table.Count -gt 0) { "WHERE NOT EXISTS(SELECT 1 FROM $($table.Temp) s WHERE $(KeyJoin $table))" } else { '' }
    $order = if ($table.Keys.Count -gt 0) { 'ORDER BY ' + (($table.Keys | ForEach-Object { 't.' + (Quote $_) }) -join ',') } else { '' }
    return @(Query "SELECT COUNT_BIG(*) Rows, CONVERT(varchar(64),HASHBYTES('SHA2_256',CONVERT(nvarchar(max),(SELECT $selection FROM $(FullName $table) t $where $order FOR JSON PATH, INCLUDE_NULL_VALUES))),2) Hash FROM $(FullName $table) t $where;")[0]
}

try {
    $connection.Open()
    $identity = @(Query "SELECT DB_NAME() DatabaseName,CONVERT(nvarchar(200),SERVERPROPERTY('ServerName')) ServerName;")[0]
    if ($identity.DatabaseName -ne 'RhemaERP' -or $identity.ServerName -ne 'RHEMA-MICHAEL\SQL2017' -or $env:COMPUTERNAME -ne 'RHEMA-MICHAEL') {
        throw 'Local reset target does not match the explicitly inspected database and host.'
    }
    $tenantRow = @(Query "SELECT Id,Code FROM dbo.Tenants WHERE Id='$tenant' AND Code='DEFAULT';")
    if ($tenantRow.Count -ne 1) { throw 'Expected DEFAULT tenant not found.' }
    $metadata = @(Query @'
SELECT t.object_id ObjectId,SCHEMA_NAME(t.schema_id) SchemaName,t.name TableName,
 ISNULL((SELECT SUM(p.rows) FROM sys.partitions p WHERE p.object_id=t.object_id AND p.index_id IN(0,1)),0) Rows,
 c.name ColumnName,ty.name TypeName,c.column_id ColumnOrder,ISNULL(ic.key_ordinal,0) KeyOrder
FROM sys.tables t JOIN sys.columns c ON c.object_id=t.object_id JOIN sys.types ty ON ty.user_type_id=c.user_type_id
LEFT JOIN sys.indexes ix ON ix.object_id=t.object_id AND ix.is_primary_key=1
LEFT JOIN sys.index_columns ic ON ic.object_id=t.object_id AND ic.index_id=ix.index_id AND ic.column_id=c.column_id
WHERE t.is_ms_shipped=0 ORDER BY t.object_id,c.column_id;
'@)
    foreach ($row in $metadata) {
        if (-not $tables.ContainsKey($row.TableName)) {
            $tables[$row.TableName] = [pscustomobject]@{ Name=$row.TableName; Schema=$row.SchemaName; ObjectId=$row.ObjectId; Rows=[long]$row.Rows; Columns=@{}; ColumnOrder=[Collections.Generic.List[string]]::new(); Keys=@(); KeyOrders=@{}; Temp="#uat_$($row.ObjectId)"; Staged=$false; Count=0 }
        }
        $table = $tables[$row.TableName]; $table.Columns[$row.ColumnName]=$row.TypeName; $table.ColumnOrder.Add($row.ColumnName)
        if ($row.KeyOrder -gt 0) { $table.KeyOrders[$row.ColumnName]=[int]$row.KeyOrder }
    }
    foreach ($table in $tables.Values) { $table.Keys=@($table.KeyOrders.Keys | Sort-Object { $table.KeyOrders[$_] }) }
    $fkRows = @(Query @'
SELECT fk.object_id FkId,fk.name FkName,fk.is_disabled Disabled,fk.is_not_trusted Untrusted,
 child.name ChildTable,parent.name ParentTable,cc.name ChildColumn,pc.name ParentColumn,f.constraint_column_id Ordinal
FROM sys.foreign_keys fk JOIN sys.foreign_key_columns f ON f.constraint_object_id=fk.object_id
JOIN sys.tables child ON child.object_id=f.parent_object_id JOIN sys.tables parent ON parent.object_id=f.referenced_object_id
JOIN sys.columns cc ON cc.object_id=f.parent_object_id AND cc.column_id=f.parent_column_id
JOIN sys.columns pc ON pc.object_id=f.referenced_object_id AND pc.column_id=f.referenced_column_id
ORDER BY fk.object_id,f.constraint_column_id;
'@)
    $fks = @($fkRows | Group-Object FkId | ForEach-Object {
        $first=$_.Group[0]
        [pscustomobject]@{ Name=$first.FkName; Child=$first.ChildTable; Parent=$first.ParentTable; Disabled=$first.Disabled; Untrusted=$first.Untrusted; Parts=@($_.Group) }
    })

    # Explicit business roots. Non-demo projects and customer/contractor masters are NOT roots.
    $roots = @('ProcurementBudgets','ProcurementPlans','MarketAnalyses','PurchaseRequisitions','RequestForQuotations','Tenders','PurchaseOrders','Contracts','BusinessPartnerRegistrations','Suppliers','EmergencyProcurementPlans','ProcurementFrameworkAgreements','ProcurementPrequalificationExercises','InventoryRequisitions','InventoryAllocations','InventoryBalances','InventoryMovements','StockMovements','PhysicalCounts','InventoryScanBatches','InventoryLabelPrintEvents','InventoryTransfers','StockAdjustments','GoodsReceiptNotes','LandedCosts','PurchaseReturns','InventoryCostLayers','InventoryLayers','InventoryIssueVouchers','InventoryReturnVouchers','InventoryDirectedTasks','InventoryDisposalCases','InventoryValuationReconciliations','InventoryReplenishmentRecommendations')
    foreach ($root in $roots) {
        if ($root -eq 'InventoryAllocations') {
            [void](AddScope $root "t.AllocationType<>'WorkOrder'")
        } else { [void](AddScope $root '1=1') }
    }
    [void](AddScope 'SupplierConsolidations' '1=1')
    [void](AddScope 'BusinessPartners' "t.PartnerType='Supplier'")
    [void](AddScope 'Projects' "t.ProjectCode LIKE 'PRJ-DEMO-%'")
    Execute 'CREATE TABLE #AllUatIds (Id uniqueidentifier NOT NULL PRIMARY KEY);'

    # Traverse dependencies, not table-name-wide deletion. Every child keeps its tenant boundary.
    for ($round=1; $round -le 30; $round++) {
        $added=0
        foreach ($table in @($tables.Values | Where-Object { $_.Count -gt 0 -and $_.Columns['Id'] -eq 'uniqueidentifier' })) {
            Execute "INSERT INTO #AllUatIds SELECT s.Id FROM $($table.Temp) s WHERE NOT EXISTS(SELECT 1 FROM #AllUatIds a WHERE a.Id=s.Id);"
        }
        foreach ($fk in $fks) {
            $parent=$tables[$fk.Parent]; $child=$tables[$fk.Child]
            if ($parent.Count -eq 0 -or $child.Rows -eq 0 -or -not (IsAllowedChild $child.Name)) { continue }
            $join=($fk.Parts | ForEach-Object { 't.'+(Quote $_.ChildColumn)+'=p.'+(Quote $_.ParentColumn) }) -join ' AND '
            $predicate="EXISTS(SELECT 1 FROM $(FullName $parent) p JOIN $($parent.Temp) s ON $(KeyJoin $parent 'p' 's') WHERE $join)"
            $added += AddScope $child.Name $predicate
        }
        # Shared runtime entities use typed IDs rather than physical foreign keys.
        foreach ($table in @($tables.Values | Where-Object { $_.Rows -gt 0 -and (IsAllowedChild $_.Name) })) {
            $links=@('EntityId','SourceDocumentId','SourceId','ReferenceId','DocumentId')
            if ($table.Name -eq 'ProcurementMasterDataChangeRequests') { $links+=@('TargetId') }
            if ($table.Name -like 'Workflow*') { $links+=@('WorkflowInstanceId','StepInstanceId','ApprovalId') }
            foreach ($column in $links) {
                if ((HasColumn $table $column) -and $table.Columns[$column] -eq 'uniqueidentifier') {
                    $added += AddScope $table.Name "EXISTS(SELECT 1 FROM #AllUatIds a WHERE a.Id=t.$(Quote $column))"
                }
            }
        }
        # Owner -> workflow/GL posting links are explicitly reviewed ownership edges.
        foreach ($owner in @($tables.Values | Where-Object { $_.Count -gt 0 })) {
            foreach ($link in @(@('WorkflowInstanceId','WorkflowInstances'),@('JournalEntryId','JournalEntries'),@('PostingEventId','FinancePostingEvents'))) {
                if ((HasColumn $owner $link[0]) -and $owner.Columns[$link[0]] -eq 'uniqueidentifier') {
                    $added += AddScope $link[1] "EXISTS(SELECT 1 FROM $(FullName $owner) o JOIN $($owner.Temp) s ON $(KeyJoin $owner 'o' 's') WHERE o.$(Quote $link[0])=t.Id)"
                }
            }
        }
        $added += AddScope 'WorkflowOfflineActions' "EXISTS(SELECT 1 FROM OPENJSON(CASE WHEN ISJSON(t.Payload)=1 THEN t.Payload ELSE '{}' END) j JOIN #AllUatIds a ON a.Id=TRY_CONVERT(uniqueidentifier,j.value) WHERE j.type=1)"
        Write-Host "Dependency pass $round`: $added additional records"
        if ($added -eq 0) { break }
        if ($round -eq 30) { throw 'Dependency graph did not converge.' }
    }
    $boundaries=[Collections.Generic.List[object]]::new()
    foreach ($fk in $fks) {
        $parent=$tables[$fk.Parent]; $child=$tables[$fk.Child]
        if ($parent.Count -eq 0 -or $child.Rows -eq 0) { continue }
        $join=($fk.Parts | ForEach-Object { 't.'+(Quote $_.ChildColumn)+'=p.'+(Quote $_.ParentColumn) }) -join ' AND '
        $remaining=if ($child.Count -gt 0) { "AND NOT EXISTS(SELECT 1 FROM $($child.Temp) d WHERE $(KeyJoin $child 't' 'd'))" } else { '' }
        $count=@(Query "SELECT COUNT_BIG(*) Records FROM $(FullName $child) t WHERE EXISTS(SELECT 1 FROM $(FullName $parent) p JOIN $($parent.Temp) s ON $(KeyJoin $parent 'p' 's') WHERE $join) $remaining;")[0].Records
        # Preserve the maintenance asset; explicitly clear only its demo-project assignment.
        $isDetach = $child.Name -eq 'MaintenanceAssets' -and $parent.Name -eq 'Projects' -and
            $fk.Parts.Count -eq 1 -and $fk.Parts[0].ChildColumn -eq 'CurrentProjectId'
        if ($count -gt 0 -and -not $isDetach) { $boundaries.Add([pscustomobject]@{Child=$child.Name;Parent=$parent.Name;ForeignKey=$fk.Name;Records=$count}) }
    }
    $manifest=[Collections.Generic.List[object]]::new()
    foreach ($table in @($tables.Values | Where-Object { $_.Count -gt 0 } | Sort-Object Name)) {
        $columns=($table.ColumnOrder | ForEach-Object {'t.'+(Quote $_)}) -join ','
        $order=($table.Keys | ForEach-Object {'t.'+(Quote $_)}) -join ','
        $hash=@(Query "SELECT CONVERT(varchar(64),HASHBYTES('SHA2_256',CONVERT(nvarchar(max),(SELECT $columns FROM $(FullName $table) t JOIN $($table.Temp) s ON $(KeyJoin $table) ORDER BY $order FOR JSON PATH, INCLUDE_NULL_VALUES))),2) Hash;")[0].Hash
        $manifest.Add([pscustomobject]@{Table=$table.Name;Records=$table.Count;Hash=$hash})
    }
    $fingerprintInput=($manifest | ConvertTo-Json -Depth 5 -Compress)
    $fingerprint=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($fingerprintInput)))
    $report=[ordered]@{Mode=$Mode;Database=$identity.DatabaseName;Server=$identity.ServerName;Tenant=$tenant;Utc=[DateTime]::UtcNow.ToString('o');Fingerprint=$fingerprint;Records=($manifest | Measure-Object Records -Sum).Sum;Tables=$manifest;Boundaries=$boundaries;Backup='Operator already backed up; no new backup requested';Applied=$false}
    [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($reportFullPath))
    [IO.File]::WriteAllText($reportFullPath,($report | ConvertTo-Json -Depth 8),[Text.UTF8Encoding]::new($false))
    if ($Mode -eq 'Preview') { $manifest | Format-Table Table,Records -AutoSize | Out-Host }
    Write-Host "Fingerprint: $fingerprint"
    Write-Host "Total scoped records: $($report.Records); boundary blockers: $($boundaries.Count)"
    if ($boundaries.Count -gt 0) { $boundaries | Format-Table -AutoSize | Out-Host; throw 'Unscoped dependent rows found; no deletion allowed.' }
    if ($Mode -eq 'Preview') { return }
    if (-not $ExpectedFingerprint -or $ExpectedFingerprint -cne $fingerprint) {
        throw 'The reviewed preview fingerprint does not match. No deletion allowed.'
    }
    if (@(Query "SELECT COUNT_BIG(*) Records FROM dbo.AccountCurrencyLinks;")[0].Records -ne 0) {
        throw 'Currency balance links were not present in the audited baseline; stop for a currency reconciliation review.'
    }
    # The application must be stopped, including its background writers, for this maintenance operation.
    if (@(Get-NetTCPConnection -LocalPort 5000 -State Listen -ErrorAction SilentlyContinue).Count -gt 0) {
        throw 'Stop the inspected local API process before rehearsal or apply.'
    }
    $transaction=$connection.BeginTransaction([System.Data.IsolationLevel]::Serializable)
    Execute "SET XACT_ABORT ON; SET LOCK_TIMEOUT 120000; DECLARE @lock int; EXEC @lock=sys.sp_getapplock @Resource='DEFAULT-UAT-RESET-20260905',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=0; IF @lock<0 THROW 51000,'Another reset holds the maintenance lock.',1;"
    # Ensure no source row changed between preview construction and the maintenance transaction.
    foreach ($entry in $manifest) {
        $table=$tables[$entry.Table]
        $columns=($table.ColumnOrder | ForEach-Object {'t.'+(Quote $_)}) -join ','
        $order=($table.Keys | ForEach-Object {'t.'+(Quote $_)}) -join ','
        $now=@(Query "SELECT COUNT_BIG(*) Records, CONVERT(varchar(64),HASHBYTES('SHA2_256',CONVERT(nvarchar(max),(SELECT $columns FROM $(FullName $table) t JOIN $($table.Temp) s ON $(KeyJoin $table) ORDER BY $order FOR JSON PATH, INCLUDE_NULL_VALUES))),2) Hash FROM $(FullName $table) t JOIN $($table.Temp) s ON $(KeyJoin $table);")[0]
        if ($now.Records -ne $entry.Records -or $now.Hash -cne $entry.Hash) { throw "Preview changed for $($table.Name)." }
    }
    Execute 'CREATE TABLE #UatCacheItems (Id uniqueidentifier NOT NULL PRIMARY KEY);'
    foreach ($name in @('PurchaseRequisitionItems','PurchaseOrderItems','InventoryRequisitionItems','InventoryMovements')) {
        $table=$tables[$name]
        if ($table.Count -gt 0 -and (HasColumn $table 'InventoryItemId')) {
            Execute "INSERT INTO #UatCacheItems SELECT DISTINCT t.InventoryItemId FROM $(FullName $table) t JOIN $($table.Temp) s ON $(KeyJoin $table) JOIN dbo.InventoryItems i ON i.Id=t.InventoryItemId AND i.TenantId='$tenant' WHERE NOT EXISTS(SELECT 1 FROM #UatCacheItems x WHERE x.Id=t.InventoryItemId);"
        }
    }
    # A PR can mention an item whose stock belongs to maintenance. Do not wipe that
    # shared baseline merely because it appeared on a disposable procurement test.
    Execute "IF EXISTS(SELECT 1 FROM #UatCacheItems x JOIN dbo.InventoryAllocations a ON a.InventoryItemId=x.Id AND a.AllocationType='WorkOrder' JOIN dbo.InventoryMovements m ON m.InventoryItemId=x.Id JOIN $($tables['InventoryMovements'].Temp) s ON s.Id=m.Id WHERE a.TenantId='$tenant') THROW 51000,'A shared maintenance item has a scoped posted movement; reconcile its delta separately.',1; DELETE x FROM #UatCacheItems x WHERE EXISTS(SELECT 1 FROM dbo.InventoryAllocations a WHERE a.InventoryItemId=x.Id AND a.TenantId='$tenant' AND a.AllocationType='WorkOrder');"
    # Mirror FinancePostingEngine's normal-balance convention; subtract ONLY deleted postings.
    $accountTransactions=$tables['AccountTransactions']
    if ($accountTransactions.Count -eq 0) { throw 'Expected scoped accounting evidence is absent; review this new baseline.' }
    Execute "SELECT t.AccountId,SUM(CASE WHEN t.DebitAmount>0 THEN CASE WHEN a.AccountType IN(1,5) THEN t.DebitAmount ELSE -t.DebitAmount END ELSE CASE WHEN a.AccountType IN(2,3,4) THEN t.CreditAmount ELSE -t.CreditAmount END END) Delta INTO #AccountDeltas FROM dbo.AccountTransactions t JOIN $($accountTransactions.Temp) s ON t.Id=s.Id JOIN dbo.Accounts a ON a.Id=t.AccountId WHERE t.TenantId='$tenant' AND a.TenantId='$tenant' AND t.IsDeleted=0 AND t.PostingStatus='Posted' GROUP BY t.AccountId;"
    Execute "SELECT a.Id,a.Balance BeforeBalance,a.Balance-d.Delta AfterBalance INTO #AccountExpected FROM dbo.Accounts a JOIN #AccountDeltas d ON d.AccountId=a.Id;"
    $excluded=@{
        Accounts=@('Balance')
        InventoryItems=@('CurrentStock','AvailableStock','AllocatedStock','OnOrderStock','AverageCost','LastPurchaseCost','LastStockDate','LastPurchaseDate','LastSaleDate','LastCountDate','RowVersion')
        WarehouseQuantities=@('CurrentStock','AvailableStock','AllocatedStock','AverageCost','LastMovementDate','LastStockTakeDate')
        InventoryLocations=@('Quantity','AllocatedQuantity','AvailableQuantity','AverageCost','LastMovementDate','LastCountDate')
        MaintenanceAssets=@('CurrentProjectId','RowVersion')
    }
    # Exact hashes protect non-target rows, all other tenants and all untouched configuration.
    $preserved=@{}
    foreach ($table in @($tables.Values | Where-Object { $_.Rows -gt 0 } | Sort-Object Name)) {
        $omit=if ($excluded.ContainsKey($table.Name)) {$excluded[$table.Name]} else {@()}
        $preserved[$table.Name]=TableHash $table -Remaining -ExcludeColumns $omit
    }
    Write-Host "Preservation fingerprints captured for $($preserved.Count) populated tables."
    # Even excluded cache columns must remain identical on rows not covered by an exact update.
    $updateFilters=@{
        Accounts='EXISTS(SELECT 1 FROM #AccountExpected x WHERE x.Id=t.Id)'
        InventoryItems='EXISTS(SELECT 1 FROM #UatCacheItems x WHERE x.Id=t.Id)'
        WarehouseQuantities='EXISTS(SELECT 1 FROM #UatCacheItems x WHERE x.Id=t.InventoryItemId)'
        InventoryLocations='EXISTS(SELECT 1 FROM #UatCacheItems x WHERE x.Id=t.InventoryItemId)'
        MaintenanceAssets="EXISTS(SELECT 1 FROM $($tables['Projects'].Temp) x WHERE x.Id=t.CurrentProjectId)"
    }
    $report['DetachedDemoProjectAssignments']=@(Query "SELECT t.Id,t.CurrentProjectId FROM dbo.MaintenanceAssets t WHERE $($updateFilters['MaintenanceAssets']) ORDER BY t.Id;")
    $unmodifiedCacheHashes=@{}
    foreach ($name in $updateFilters.Keys) {
        $table=$tables[$name]; $columns=($table.ColumnOrder | ForEach-Object {'t.'+(Quote $_)}) -join ','
        $unmodifiedCacheHashes[$name]=@(Query "SELECT CONVERT(varchar(64),HASHBYTES('SHA2_256',CONVERT(nvarchar(max),(SELECT $columns FROM $(FullName $table) t WHERE NOT ($($updateFilters[$name])) ORDER BY t.Id FOR JSON PATH,INCLUDE_NULL_VALUES))),2) Hash;")[0].Hash
        # Store update IDs because detach predicates no longer match after the update.
        Execute "SELECT t.Id INTO #update_$($table.ObjectId) FROM $(FullName $table) t WHERE $($updateFilters[$name]);"
    }
    $touched=@($tables.Values | Where-Object { $_.Count -gt 0 -or $excluded.ContainsKey($_.Name) })
    $touchedIds=($touched.ObjectId | Sort-Object -Unique) -join ','
    $triggers=@(Query "SELECT tr.name TriggerName,t.name TableName,tr.is_disabled Disabled FROM sys.triggers tr JOIN sys.tables t ON tr.parent_id=t.object_id WHERE tr.parent_id IN($touchedIds) AND tr.is_ms_shipped=0;")
    $internalFks=@($fks | Where-Object { -not $_.Disabled -and $tables[$_.Child].Count -gt 0 -and $tables[$_.Parent].Count -gt 0 })
    try {
        foreach ($tr in $triggers | Where-Object {-not $_.Disabled}) { Execute "DISABLE TRIGGER $(Quote $tr.TriggerName) ON $(FullName $tables[$tr.TableName]);" }
        # Only internal reset edges are suspended, transactionally, after proving no outside dependents.
        foreach ($fk in $internalFks) { Execute "ALTER TABLE $(FullName $tables[$fk.Child]) NOCHECK CONSTRAINT $(Quote $fk.Name);" }
        Execute "UPDATE a SET Balance=x.AfterBalance FROM dbo.Accounts a JOIN #AccountExpected x ON x.Id=a.Id WHERE a.TenantId='$tenant';"
        Execute "UPDATE t SET CurrentStock=0,AvailableStock=0,AllocatedStock=0,OnOrderStock=0,AverageCost=0,LastPurchaseCost=0,LastStockDate=NULL,LastPurchaseDate=NULL,LastSaleDate=NULL,LastCountDate=NULL FROM dbo.InventoryItems t JOIN #UatCacheItems x ON x.Id=t.Id WHERE t.TenantId='$tenant';"
        Execute "UPDATE t SET CurrentStock=0,AvailableStock=0,AllocatedStock=0,AverageCost=0,LastMovementDate=NULL,LastStockTakeDate=NULL FROM dbo.WarehouseQuantities t JOIN #UatCacheItems x ON x.Id=t.InventoryItemId WHERE t.TenantId='$tenant';"
        Execute "UPDATE t SET Quantity=0,AllocatedQuantity=0,AvailableQuantity=0,AverageCost=0,LastMovementDate=NULL,LastCountDate=NULL FROM dbo.InventoryLocations t JOIN #UatCacheItems x ON x.Id=t.InventoryItemId WHERE t.TenantId='$tenant';"
        Execute "UPDATE t SET CurrentProjectId=NULL FROM dbo.MaintenanceAssets t JOIN $($tables['Projects'].Temp) x ON x.Id=t.CurrentProjectId WHERE t.TenantId='$tenant';"
        # Prefer child-first order; cycles are covered by the precisely scoped constraints above.
        $pending=@($tables.Values | Where-Object {$_.Count -gt 0}); $deletionOrder=[Collections.Generic.List[object]]::new()
        $childrenByParent=@{}
        foreach ($fk in $fks) {
            if (-not $childrenByParent.ContainsKey($fk.Parent)) { $childrenByParent[$fk.Parent]=[Collections.Generic.HashSet[string]]::new() }
            if ($fk.Child -ne $fk.Parent) { [void]$childrenByParent[$fk.Parent].Add($fk.Child) }
        }
        while ($pending.Count -gt 0) {
            $pendingNames=[Collections.Generic.HashSet[string]]::new([string[]]$pending.Name)
            $leaf=@($pending | Where-Object {
                $hasChild=$false
                if ($childrenByParent.ContainsKey($_.Name)) {
                    foreach ($childName in $childrenByParent[$_.Name]) { if ($pendingNames.Contains($childName)) { $hasChild=$true; break } }
                }
                -not $hasChild
            } | Sort-Object Name)
            if ($leaf.Count -eq 0) { $leaf=@($pending | Sort-Object Name | Select-Object -First 1) }
            foreach ($table in $leaf) { $deletionOrder.Add($table) }
            $pending=@($pending | Where-Object { $_.Name -notin $leaf.Name })
        }
        Write-Host "Deleting the reviewed rows from $($deletionOrder.Count) tables inside the transaction."
        foreach ($table in $deletionOrder) {
            Execute "DELETE t FROM $(FullName $table) t JOIN $($table.Temp) s ON $(KeyJoin $table); IF @@ROWCOUNT<>$($table.Count) THROW 51000,'Reset deletion count changed.',1;"
        }
        foreach ($fk in $internalFks) {
            $check=if ($fk.Untrusted) {'WITH NOCHECK CHECK'} else {'WITH CHECK CHECK'}
            Execute "ALTER TABLE $(FullName $tables[$fk.Child]) $check CONSTRAINT $(Quote $fk.Name);"
        }
        foreach ($tr in $triggers | Where-Object {-not $_.Disabled}) { Execute "ENABLE TRIGGER $(Quote $tr.TriggerName) ON $(FullName $tables[$tr.TableName]);" }
        Write-Host 'Deletion complete inside the transaction; verifying preservation and restored controls.'
        foreach ($name in $preserved.Keys) {
            $omit=if ($excluded.ContainsKey($name)) {$excluded[$name]} else {@()}
            $after=TableHash $tables[$name] -ExcludeColumns $omit
            if ($after.Rows -ne $preserved[$name].Rows -or $after.Hash -cne $preserved[$name].Hash) { throw "Preserved data changed in $name; rolling back." }
        }
        foreach ($name in $unmodifiedCacheHashes.Keys) {
            $table=$tables[$name];$columns=($table.ColumnOrder | ForEach-Object {'t.'+(Quote $_)}) -join ','
            $after=@(Query "SELECT CONVERT(varchar(64),HASHBYTES('SHA2_256',CONVERT(nvarchar(max),(SELECT $columns FROM $(FullName $table) t WHERE NOT EXISTS(SELECT 1 FROM #update_$($table.ObjectId) x WHERE x.Id=t.Id) ORDER BY t.Id FOR JSON PATH,INCLUDE_NULL_VALUES))),2) Hash;")[0].Hash
            if ($after -cne $unmodifiedCacheHashes[$name]) { throw "Unrelated cache rows changed in $name; rolling back." }
        }
        $emptyTableCount=0
        foreach ($table in $tables.Values | Where-Object {$_.Rows -eq 0}) {
            if (@(Query "SELECT COUNT_BIG(*) Records FROM $(FullName $table);")[0].Records -ne 0) { throw "Previously empty table $($table.Name) changed; rolling back." }
            $emptyTableCount++
        }
        $currentFkStates=@{}
        foreach ($state in @(Query 'SELECT name,is_disabled Disabled,is_not_trusted Untrusted FROM sys.foreign_keys;')) { $currentFkStates[$state.name]=$state }
        foreach ($fk in $fks) {
            if ($currentFkStates[$fk.Name].Disabled -ne $fk.Disabled -or $currentFkStates[$fk.Name].Untrusted -ne $fk.Untrusted) { throw "FK state changed for $($fk.Name); rolling back." }
        }
        $currentTriggerStates=@{}
        foreach ($state in @(Query "SELECT name,is_disabled Disabled FROM sys.triggers WHERE parent_id IN($touchedIds);")) { $currentTriggerStates[$state.name]=$state.Disabled }
        foreach ($tr in $triggers) { if ($currentTriggerStates[$tr.TriggerName] -ne $tr.Disabled) { throw "Trigger state changed for $($tr.TriggerName); rolling back." } }
        Execute "IF EXISTS(SELECT 1 FROM dbo.Accounts a JOIN #AccountExpected x ON x.Id=a.Id WHERE a.Balance<>x.AfterBalance) THROW 51000,'GL cache reconciliation failed.',1; IF EXISTS(SELECT 1 FROM dbo.InventoryItems i JOIN #UatCacheItems x ON x.Id=i.Id WHERE i.CurrentStock<>0 OR i.AvailableStock<>0 OR i.AllocatedStock<>0 OR i.OnOrderStock<>0) THROW 51000,'Inventory cache reconciliation failed.',1;"
        $report['PreservedTablesVerified']=$preserved.Count
        $report['PreservedEmptyTablesVerified']=$emptyTableCount
        $report['ForeignKeyStatesVerified']=$fks.Count
        $report['TriggerStatesVerified']=$triggers.Count
        $report['AccountAdjustments']=@(Query 'SELECT Id,BeforeBalance,AfterBalance FROM #AccountExpected ORDER BY Id;')
        $report['ClearedInventoryItems']=@(Query 'SELECT i.Id,i.ItemCode FROM dbo.InventoryItems i JOIN #UatCacheItems x ON x.Id=i.Id ORDER BY i.ItemCode;')
        $report['PreservedMaintenanceAllocations']=@(Query "SELECT COUNT_BIG(*) Records FROM dbo.InventoryAllocations WHERE TenantId='$tenant' AND AllocationType='WorkOrder';")[0].Records
        if ($Mode -eq 'Rehearse') { $transaction.Rollback(); $report['RehearsalRolledBack']=$true }
        else { $transaction.Commit(); $report['Applied']=$true }
        $transaction.Dispose(); $transaction=$null
        $report['CompletedAtUtc']=[DateTime]::UtcNow.ToString('o')
        [IO.File]::WriteAllText($reportFullPath,($report | ConvertTo-Json -Depth 8),[Text.UTF8Encoding]::new($false))
        Write-Host "$Mode complete; preserved-table verification: $($preserved.Count)."
    } catch {
        try { $transaction.Rollback() } catch { }
        throw
    }
} finally {
    if ($null -ne $transaction) { $transaction.Dispose() }
    $connection.Dispose()
}
