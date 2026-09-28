param(
    [ValidateSet('Clone','Capture','Verify')][string]$Mode = 'Clone',
    [string]$CloneEvidence,
    [string]$Baseline,
    [switch]$RequireIdempotent,
    [string]$Workspace = (Join-Path $PSScriptRoot '..\..')
)
$ErrorActionPreference = 'Stop'
$Workspace = [IO.Path]::GetFullPath($Workspace)
$server = 'RHEMA-MICHAEL\SQL2017'
$source = 'RhemaERP_ReceiptUpgrade_20260927_023608_918225d0'
$latest = '20260927211546_InventoryIssueOptionalWorkflowApproval'
$target = $null
$connection = $null
$stage = 'initialization'
$stamp = [DateTime]::UtcNow.ToString('yyyyMMdd_HHmmss') + '_' + [Guid]::NewGuid().ToString('N').Substring(0,8)
$evidence = Join-Path $Workspace ('tmp\qs-uat-sql-' + $Mode.ToLowerInvariant() + '-' + $stamp + '.json')
$result = [ordered]@{Mode=$Mode; Server=$server; Source=$source; StartedUtc=[DateTime]::UtcNow; Passed=$false}

# Only digests and row identities leave SQL. Passwords, password hashes, connection
# strings and profile contents are never returned or persisted as evidence.
$preserveTables = @('Users','BusinessPartners','BusinessPartnerApProfileVersions','BusinessPartnerArProfileVersions',
    'BusinessPartnerApWhtDefaults','EstateManagedAssets','EstateLandDemarcations','ProjectDevelopmentProfiles')
$transactionTables = @('JournalEntries','AccountTransactions','FinancePostingEvents','VendorInvoice','VendorInvoiceLineItem',
    'Invoices','VendorPayment','VendorPaymentAllocation','CustomerPayment','CashTransaction',
    'InventoryMovements','StockMovements','InventoryBalances','StockAdjustments','InventoryRequisitions','InventoryIssueVouchers',
    'PurchaseOrders','PurchaseOrderReceipts','PurchaseRequisitions','ProjectPaymentCertificates','StampDutyPayments','TenderPayments')
$countOnlyTables = @('UserRoles','UserTenants','Roles','Employees','WorkflowDefinitions','WorkflowSteps')

