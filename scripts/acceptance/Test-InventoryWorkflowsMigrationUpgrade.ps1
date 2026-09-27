# Local SQL Server acceptance only. Prepare makes a unique COPY_ONLY restore of
# the explicitly approved populated verification database. Apply accepts only
# the exact prepared target and one generated 60-to-61 migration script.
# The app database, secrets, and runtime configuration are never changed.
param(
    [ValidateSet('Prepare','Apply')][string]$Mode='Prepare',
    [string]$PreparedEvidence,
    [string]$MigrationSql,
    [ValidatePattern('^20\d{12}_InventoryControlledWorkflowsAndAccounting$')][string]$ExpectedMigration,
    [string]$Workspace=(Join-Path $PSScriptRoot '..\..')
)
$ErrorActionPreference='Stop'
$Workspace=[IO.Path]::GetFullPath($Workspace)
$source='RhemaERP_ReceiptUpgrade_20260927_023608_918225d0'
$server='RHEMA-MICHAEL\SQL2017'
$previous='20260927021852_InventoryIssueActualReceipts'
$next=$ExpectedMigration
$prefix='RhemaERP_InventoryWorkflowsUpgrade_'
$connection=$null; $stage='configuration'; $created=$false
$script:preservationColumns=[ordered]@{}
$result=[ordered]@{Mode=$Mode; Source=$source; Server=$server; StartedUtc=[DateTime]::UtcNow; Passed=$false; MigrationApplied=$false}
function Sql([string]$Text,[hashtable]$Parameters=@{},[switch]$Scalar,[switch]$Rows) {
    $cmd=$connection.CreateCommand();$cmd.CommandTimeout=600;$cmd.CommandText=$Text
    foreach($entry in $Parameters.GetEnumerator()){[void]$cmd.Parameters.AddWithValue('@'+$entry.Key,$entry.Value)}
    try {
        if($Scalar){return $cmd.ExecuteScalar()}
        if($Rows){$table=[Data.DataTable]::new();$reader=$cmd.ExecuteReader();try{$table.Load($reader)}finally{$reader.Dispose()};return ,$table}
        [void]$cmd.ExecuteNonQuery()
    } finally {$cmd.Dispose()}
}
function Switch-Db([string]$Name) {
    if($Name -ne 'master' -and $Name -cne $source -and $Name -cne $target){throw 'Database switch outside exact rehearsal scope.'}
    $connection.ChangeDatabase($Name)
    if([string](Sql 'SELECT DB_NAME()' -Scalar) -cne $Name){throw 'Database identity verification failed.'}
    if([string](Sql "SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))" -Scalar) -ine $server){throw 'Physical SQL instance changed.'}
}
function Snapshot {
    $data=[ordered]@{}
    $data.LatestMigration=[string](Sql 'SELECT MAX(MigrationId) FROM dbo.__EFMigrationsHistory' -Scalar)
    $data.MigrationCount=[int](Sql 'SELECT COUNT(*) FROM dbo.__EFMigrationsHistory' -Scalar)
    foreach($table in @('BusinessPartnerApProfileVersions','BusinessPartnerArProfileVersions','BusinessPartnerApWhtDefaults',
        'StockAdjustments','StockAdjustmentItems','StockAdjustmentActions','InventoryRequisitions','InventoryRequisitionItems',
        'InventoryMovements','InventoryBalances','InventoryItems','InventoryLocations','StockMovements','WarehouseQuantities',
        'JournalEntries','AccountTransactions')) {
        if(!$script:preservationColumns.Contains($table)) {
            $columns=Sql 'SELECT name FROM sys.columns WHERE object_id=OBJECT_ID(@table) ORDER BY column_id' @{table='dbo.'+$table} -Rows
            if($columns.Rows.Count -eq 0){throw 'Expected preservation table is missing.'}
            $script:preservationColumns[$table]=(@($columns.Rows|ForEach-Object{'['+([string]$_.name).Replace(']',']]')+']'}) -join ',')
        }
        $projection=$script:preservationColumns[$table]
        $data[$table+'Count']=[long](Sql "SELECT COUNT_BIG(*) FROM dbo.[$table]" -Scalar)
        $data[$table+'Hash']=[string](Sql "SELECT CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT $projection FROM dbo.[$table] ORDER BY Id FOR JSON PATH,INCLUDE_NULL_VALUES)),2)" -Scalar)
    }
    $data.LegacyPartnerAccountsHash=[string](Sql 'SELECT CONVERT(varchar(64),HASHBYTES(''SHA2_256'',(SELECT Id,DefaultApAccountId,DefaultArAccountId,DefaultExpenseAccountId FROM dbo.BusinessPartners ORDER BY Id FOR JSON PATH,INCLUDE_NULL_VALUES)),2)' -Scalar)
    $data.JournalCount=[long](Sql 'SELECT COUNT_BIG(*) FROM dbo.JournalEntries' -Scalar)
    $data.AccountTransactionCount=[long](Sql 'SELECT COUNT_BIG(*) FROM dbo.AccountTransactions' -Scalar)
    return [pscustomobject]$data
}
function Same-Snapshot($Before,$After,[switch]$AccountingOnly) {
    foreach($property in $Before.PSObject.Properties) {
        if($AccountingOnly -and $property.Name -in @('LatestMigration','MigrationCount')){continue}
        if([string]$property.Value -cne [string]$After.($property.Name)){throw ('Preservation verification failed: '+$property.Name)}
    }
}
function Guard-Target([string]$Name) {
    if($Name -notmatch '^RhemaERP_InventoryWorkflowsUpgrade_[0-9]{8}_[0-9]{6}_[a-f0-9]{8}$' -or $Name -eq $source -or $Name -eq 'RhemaERP'){throw 'Invalid rehearsal target.'}
}
try {
    if($Mode -eq 'Prepare') {
        $stamp=[DateTime]::UtcNow.ToString('yyyyMMdd_HHmmss')+'_'+[Guid]::NewGuid().ToString('N').Substring(0,8)
        $target=$prefix+$stamp
        $evidence=Join-Path $Workspace ('tmp\inventory-workflows-upgrade-'+$stamp+'-prepared.json')
    } else {
        if(!$PreparedEvidence -or !$MigrationSql){throw 'Apply requires prepared evidence and root-supplied generated SQL.'}
        $prepared=Get-Content -LiteralPath $PreparedEvidence -Raw | ConvertFrom-Json
        if(!$prepared.Passed -or !$prepared.CloneCreated -or $prepared.Source -cne $source -or $prepared.Server -ine $server){throw 'Prepared clone evidence is invalid.'}
        $target=[string]$prepared.Target
        foreach($property in $prepared.PreservationColumns.PSObject.Properties){$script:preservationColumns[$property.Name]=[string]$property.Value}
        if(!$ExpectedMigration){throw 'Apply requires the exact expected migration identity.'}
        $evidence=Join-Path $Workspace ('tmp\inventory-workflows-upgrade-'+$target.Substring($prefix.Length)+'-applied.json')
    }
    Guard-Target $target; $result.Target=$target
    # Exact parent-approved local verification source; credentials are not read or emitted.
    $builder=[Data.SqlClient.SqlConnectionStringBuilder]::new()
    $builder['Data Source']=$server;$builder['Initial Catalog']='master'
    $builder['Integrated Security']=$true;$builder['Connect Timeout']=30;$builder['TrustServerCertificate']=$true
    $connection=[Data.SqlClient.SqlConnection]::new($builder.ConnectionString);$connection.Open();Switch-Db 'master'
    if([int](Sql 'SELECT COUNT(*) FROM sys.databases WHERE name=@name AND state_desc=''ONLINE''' @{name=$source} -Scalar) -ne 1){throw 'Exact approved source clone is unavailable.'}
    Switch-Db $source; $sourceBefore=Snapshot; $result.SourceBefore=$sourceBefore;$result.PreservationColumns=$script:preservationColumns
    if($sourceBefore.LatestMigration -cne $previous -or $sourceBefore.MigrationCount -ne 60){throw 'Approved source is not at the expected migration predecessor.'}
    if($Mode -eq 'Prepare') {
        Sql @'
IF NOT EXISTS(SELECT 1 FROM dbo.StockAdjustments WHERE Id='486da09b-8e8c-4a6d-9797-11ee9396187e')
 OR NOT EXISTS(SELECT 1 FROM dbo.JournalEntries WHERE Id='78eafcb5-1477-474e-a709-53efc4f36179')
 THROW 51021,'Approved opening-stock source evidence is missing.',1;
'@
        $stage='copy-only verified backup'
        Switch-Db 'master'
        if([int](Sql 'SELECT COUNT(*) FROM sys.databases WHERE name=@name' @{name=$target} -Scalar) -ne 0){throw 'Target already exists; refusing overwrite.'}
        $dataRoot=[string](Sql "SELECT CONVERT(nvarchar(260),SERVERPROPERTY('InstanceDefaultDataPath'))" -Scalar)
        $logRoot=[string](Sql "SELECT CONVERT(nvarchar(260),SERVERPROPERTY('InstanceDefaultLogPath'))" -Scalar)
        $backupRoot=[string](Sql "SELECT CONVERT(nvarchar(260),SERVERPROPERTY('InstanceDefaultBackupPath'))" -Scalar)
        if(!$dataRoot -or !$logRoot){throw 'SQL data/log directories are unavailable.'}
        if(!$backupRoot){$backupRoot=$dataRoot}
        $backup=Join-Path $backupRoot ($target+'.bak')
        if(Test-Path -LiteralPath $backup){throw 'Backup file already exists; refusing overwrite.'}
        $result.Backup=$backup
        Sql "BACKUP DATABASE [$source] TO DISK=@backup WITH COPY_ONLY,CHECKSUM,COMPRESSION;" @{backup=$backup}
        Sql 'RESTORE VERIFYONLY FROM DISK=@backup WITH CHECKSUM;' @{backup=$backup};$result.BackupVerified=$true
        $files=Sql 'RESTORE FILELISTONLY FROM DISK=@backup;' @{backup=$backup} -Rows
        $moves=[Collections.Generic.List[string]]::new();$moveParams=@{backup=$backup};$fileEvidence=[Collections.Generic.List[object]]::new();$index=0
        foreach($file in $files.Rows) {
            if([string]$file.Type -notin @('D','L')){throw 'Unsupported backup file type; no restore attempted.'}
            $root=if($file.Type -eq 'L'){$logRoot}else{$dataRoot}
            $ext=if($file.Type -eq 'L'){'.ldf'}elseif($index -eq 0){'.mdf'}else{'.ndf'}
            $path=[IO.Path]::GetFullPath((Join-Path $root ($target+'_'+$index+$ext)))
            $checkedRoot=[IO.Path]::GetFullPath($root).TrimEnd('\')+'\'
            if(!$path.StartsWith($checkedRoot,[StringComparison]::OrdinalIgnoreCase) -or (Test-Path -LiteralPath $path)){throw 'Restore target file is unsafe or already exists.'}
            $moveParams['logical'+$index]=[string]$file.LogicalName;$moveParams['file'+$index]=$path
            $moves.Add("MOVE @logical$index TO @file$index")
            $fileEvidence.Add([pscustomobject]@{LogicalName=[string]$file.LogicalName;TargetFile=$path})
            $index++
        }
        if($index -lt 2){throw 'Unexpected backup file layout.'}
        $stage='restore isolated clone'
        Sql ("RESTORE DATABASE [$target] FROM DISK=@backup WITH "+($moves -join ',')+',RECOVERY,CHECKSUM;') $moveParams
        $created=$true;$result.CloneCreated=$true;$result.RestoreFiles=$fileEvidence
        Sql "DBCC CHECKDB ([$target]) WITH PHYSICAL_ONLY,NO_INFOMSGS;";$result.PhysicalCheckPassed=$true
        Switch-Db $target;$cloneBefore=Snapshot;Same-Snapshot $sourceBefore $cloneBefore;$result.CloneBefore=$cloneBefore
        Switch-Db $source;$sourceAfter=Snapshot;Same-Snapshot $sourceBefore $sourceAfter;$result.SourceAfter=$sourceAfter;$result.SourceUnchanged=$true
        $result.Passed=$true;$result.WaitingForGeneratedMigrationSql=$true
        Write-Output "PASS|Verified fresh predecessor clone|$target"
    } else {
        $stage='validate generated migration script'
        $result.ApiSha256=(Get-FileHash -LiteralPath (Join-Path $Workspace 'src/ErpSystem.Api/bin/Debug/net8.0/ErpSystem.Api.dll') -Algorithm SHA256).Hash
        $result.DataSha256=(Get-FileHash -LiteralPath (Join-Path $Workspace 'src/ErpSystem.Api/bin/Debug/net8.0/ErpSystem.Data.dll') -Algorithm SHA256).Hash
        $result.ExpectedMigration=$next
        $sqlPath=(Resolve-Path -LiteralPath $MigrationSql).Path
        $sqlText=[IO.File]::ReadAllText($sqlPath)
        if($sqlText -match '(?im)^[ \t]*(USE\b|CREATE[ \t]+DATABASE\b|DROP[ \t]+DATABASE\b|ALTER[ \t]+DATABASE\b)'){throw 'Migration script includes database-level commands outside scope.'}
        $migrationIds=@([regex]::Matches($sqlText,"N?'(20[0-9]{12}_[A-Za-z0-9_]+)'")|ForEach-Object {$_.Groups[1].Value}|Sort-Object -Unique)
        if($migrationIds.Count -ne 1 -or $migrationIds[0] -cne $next){throw 'Generated script does not contain only the expected target migration identity.'}
        $result.MigrationSql=$sqlPath;$result.MigrationSqlSha256=(Get-FileHash -LiteralPath $sqlPath -Algorithm SHA256).Hash
        Switch-Db $target;$cloneBefore=Snapshot;Same-Snapshot $prepared.CloneBefore $cloneBefore
        if($cloneBefore.LatestMigration -cne $previous){throw 'Target is not the verified migration predecessor.'}
        $result.CloneBefore=$cloneBefore
        $batches=@([regex]::Split($sqlText,'(?im)^[ \t]*GO[ \t]*(?:--[^\r\n]*)?\r?$')|Where-Object {!([string]::IsNullOrWhiteSpace($_))})
        $stage='apply exact generated migration to isolated clone';$batchCount=0
        foreach($batch in $batches) {
            if([string](Sql 'SELECT DB_NAME()' -Scalar) -cne $target){throw 'Migration connection changed database.'}
            $result.CurrentBatch=$batchCount+1
            Sql $batch;$batchCount++
            $result.ExecutedBatches=$batchCount
        }
        if([int](Sql 'SELECT @@TRANCOUNT' -Scalar) -ne 0){throw 'Generated migration left an uncommitted transaction.'}
        $result.ExecutedBatches=$batchCount
        $cloneAfter=Snapshot
        if($cloneAfter.LatestMigration -cne $next -or $cloneAfter.MigrationCount -ne ($cloneBefore.MigrationCount+1)){throw 'Target migration history is incorrect.'}
        Same-Snapshot $cloneBefore $cloneAfter -AccountingOnly
        Sql ([IO.File]::ReadAllText((Join-Path $PSScriptRoot 'InventoryWorkflowsSchemaAssertions.sql')))
        $result.CloneAfter=$cloneAfter;$result.MigrationApplied=$true;$result.SchemaAndGuardsVerified=$true
        Switch-Db $source;$sourceAfter=Snapshot;Same-Snapshot $sourceBefore $sourceAfter;Same-Snapshot $prepared.SourceBefore $sourceAfter
        $result.SourceAfter=$sourceAfter;$result.SourceUnchanged=$true;$result.Passed=$true
        Write-Output "PASS|Full EF migration upgrade preserved Finance data|$target"
    }
} catch {
    if($connection -and $connection.State -eq 'Open') {try{[void](Sql 'IF @@TRANCOUNT>0 ROLLBACK;')}catch{}}
    $result.FailureStage=$stage;$result.ErrorType=$_.Exception.GetType().FullName
    $inner=$_.Exception;while($inner.InnerException){$inner=$inner.InnerException}
    # Never serialize a connection string or a credential-bearing exception.
    $result.SqlError=if($inner -is [Data.SqlClient.SqlException]){$inner.Number}else{$null}
    if($inner -is [Data.SqlClient.SqlException] -and $stage -eq 'apply exact generated migration to isolated clone') {
        # This is our generated, credential-free DDL, not connection configuration.
        $result.MigrationErrors=@($inner.Errors | ForEach-Object {
            [pscustomobject]@{Number=$_.Number;Line=$_.LineNumber;Procedure=$_.Procedure;Message=$_.Message}
        })
    }
    Write-Output "FAIL|Upgrade rehearsal|Stage=$stage|Type=$($result.ErrorType)"
} finally {
    if($connection){$connection.Dispose()};$builder=$null
    $result.FinishedUtc=[DateTime]::UtcNow
    if($evidence){$result|ConvertTo-Json -Depth 8|Set-Content -LiteralPath $evidence -Encoding utf8;Write-Output "EVIDENCE|$evidence"}
    if($created -or ($Mode -eq 'Apply' -and $target)){Write-Output "TARGET_RETAINED|$target"}
}
if(!$result.Passed){exit 1}
