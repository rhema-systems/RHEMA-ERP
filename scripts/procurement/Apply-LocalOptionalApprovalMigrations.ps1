# Separate from the rollback harness: Plan is read-only; Apply needs the exact
# reviewed SQL and per-database baseline hashes plus an explicit apply switch.
# Do not use while people are editing. This script does not stop/start servers.
param(
    [Parameter(Mandatory)][ValidateSet('RhemaERP','RhemaERP_PO_Rehearsal_20260909')][string]$Database,
    [ValidateSet('Plan','Apply')][string]$Mode='Plan',
    [ValidateSet('Initial','LegacyModules','InventoryFinal','TransferAutoComplete','TransferDraftLineGuard')][string]$Phase='Initial',
    [Parameter(Mandatory)][string]$ReviewedSqlPath,
    [Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$ReviewedSqlSha256,
    [ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$ReviewedBaselineSha256,
    [switch]$ConfirmApply
)
$ErrorActionPreference='Stop'
$applyMode=$Mode
$applyPhase=$Phase
$applySqlPath=(Resolve-Path -LiteralPath $ReviewedSqlPath).Path
$applySqlHash=$ReviewedSqlSha256
$applyBaselineHash=$ReviewedBaselineSha256
$applyConfirmed=[bool]$ConfirmApply
$applyHelperPath=$PSCommandPath
$applyReaderPath=Join-Path $PSScriptRoot 'Test-LocalOptionalApprovalMigrations.ps1'
# Hold one reviewed byte buffer through Audit, backup and apply. A later edit of
# the artifact cannot change the SQL executed inside this transaction.
$applySqlBytes=[IO.File]::ReadAllBytes($applySqlPath)
$applySqlHasher=[Security.Cryptography.SHA256]::Create()
try{$applyLoadedSqlHash=[Convert]::ToHexString($applySqlHasher.ComputeHash($applySqlBytes))}
finally{$applySqlHasher.Dispose()}
if($applyLoadedSqlHash -ne $applySqlHash){throw 'Reviewed SQL hash mismatch.'}
$applySql=[Text.UTF8Encoding]::new($false,$true).GetString($applySqlBytes).TrimStart([char]0xFEFF)
if($applyMode -eq 'Apply' -and (-not $applyConfirmed -or -not $applyBaselineHash)){throw 'Apply requires ConfirmApply and the reviewed per-database Plan baseline hash.'}

# Import the already-reviewed SELECT/hash helpers through their read-only mode.
# Audit verifies exact SQL server/database/tenant and the selected fixed baseline.
# Its own connection is disposed on return. The rollback path is never invoked.
. $applyReaderPath -Database $Database -Mode Audit -Phase $applyPhase
$previewConnection=[System.Data.SqlClient.SqlConnection]::new(
    "Server=RHEMA-MICHAEL\SQL2017;Database=$Database;Integrated Security=True;Application Name=ReviewedOptionalApprovalApply")
$previewTransaction=$null
$applyCommitted=$false
$applyCommitAttempted=$false
$applyBackupPath=$null

function Get-ApplyHash($Value){
    $applyHasher=[Security.Cryptography.SHA256]::Create()
    try{[Convert]::ToHexString($applyHasher.ComputeHash([Text.Encoding]::UTF8.GetBytes(($Value|ConvertTo-Json -Depth 15 -Compress))))}
    finally{$applyHasher.Dispose()}
}
function Read-ApplySnapshot {
    [ordered]@{
        Database=$Database; Server='RHEMA-MICHAEL\SQL2017'; Phase=$applyPhase; SqlSha256=$applySqlHash;
        ApplyHelperSha256=(Get-FileHash -LiteralPath $applyHelperPath -Algorithm SHA256).Hash;
        ReaderHelperSha256=(Get-FileHash -LiteralPath $applyReaderPath -Algorithm SHA256).Hash;
        History=@(Read-PreviewRows 'SELECT MigrationId,ProductVersion FROM dbo.__EFMigrationsHistory ORDER BY MigrationId');
        Schema=@(Read-PreviewSchema); Checks=@(Read-PreviewCheckConstraints); Tables=@(Read-PreviewTableInventory);
        Modules=@(Read-PreviewModules); Records=@(Read-PreviewRecords)
    }
}
function Assert-ApplyPostconditions($Before,$ExpectedHistory){
    Assert-PreviewEqual $Before.Records @(Read-PreviewRecords) 'Original business records'
    Assert-PreviewEqual $ExpectedHistory @(Read-PreviewRows 'SELECT MigrationId,ProductVersion FROM dbo.__EFMigrationsHistory ORDER BY MigrationId') 'Exact migration history'
    $applySchema=@(Read-PreviewSchema)
    $applyNewColumnCount=0
    foreach($applyNewTable in $previewNewTableColumns.Keys){$applyNewColumnCount+=$previewNewTableColumns[$applyNewTable].Count}
    if($applySchema.Count -ne $Before.Schema.Count+$previewFlagTables.Count+$previewNewLineageTables.Count+$applyNewColumnCount){throw 'Unexpected schema column changes.'}
    Assert-PreviewNewTables
    foreach($applyColumn in $Before.Schema){
        $applyNow=@($applySchema|Where-Object{$_.TableName -eq $applyColumn.TableName -and $_.ColumnName -eq $applyColumn.ColumnName})
        if($applyNow.Count -ne 1){throw 'An original schema column disappeared.'}
        $applyExpectedColumn=$applyColumn|ConvertTo-Json -Depth 4 -Compress|ConvertFrom-Json
        if($applyColumn.TableName -in $previewNullableDefinitionTables -and $applyColumn.ColumnName -eq 'WorkflowDefinitionId'){$applyExpectedColumn.Nullable=$true}
        Assert-PreviewEqual $applyExpectedColumn $applyNow[0] 'Original column contract'
    }
    foreach($applyTable in $previewFlagTables){
        $applyFlag=@($applySchema|Where-Object{$_.TableName -eq $applyTable -and $_.ColumnName -eq 'ApprovalRequired'})
        $applyDefault=if($applyFlag.Count -eq 1){$applyFlag[0].DefaultDefinition -replace '[()\[\]\s]',''}else{''}
        if($applyFlag.Count -ne 1 -or $applyFlag[0].SqlType -ne 'bit' -or $applyFlag[0].Nullable -ne $false -or $applyDefault -notin @('1','CAST1ASbit','CONVERTbit,1')){throw "$applyTable required=true default was not retained."}
        if(@(Read-PreviewRows "SELECT COUNT_BIG(*) InvalidRows FROM dbo.[$applyTable] WHERE ApprovalRequired<>1 OR ApprovalRequired IS NULL")[0].InvalidRows -ne 0){throw 'Historical approval policy was changed.'}
    }
    foreach($applyTable in $previewNewLineageTables){
        $applyLineage=@($applySchema|Where-Object{$_.TableName -eq $applyTable -and $_.ColumnName -eq 'WorkflowInstanceId'})
        if($applyLineage.Count -ne 1 -or $applyLineage[0].SqlType -ne 'uniqueidentifier' -or $applyLineage[0].Nullable -ne $true -or $applyLineage[0].DefaultDefinition){throw 'Invalid new workflow lineage column.'}
        if(@(Read-PreviewRows "SELECT COUNT_BIG(*) InvalidRows FROM dbo.[$applyTable] WHERE WorkflowInstanceId IS NOT NULL")[0].InvalidRows -ne 0){throw 'Historical workflow identity was invented.'}
    }
    $applyModules=@(Read-PreviewModules)
    foreach($applyModule in $Before.Modules){
        $applyNow=@($applyModules|Where-Object ObjectName -eq $applyModule.ObjectName)
        if($applyNow.Count -ne 1 -or $applyNow[0].IsDisabled -ne $applyModule.IsDisabled){throw 'Existing SQL module/enabled state changed unexpectedly.'}
        if($applyModule.ObjectName -notin $previewExpectedChangedModules -and $applyNow[0].DefinitionHash -ne $applyModule.DefinitionHash){throw "Unrelated SQL module changed: $($applyModule.ObjectName)"}
    }
    foreach($applyModule in $applyModules|Where-Object{$_.ObjectName -notin $Before.Modules.ObjectName}){
        if($applyModule.ObjectName -notin $previewExpectedChangedModules -or $applyModule.IsDisabled -eq $true){throw 'Unreviewed or disabled new SQL module.'}
    }
    $applyChecks=@(Read-PreviewCheckConstraints)
    $applyExpectedChecks=@{
        CK_InventoryReturnVouchers_Status='Status>=1ANDStatus<=6';
        CK_PhysicalCountActions_ActionType='ActionType>=1ANDActionType<=16';
        CK_ProcurementReceiptInspectionActions_Type='ActionType>=0ANDActionType<=15'
    }
    if($applyPhase -in @('LegacyModules','InventoryFinal','TransferAutoComplete','TransferDraftLineGuard')){$applyExpectedChecks=@{}}
    if($applyChecks.Count -ne $Before.Checks.Count){throw 'Unexpected CHECK constraint count.'}
    foreach($applyCheck in $Before.Checks){
        $applyNow=@($applyChecks|Where-Object{$_.TableName -eq $applyCheck.TableName -and $_.ConstraintName -eq $applyCheck.ConstraintName})
        if($applyNow.Count -ne 1){throw 'An original CHECK constraint disappeared.'}
        if($applyPhase -eq 'LegacyModules' -and $applyCheck.ConstraintName -in @('CK_ProcurementExceptionalSourcingControls_Lifecycle','CK_ProcurementPrequalificationExercises_Evidence')){
            # Compare the whole prior expression with only the reviewed optional-workflow clauses changed.
            $applyExpectedDefinition=$applyCheck.Definition.Replace('[WorkflowInstanceId] IS NOT NULL','([ApprovalRequired]=(0) OR [WorkflowInstanceId] IS NOT NULL)')
            if($applyCheck.ConstraintName -eq 'CK_ProcurementExceptionalSourcingControls_Lifecycle'){
                $applyExpectedDefinition=$applyExpectedDefinition.Replace('[ApprovedAtUtc] IS NOT NULL AND [ApprovedById] IS NOT NULL','([ApprovalRequired]=(0) OR [ApprovedAtUtc] IS NOT NULL AND [ApprovedById] IS NOT NULL)')
            }
            if($applyNow[0].IsDisabled -or $applyNow[0].IsNotTrusted -or
                ($applyNow[0].Definition -replace '[()\[\]\s]','') -ine ($applyExpectedDefinition -replace '[()\[\]\s]','')){throw 'Reviewed sourcing CHECK differs beyond optional internal approval.'}
        }elseif($applyExpectedChecks.ContainsKey($applyCheck.ConstraintName)){
            if($applyNow[0].IsDisabled -or $applyNow[0].IsNotTrusted -or ($applyNow[0].Definition -replace '[()\[\]\s]','') -ine $applyExpectedChecks[$applyCheck.ConstraintName]){throw 'Reviewed action/status CHECK was not retained as trusted and enabled.'}
        }else{Assert-PreviewEqual $applyCheck $applyNow[0] 'Unrelated CHECK constraint'}
    }
    Assert-PreviewInventoryFinal $Before.Schema $Before.Modules $Before.Checks $Before.Tables
}

try{
    $previewConnection.Open()
    $applyIdentity=@(Read-PreviewRows "SELECT DB_NAME() DatabaseName,CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) ServerName")[0]
    if($applyIdentity.DatabaseName -ne $Database -or $applyIdentity.ServerName -ne 'RHEMA-MICHAEL\SQL2017'){throw 'Exact target identity mismatch.'}
    if($applySql -match '(?im)^\s*(COMMIT\b|ROLLBACK\b|BEGIN\s+TRAN|USE\s|BACKUP\b|RESTORE\b|DISABLE\s+TRIGGER)'){throw 'Reviewed SQL contains forbidden transaction/database/guard controls.'}
    $applyBatches=@([regex]::Split($applySql,'(?im)^\s*GO\s*\r?$')|Where-Object{$_.Trim()})
    $applyHistoryRows=@([regex]::Matches($applySql,"(?is)INSERT\s+INTO\s+\[__EFMigrationsHistory\]\s*\(\[MigrationId\],\s*\[ProductVersion\]\)\s*VALUES\s*\(N'([^']+)',\s*N'([^']+)'\)")|ForEach-Object{[pscustomobject]@{MigrationId=$_.Groups[1].Value;ProductVersion=$_.Groups[2].Value}})
    Assert-PreviewEqual $previewMigrations @($applyHistoryRows.MigrationId) 'Exact reviewed migration IDs'
    if($applyHistoryRows.Count -ne $previewMigrations.Count){throw 'Only the fixed selected checkpoint migrations can be applied.'}
    foreach($applyBatch in $applyBatches){
        if($applyBatch -match '(?i)INSERT\s+INTO\s+\[__EFMigrationsHistory\]' -and $applyBatch.Trim() -notmatch "(?is)^INSERT\s+INTO\s+\[__EFMigrationsHistory\]\s*\(\[MigrationId\],\s*\[ProductVersion\]\)\s*VALUES\s*\(N'[^']+',\s*N'[^']+'\);?\s*$"){throw 'Unexpected combined migration history batch.'}
    }
    $applyBefore=Read-ApplySnapshot
    $applyCurrentBaseline=Get-ApplyHash $applyBefore
    if($applyBefore.History.Count -ne $previewBaselineCount -or $applyBefore.History[-1].MigrationId -ne $previewBaseline){throw 'The exact reviewed baseline changed.'}
    if($applyMode -eq 'Plan'){
        [pscustomobject]@{Mode='Plan';Phase=$applyPhase;Database=$Database;SqlSha256=$applySqlHash;BaselineSha256=$applyCurrentBaseline;Migrations=$previewMigrations.Count;SqlBatches=$applyBatches.Count;ProtectedBusinessTables=$previewTables.Count;BackupCreated=$false;Applied=$false}|ConvertTo-Json -Compress
        return
    }
    if($applyCurrentBaseline -ne $applyBaselineHash){throw 'Database/helper baseline changed since Plan. No backup/apply performed; obtain a new reviewed Plan.'}
    $applyBackupDirectory='C:/Program Files/Microsoft SQL Server/MSSQL15.SQL2017/MSSQL/Backup'
    if(-not(Test-Path -LiteralPath $applyBackupDirectory -PathType Container)){throw 'Verified local SQL backup directory is unavailable.'}
    $applyBackupPath=Join-Path $applyBackupDirectory ($Database+'_before_optional_approval_'+[DateTime]::UtcNow.ToString('yyyyMMdd_HHmmss_fff')+'.bak')
    if(Test-Path -LiteralPath $applyBackupPath){throw 'Refusing to overwrite a backup.'}
    $applyBackupCommand=$previewConnection.CreateCommand()
    $applyBackupCommand.CommandTimeout=600
    $applyBackupCommand.CommandText="BACKUP DATABASE [$Database] TO DISK=@backup WITH COPY_ONLY,CHECKSUM,COMPRESSION; RESTORE VERIFYONLY FROM DISK=@backup WITH CHECKSUM;"
    [void]$applyBackupCommand.Parameters.AddWithValue('@backup',$applyBackupPath)
    try{[void]$applyBackupCommand.ExecuteNonQuery()}finally{$applyBackupCommand.Dispose()}
    Write-Output "Verified fresh COPY_ONLY/CHECKSUM backup: $applyBackupPath"
    if((Get-ApplyHash (Read-ApplySnapshot)) -ne $applyBaselineHash){throw 'Concurrent change during backup; schema apply refused.'}

    $previewTransaction=$previewConnection.BeginTransaction([System.Data.IsolationLevel]::Serializable)
    Invoke-PreviewBatch 'SET XACT_ABORT ON; SET LOCK_TIMEOUT 5000; SET DEADLOCK_PRIORITY LOW;'
    Invoke-PreviewBatch "DECLARE @lockResult int; EXEC @lockResult=sys.sp_getapplock @Resource=N'TDC-OptionalApproval-Schema',@LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=5000; IF @lockResult<0 THROW 51999,'Optional approval migration lock unavailable.',1;"
    # Lock each protected owner and history before the final baseline check.
    # This never disables a trigger or any stock/finance/approval control.
    foreach($applyTable in @($previewTables+'__EFMigrationsHistory'|Sort-Object -Unique)){
        Invoke-PreviewBatch "DECLARE @lockedRows bigint; SELECT @lockedRows=COUNT_BIG(*) FROM dbo.[$applyTable] WITH(TABLOCKX,HOLDLOCK);"
    }
    if((Get-ApplyHash (Read-ApplySnapshot)) -ne $applyBaselineHash){throw 'Concurrent change before exclusive ownership; schema apply refused.'}
    $applyBatchNumber=0
    foreach($applyBatch in $applyBatches){$applyBatchNumber++;Invoke-PreviewBatch $applyBatch}
    $applyExpectedHistory=@($applyBefore.History+$applyHistoryRows)
    Assert-ApplyPostconditions $applyBefore $applyExpectedHistory
    $applyCommitAttempted=$true
    $previewTransaction.Commit()
    $applyCommitted=$true
    $previewTransaction.Dispose();$previewTransaction=$null
    Assert-ApplyPostconditions $applyBefore $applyExpectedHistory
    [pscustomobject]@{Database=$Database;Phase=$applyPhase;Applied=$true;MigrationCount=($previewBaselineCount+$previewMigrations.Count);MigrationIds=$previewMigrations;SqlSha256=$applySqlHash;ReviewedBaselineSha256=$applyBaselineHash;VerifiedBackup=$applyBackupPath;OriginalBusinessRowsUnchanged=$true;HumanApprovalHistoryUnchanged=$true;PostedCountAndUatCountsUnchanged=$true}|ConvertTo-Json -Compress
}catch{
    $applyFailure=$_
    if($applyCommitAttempted){
        throw "Commit was attempted (confirmed=$applyCommitted). Do not rerun or restore automatically; verify migration history and business fingerprints. Backup: $applyBackupPath. Error: $($applyFailure.Exception.Message)"
    }
    if($previewTransaction){
        $applyOwnedTransaction=$previewTransaction;$previewTransaction=$null
        try{$applyOwnedTransaction.Rollback()}catch{
            if($previewConnection.State -ne [System.Data.ConnectionState]::Open){$previewConnection.Close();$previewConnection.Open()}
            if(@(Read-PreviewRows 'SELECT @@TRANCOUNT OpenTransactions')[0].OpenTransactions -ne 0){throw 'Rollback is unverified: SQL transaction remains open.'}
        }finally{$applyOwnedTransaction.Dispose()}
        if((Get-ApplyHash (Read-ApplySnapshot)) -ne $applyBaselineHash){throw "Rollback/business baseline differs; inspect concurrent changes. Original error: $($applyFailure.Exception.Message)"}
        throw "Apply failed before commit; full reviewed baseline was verified restored. Batch $applyBatchNumber. Error: $($applyFailure.Exception.Message)"
    }
    throw
}finally{
    if($previewTransaction){
        try{if(-not $applyCommitAttempted){$previewTransaction.Rollback()}}finally{$previewTransaction.Dispose()}
    }
    $previewConnection.Dispose()
}