function Sql([string]$Text,[hashtable]$Parameters=@{},[switch]$Scalar,[switch]$Rows) {
    $command=$connection.CreateCommand(); $command.CommandTimeout=600; $command.CommandText=$Text
    foreach($entry in $Parameters.GetEnumerator()){[void]$command.Parameters.AddWithValue('@'+$entry.Key,$entry.Value)}
    try {
        if($Scalar){return $command.ExecuteScalar()}
        if($Rows){$table=[Data.DataTable]::new();$reader=$command.ExecuteReader();try{$table.Load($reader)}finally{$reader.Dispose()};return ,$table}
        [void]$command.ExecuteNonQuery()
    } finally {$command.Dispose()}
}
function Switch-Db([string]$Name) {
    if($Name -cne 'master' -and $Name -cne $source -and $Name -cne $target){throw 'Database outside verification scope.'}
    $connection.ChangeDatabase($Name)
    if([string](Sql 'SELECT DB_NAME()' -Scalar) -cne $Name -or
       [string](Sql "SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))" -Scalar) -ine $server){throw 'SQL target identity mismatch.'}
}
function Snapshot {
    $snapshot=[ordered]@{Database=[string](Sql 'SELECT DB_NAME()' -Scalar); MigrationCount=[int](Sql 'SELECT COUNT(*) FROM dbo.__EFMigrationsHistory' -Scalar);
        LatestMigration=[string](Sql 'SELECT MAX(MigrationId) FROM dbo.__EFMigrationsHistory' -Scalar); Existing=[ordered]@{}; Transactions=[ordered]@{}; Counts=[ordered]@{}}
    if($snapshot.MigrationCount -ne 62 -or $snapshot.LatestMigration -cne $latest){throw 'Expected migration62 database required.'}
    foreach($table in ($preserveTables + $transactionTables)) {
        if([int](Sql 'SELECT COUNT(*) FROM sys.tables WHERE schema_id=SCHEMA_ID(''dbo'') AND name=@name' @{name=$table} -Scalar) -ne 1){throw "Required invariant table missing: $table"}
        if($table -in $preserveTables) {
            $rows=Sql "SELECT CONVERT(nvarchar(128),t.Id) AS RowId, CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT t.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)),2) AS RowHash FROM dbo.[$table] t ORDER BY t.Id" -Rows
            $snapshot.Existing[$table]=@($rows.Rows | ForEach-Object{[pscustomobject]@{Id=[string]$_.RowId;Hash=[string]$_.RowHash}})
        } else {
            $hash=[string](Sql "SELECT CONVERT(varchar(64),HASHBYTES('SHA2_256',COALESCE((SELECT * FROM dbo.[$table] ORDER BY Id FOR JSON PATH,INCLUDE_NULL_VALUES),N'[]')),2)" -Scalar)
            $snapshot.Transactions[$table]=[pscustomobject]@{Count=[long](Sql "SELECT COUNT_BIG(*) FROM dbo.[$table]" -Scalar); Hash=$hash}
        }
    }
    foreach($table in $countOnlyTables) {
        if([int](Sql 'SELECT COUNT(*) FROM sys.tables WHERE schema_id=SCHEMA_ID(''dbo'') AND name=@name' @{name=$table} -Scalar) -eq 1){
            $snapshot.Counts[$table]=[long](Sql "SELECT COUNT_BIG(*) FROM dbo.[$table]" -Scalar)
        }
    }
    return [pscustomobject]$snapshot
}
function Compare-Snapshots($Before,$After,[switch]$Exact) {
    if($Before.MigrationCount -ne $After.MigrationCount -or $Before.LatestMigration -cne $After.LatestMigration){throw 'Migration history changed.'}
    foreach($table in $preserveTables) {
        $stageBefore=@($Before.Existing.$table);$stageAfter=@($After.Existing.$table)
        $current=@{}; foreach($row in $stageAfter){$current[[string]$row.Id]=[string]$row.Hash}
        foreach($row in $stageBefore){if(!$current.ContainsKey([string]$row.Id) -or $current[[string]$row.Id] -cne [string]$row.Hash){$script:stage="preserved row mismatch: $table";throw 'Existing row changed or disappeared.'}}
        if($Exact -and $stageBefore.Count -ne $stageAfter.Count){$script:stage="repeat row count mismatch: $table";throw 'Repeated seed added or removed master rows.'}
    }
    foreach($table in $transactionTables) {
        if($Before.Transactions.$table.Count -ne $After.Transactions.$table.Count -or $Before.Transactions.$table.Hash -cne $After.Transactions.$table.Hash){$script:stage="business transaction mismatch: $table";throw 'Business transaction rows changed.'}
    }
    if($Exact){foreach($property in $Before.Counts.PSObject.Properties){if($property.Value -ne $After.Counts.($property.Name)){$script:stage="repeat link count mismatch: $($property.Name)";throw 'Repeated seed changed identity/workflow link count.'}}}
}

try {
    if($Mode -eq 'Clone'){$target='RhemaERP_QsUatVerify_'+$stamp}
    else {
        if(!$CloneEvidence){throw 'Prepared clone evidence is required.'}
        $prepared=Get-Content -LiteralPath $CloneEvidence -Raw | ConvertFrom-Json
        if(!$prepared.Passed -or !$prepared.CloneCreated -or $prepared.Server -ine $server -or $prepared.Source -cne $source){throw 'Invalid prepared clone evidence.'}
        $target=[string]$prepared.Target
    }
    if($target -notmatch '^RhemaERP_QsUatVerify_20260928_[0-9]{6}_[a-f0-9]{8}$' -or $target -ceq $source){throw 'Target is not an owned QS UAT verification copy.'}
    $result.Target=$target
    $builder=[Data.SqlClient.SqlConnectionStringBuilder]::new()
    $builder['Data Source']=$server;$builder['Initial Catalog']='master';$builder['Integrated Security']=$true
    $builder['TrustServerCertificate']=$true;$builder['Connect Timeout']=30
    $connection=[Data.SqlClient.SqlConnection]::new($builder.ConnectionString);$connection.Open();Switch-Db 'master'
    if($Mode -eq 'Clone') {
        $stage='source read-only baseline';Switch-Db $source;$before=Snapshot;$result.SourceBefore=$before
        Switch-Db 'master'
        if([int](Sql 'SELECT COUNT(*) FROM sys.databases WHERE name=@name' @{name=$target} -Scalar) -ne 0){throw 'Target already exists; no overwrite allowed.'}
        $dataRoot=[string](Sql "SELECT CONVERT(nvarchar(260),SERVERPROPERTY('InstanceDefaultDataPath'))" -Scalar)
        $logRoot=[string](Sql "SELECT CONVERT(nvarchar(260),SERVERPROPERTY('InstanceDefaultLogPath'))" -Scalar)
        $backupRoot=[string](Sql "SELECT CONVERT(nvarchar(260),SERVERPROPERTY('InstanceDefaultBackupPath'))" -Scalar)
        if(!$dataRoot -or !$logRoot){throw 'SQL data/log roots unavailable.'};if(!$backupRoot){$backupRoot=$dataRoot}
        $backup=Join-Path $backupRoot ($target+'.bak')
        if(Test-Path -LiteralPath $backup){throw 'Backup path exists; no overwrite allowed.'}
        $stage='copy-only checksum backup'
        Sql "BACKUP DATABASE [$source] TO DISK=@backup WITH COPY_ONLY,CHECKSUM,COMPRESSION;" @{backup=$backup}
        Sql 'RESTORE VERIFYONLY FROM DISK=@backup WITH CHECKSUM;' @{backup=$backup}
        $result.Backup=$backup;$result.BackupVerified=$true
        $files=Sql 'RESTORE FILELISTONLY FROM DISK=@backup;' @{backup=$backup} -Rows
        $moves=[Collections.Generic.List[string]]::new();$parameters=@{backup=$backup};$index=0
        foreach($file in $files.Rows){
            if([string]$file.Type -notin @('D','L')){throw 'Unsupported backup file type.'}
            $root=if($file.Type -eq 'L'){$logRoot}else{$dataRoot}
            $extension=if($file.Type -eq 'L'){'.ldf'}elseif($index -eq 0){'.mdf'}else{'.ndf'}
            $filePath=[IO.Path]::GetFullPath((Join-Path $root ($target+'_'+$index+$extension)))
            $checkedRoot=[IO.Path]::GetFullPath($root).TrimEnd('\')+'\'
            if(!$filePath.StartsWith($checkedRoot,[StringComparison]::OrdinalIgnoreCase) -or (Test-Path -LiteralPath $filePath)){throw 'Unsafe or occupied restore file path.'}
            $parameters['logical'+$index]=[string]$file.LogicalName;$parameters['file'+$index]=$filePath
            $moves.Add("MOVE @logical$index TO @file$index");$index++
        }
        if($index -lt 2){throw 'Unexpected backup file layout.'}
        $stage='isolated restore'
        Sql ("RESTORE DATABASE [$target] FROM DISK=@backup WITH "+($moves -join ',')+',RECOVERY,CHECKSUM;') $parameters
        $result.CloneCreated=$true
        Sql "DBCC CHECKDB ([$target]) WITH PHYSICAL_ONLY,NO_INFOMSGS;"
        Switch-Db $target;$restored=Snapshot
        Compare-Snapshots ($before | ConvertTo-Json -Depth 12 | ConvertFrom-Json) ($restored | ConvertTo-Json -Depth 12 | ConvertFrom-Json) -Exact
        $result.Snapshot=$restored
        Switch-Db $source;$after=Snapshot
        Compare-Snapshots ($before | ConvertTo-Json -Depth 12 | ConvertFrom-Json) ($after | ConvertTo-Json -Depth 12 | ConvertFrom-Json) -Exact
        $result.SourceUnchanged=$true;$result.PhysicalCheckPassed=$true
    } else {
        $stage='read-only target snapshot';Switch-Db $target;$snapshot=Snapshot;$result.Snapshot=$snapshot
        if($Mode -eq 'Verify'){
            if(!$Baseline){throw 'Verification baseline is required.'}
            $baselineEvidence=Get-Content -LiteralPath $Baseline -Raw | ConvertFrom-Json
            if(!$baselineEvidence.Passed -or $baselineEvidence.Target -cne $target -or !$baselineEvidence.Snapshot){throw 'Baseline does not identify this exact clone.'}
            Compare-Snapshots $baselineEvidence.Snapshot ($snapshot | ConvertTo-Json -Depth 12 | ConvertFrom-Json) -Exact:$RequireIdempotent
            $result.ExistingPasswordsProfilesAndLandPreserved=$true;$result.NoBusinessTransactionsChanged=$true
            $result.IdempotentMasterCountsVerified=[bool]$RequireIdempotent
        }
    }
    $result.Passed=$true
    Write-Output "PASS|$Mode|$server|$target"
} catch {
    $result.FailureStage=$stage;$result.ErrorType=$_.Exception.GetType().FullName
    $inner=$_.Exception;while($inner.InnerException){$inner=$inner.InnerException}
    $result.SqlError=if($inner -is [Data.SqlClient.SqlException]){$inner.Number}else{$null}
    Write-Output "FAIL|$Mode|Stage=$stage|Type=$($result.ErrorType)|SqlError=$($result.SqlError)"
} finally {
    if($connection){$connection.Dispose()};$builder=$null
    $result.FinishedUtc=[DateTime]::UtcNow
    $result | ConvertTo-Json -Depth 14 | Set-Content -LiteralPath $evidence -Encoding utf8
    Write-Output "EVIDENCE|$evidence"
}
if(!$result.Passed){exit 1}
