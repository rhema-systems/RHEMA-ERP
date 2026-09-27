<#
Run with Windows PowerShell 5.1:
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/acceptance/Test-InventoryReceiptConcurrency.ps1

Creates and retains a uniquely named isolated database on the verified local SQL2017
instance. Reads the API user-secret connection in memory; never prints credentials.
Requires CREATE DATABASE and permission to inspect blocking in sys.dm_exec_requests.
Uses exact production triggers against a minimal fixture, not a full ERP migration.
No running application database is opened or changed.
#>
param([string]$Workspace = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path)
$ErrorActionPreference = 'Stop'
$Workspace=(Resolve-Path -LiteralPath $Workspace).Path
[void](New-Item -ItemType Directory -Path (Join-Path $Workspace 'tmp') -Force)
$run = [DateTime]::UtcNow.ToString('yyyyMMdd_HHmmss') + '_' + [Guid]::NewGuid().ToString('N').Substring(0,8)
$database = 'RhemaERP_ReceiptConcurrent_' + $run
$server = 'RHEMA-MICHAEL\SQL2017'
if ($database -notmatch '^RhemaERP_ReceiptConcurrent_[0-9]{8}_[0-9]{6}_[a-f0-9]{8}$') { throw 'Invalid isolated database name.' }
$evidence = Join-Path $Workspace ('tmp\receipt-concurrency-' + $run + '.json')
$sqlEvidence = Join-Path $Workspace ('tmp\receipt-concurrency-' + $run + '.sql')
$results = [System.Collections.Generic.List[object]]::new()
$executed = [System.Collections.Generic.List[string]]::new()
$connection = $null
$stage = 'Read user-secret configuration'
$databaseCreated = $false
function Run-Sql([string]$sql) {
    $executed.Add($sql)
    $command = $connection.CreateCommand(); $command.CommandTimeout = 60; $command.CommandText = $sql
    try { [void]$command.ExecuteNonQuery() } finally { $command.Dispose() }
}
function Check-Sql([string]$name, [string]$sql, [int]$expected = 0) {
    $batch = "SET XACT_ABORT ON; BEGIN TRY BEGIN TRAN;`n$sql`nCOMMIT; SELECT 0; END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK; SELECT ERROR_NUMBER(); END CATCH;"
    $executed.Add("-- Check: $name; expected error $expected`n$batch")
    $command = $connection.CreateCommand(); $command.CommandTimeout = 60; $command.CommandText = $batch
    try { $actual = [int]$command.ExecuteScalar() } finally { $command.Dispose() }
    $pass = $actual -eq $expected
    $results.Add([pscustomobject]@{name=$name; passed=$pass; expectedSqlError=$expected; actualSqlError=$actual})
    Write-Output ("{0}|{1}|SQL={2}" -f $(if($pass){'PASS'}else{'FAIL'}),$name,$actual)
}
function Seed-Voucher([string]$voucher, [string]$line, [int]$sequence = 0) {
    return @"
INSERT dbo.InventoryIssueVouchers(Id,TenantId,InventoryRequisitionId,VoucherNumber,WarehouseId,DepartmentId,RequestedById,ApprovedById,IssuedById,ReceiverUserId,Status,ReceiptSequence,IssuedAtUtc,CostCenter,MovementReasonCode,IdempotencyKey,PayloadHash,CorrelationId,SourceSnapshotJson,IntegrityHash,CreatedAt,IsDeleted)
VALUES('$voucher','10000000-0000-0000-0000-000000000001','20000000-0000-0000-0000-000000000001','$voucher','30000000-0000-0000-0000-000000000001','40000000-0000-0000-0000-000000000001','50000000-0000-0000-0000-000000000001','50000000-0000-0000-0000-000000000002','50000000-0000-0000-0000-000000000003','50000000-0000-0000-0000-000000000004',1,$sequence,SYSUTCDATETIME(),'DEPT','DEPARTMENT_CONSUMPTION','$voucher',REPLICATE('A',64),'probe','{}',REPLICATE('B',64),SYSUTCDATETIME(),0);
INSERT dbo.InventoryIssueVoucherActions(Id,TenantId,InventoryIssueVoucherId,Sequence,ActionType,StatusAfter,ActorUserId,OccurredAtUtc,Comment,IntegrityHash,IsDeleted)
VALUES(NEWID(),'10000000-0000-0000-0000-000000000001','$voucher',1,1,1,'50000000-0000-0000-0000-000000000003',SYSUTCDATETIME(),'Issued',REPLICATE('C',64),0);
INSERT dbo.InventoryIssueVoucherLines(Id,TenantId,InventoryIssueVoucherId,Quantity,IsDeleted) VALUES('$line','10000000-0000-0000-0000-000000000001','$voucher',100,0);
"@
}
function Receive-Sql([string]$voucher,[string]$line,[decimal]$quantity,[int]$sequence,[bool]$final,[string]$mutation='') {
    $state = if($final){2}else{1}; $action = if($final){2}else{3}
    $ackActor = if($final){"'50000000-0000-0000-0000-000000000004'"}else{'NULL'}
    $ackTime = if($final){'@at'}else{'NULL'}
    return @"
DECLARE @action uniqueidentifier=NEWID(), @at datetime2=SYSUTCDATETIME();
INSERT dbo.InventoryIssueVoucherActions(Id,TenantId,InventoryIssueVoucherId,Sequence,ActionType,StatusAfter,ActorUserId,OccurredAtUtc,Comment,IntegrityHash,ReceiptIdempotencyKey,ReceiptPayloadHash,IsDeleted)
VALUES(@action,'10000000-0000-0000-0000-000000000001','$voucher',$($sequence+1),$action,$state,'50000000-0000-0000-0000-000000000004',@at,'Actual receipt',REPLICATE('D',64),CONVERT(nvarchar(100),@action),REPLICATE('E',64),0);
INSERT dbo.InventoryIssueVoucherReceiptLines(Id,TenantId,InventoryIssueVoucherActionId,InventoryIssueVoucherLineId,ReceivedQuantity,IsDeleted)
VALUES(NEWID(),'10000000-0000-0000-0000-000000000001',@action,'$line',$quantity,0);
UPDATE dbo.InventoryIssueVouchers SET ReceiptSequence=$sequence,Status=$state,AcknowledgedById=$ackActor,AcknowledgedAtUtc=$ackTime,
ReceiverComment='Actual receipt',UpdatedAt=@at,LastModifiedById='50000000-0000-0000-0000-000000000004',IntegrityHash=REPLICATE('$sequence',64)$mutation WHERE Id='$voucher';
"@
}
try {
    $project = [IO.File]::ReadAllText((Join-Path $Workspace 'src\ErpSystem.Api\ErpSystem.Api.csproj'))
    $secretId = [regex]::Match($project,'<UserSecretsId>([^<]+)</UserSecretsId>').Groups[1].Value
    $secretPath = Join-Path $env:APPDATA "Microsoft\UserSecrets\$secretId\secrets.json"
    $secret = [IO.File]::ReadAllText($secretPath) | ConvertFrom-Json
    $candidate = $secret.'ConnectionStrings:DefaultConnection'
    if (!$candidate -and $secret.ConnectionStrings) { $candidate = $secret.ConnectionStrings.DefaultConnection }
    if (!$candidate) { throw 'Default database connection is not available in user secrets.' }
    $stage = 'Parse SQL client connection settings'
    $builder = [System.Data.SqlClient.SqlConnectionStringBuilder]::new($candidate)
    $candidate=$null; $secret=$null
    if ($builder.DataSource -notin @($server,'.\SQL2017','localhost\SQL2017')) { throw 'Secret connection targets a different SQL instance; probe stopped.' }
    $stage = 'Configure isolated master connection'
    $builder['Asynchronous Processing']=$true; $builder['Data Source']=$server; $builder['Initial Catalog']='master'; $builder['Connect Timeout']=20; $builder['TrustServerCertificate']=$true
    $stage = 'Open verified SQL instance'
    $connection=[System.Data.SqlClient.SqlConnection]::new($builder.ConnectionString); $connection.Open()
    $verify=$connection.CreateCommand(); $verify.CommandText="SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))"; $actualServer=[string]$verify.ExecuteScalar(); $verify.Dispose()
    if ($actualServer -ine $server) { throw 'Connected SQL instance does not match the isolated-probe target.' }
    Run-Sql "IF DB_ID(N'$database') IS NOT NULL THROW 51000, 'Probe database already exists.', 1; CREATE DATABASE [$database];"
    $databaseCreated = $true
    $connection.ChangeDatabase($database)
    Run-Sql @'
CREATE TABLE dbo.InventoryRequisitions(Id uniqueidentifier PRIMARY KEY,TenantId uniqueidentifier,WarehouseId uniqueidentifier,DepartmentId uniqueidentifier,RequestedById uniqueidentifier,ApprovedById uniqueidentifier,Status int,IsDeleted bit);
CREATE TABLE dbo.WarehouseLocations(Id uniqueidentifier,TenantId uniqueidentifier,WarehouseId uniqueidentifier,IsDeleted bit);
CREATE TABLE dbo.Users(Id uniqueidentifier,IsActive bit);
CREATE TABLE dbo.UserTenants(UserId uniqueidentifier,TenantId uniqueidentifier,Status int,ExpiresAt datetime2 NULL,IsDeleted bit);
CREATE TABLE dbo.JournalEntries(Id uniqueidentifier,TenantId uniqueidentifier,IsDeleted bit,SourceDocumentType nvarchar(100),SourceDocumentId uniqueidentifier,PostingStatus nvarchar(20),IsBalanced bit,TotalDebitAmount decimal(18,2),TotalCreditAmount decimal(18,2),BalanceDifference decimal(18,2));
CREATE TABLE dbo.FinancePostingEvents(Id uniqueidentifier,TenantId uniqueidentifier,IsDeleted bit,SourceDocumentType nvarchar(100),SourceDocumentId uniqueidentifier,JournalEntryId uniqueidentifier,PostingStatus nvarchar(20));
CREATE TABLE dbo.InventoryIssueVouchers(Id uniqueidentifier PRIMARY KEY,TenantId uniqueidentifier NOT NULL,InventoryRequisitionId uniqueidentifier NOT NULL,VoucherNumber nvarchar(50) NOT NULL,WarehouseId uniqueidentifier NOT NULL,LocationId uniqueidentifier NULL,DepartmentId uniqueidentifier NULL,DepartmentName nvarchar(100) NULL,CostCenter nvarchar(100) NULL,ProjectId uniqueidentifier NULL,ProjectCode nvarchar(100) NULL,RequestedById uniqueidentifier NOT NULL,ApprovedById uniqueidentifier NOT NULL,IssuedById uniqueidentifier NOT NULL,ReceiverUserId uniqueidentifier NOT NULL,Status int NOT NULL,ReceiptSequence int NOT NULL DEFAULT 0 CHECK(ReceiptSequence>=0),AcknowledgedById uniqueidentifier NULL,AcknowledgedAtUtc datetime2 NULL,IssuedAtUtc datetime2 NOT NULL,Notes nvarchar(2000) NULL,ReceiverComment nvarchar(1000) NULL,MovementReasonCode nvarchar(50) NOT NULL,FinancePostingEventId uniqueidentifier NULL,FinanceJournalEntryId uniqueidentifier NULL,IdempotencyKey nvarchar(100) NOT NULL,PayloadHash nvarchar(64) NOT NULL,CorrelationId nvarchar(100) NOT NULL,SourceSnapshotJson nvarchar(max) NOT NULL,IntegrityHash nvarchar(64) NOT NULL,CreatedAt datetime2 NOT NULL,CreatedBy nvarchar(256) NULL,CreatedById uniqueidentifier NULL,UpdatedAt datetime2 NULL,LastModifiedById uniqueidentifier NULL,IsDeleted bit NOT NULL,DeletedAt datetime2 NULL,DeletedBy nvarchar(256) NULL);
CREATE TABLE dbo.InventoryIssueVoucherActions(Id uniqueidentifier PRIMARY KEY,TenantId uniqueidentifier NOT NULL,InventoryIssueVoucherId uniqueidentifier NOT NULL REFERENCES dbo.InventoryIssueVouchers(Id),Sequence int NOT NULL CHECK(Sequence>0),ActionType int NOT NULL CHECK(ActionType IN(1,2,3)),StatusAfter int NOT NULL CHECK(StatusAfter IN(1,2)),ActorUserId uniqueidentifier NOT NULL,OccurredAtUtc datetime2 NOT NULL,Comment nvarchar(1000) NOT NULL,IntegrityHash nvarchar(64) NOT NULL,ReceiptIdempotencyKey nvarchar(100) NULL,ReceiptPayloadHash nvarchar(64) NULL,IsDeleted bit NOT NULL,UNIQUE(TenantId,InventoryIssueVoucherId,Sequence));
CREATE UNIQUE INDEX UX_ReceiptReplay ON dbo.InventoryIssueVoucherActions(TenantId,InventoryIssueVoucherId,ReceiptIdempotencyKey) WHERE ReceiptIdempotencyKey IS NOT NULL;
CREATE TABLE dbo.InventoryIssueVoucherLines(Id uniqueidentifier PRIMARY KEY,TenantId uniqueidentifier NOT NULL,InventoryIssueVoucherId uniqueidentifier NOT NULL REFERENCES dbo.InventoryIssueVouchers(Id),Quantity decimal(18,4) NOT NULL CHECK(Quantity>0),SerialNumber nvarchar(100) NULL,IsDeleted bit NOT NULL);
CREATE TABLE dbo.InventoryIssueVoucherReceiptLines(Id uniqueidentifier PRIMARY KEY,TenantId uniqueidentifier NOT NULL,InventoryIssueVoucherActionId uniqueidentifier NOT NULL REFERENCES dbo.InventoryIssueVoucherActions(Id),InventoryIssueVoucherLineId uniqueidentifier NOT NULL REFERENCES dbo.InventoryIssueVoucherLines(Id),ReceivedQuantity decimal(18,4) NOT NULL CHECK(ReceivedQuantity>0),IsDeleted bit NOT NULL,UNIQUE(TenantId,InventoryIssueVoucherActionId,InventoryIssueVoucherLineId));
INSERT dbo.InventoryRequisitions VALUES('20000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000001','30000000-0000-0000-0000-000000000001','40000000-0000-0000-0000-000000000001','50000000-0000-0000-0000-000000000001','50000000-0000-0000-0000-000000000002',6,0);
INSERT dbo.Users VALUES('50000000-0000-0000-0000-000000000001',1),('50000000-0000-0000-0000-000000000002',1),('50000000-0000-0000-0000-000000000003',1),('50000000-0000-0000-0000-000000000004',1);
INSERT dbo.UserTenants SELECT Id,'10000000-0000-0000-0000-000000000001',0,NULL,0 FROM dbo.Users;
'@
    $archive=[IO.File]::ReadAllText((Join-Path $Workspace 'src\ErpSystem.Data\Migrations\ArchivedGovernanceBaselineSql.cs'))
    $baselineSources=[System.Collections.Generic.List[string]]::new()
    foreach($trigger in @('TR_InventoryIssueVouchers_ControlledLifecycle','TR_InventoryIssueVoucherActions_AppendOnly')) {
        $pattern='(?s)CREATE OR ALTER TRIGGER \[dbo\]\.\['+[regex]::Escape($trigger)+'\].*?(?=\r?\n\s*"""\);)'
        $matches=[regex]::Matches($archive,$pattern)
        if($matches.Count -ne 1){throw "Expected one exact archived trigger: $trigger"}
        $baselineSources.Add($matches[0].Value)
        Run-Sql $matches[0].Value
    }
    $helper=[IO.File]::ReadAllText((Join-Path $Workspace 'src\ErpSystem.Data\Migrations\InventoryIssueReceiptGuards.cs'))
    $helperSources=[System.Collections.Generic.List[string]]::new()
    foreach($constant in @('ParentLifecyclePatch','ActionGuard','ReceiptLineGuard')) {
        $match=[regex]::Match($helper,'(?s)public const string '+$constant+' = """\r?\n(.*?)\r?\n\s*""";')
        if(!$match.Success){throw "Helper SQL constant unavailable: $constant"}
        $helperSources.Add($match.Groups[1].Value)
        Run-Sql $match.Groups[1].Value
        $results.Add([pscustomobject]@{name="Compile/$constant";passed=$true;expectedSqlError=0;actualSqlError=0})
        Write-Output "PASS|Compile/$constant"
    }
    # Both sessions use production receipt SQL guards. The service's application lock is
    # deliberately absent: this verifies database defense even below that service boundary.
    foreach ($scenario in @(
        @{ Name='Competing cumulative overreceipt'; Quantity=60; Final=$false; Replay=$false; Error=51632; Total=60; Rows=1 },
        @{ Name='Competing replay key'; Quantity=60; Final=$false; Replay=$true; Error=2601; Total=60; Rows=1 },
        @{ Name='Concurrent remaining quantity'; Quantity=40; Final=$true; Replay=$false; Error=0; Total=100; Rows=2 }
    )) {
        $stage=$scenario.Name
        $voucher=[Guid]::NewGuid().ToString(); $line=[Guid]::NewGuid().ToString()
        Run-Sql (Seed-Voucher $voucher $line)
        $sessionA=$null; $sessionB=$null; $transactionA=$null; $pending=$null; $commandB=$null
        try {
            $builder['Initial Catalog']=$database
            $sessionA=[System.Data.SqlClient.SqlConnection]::new($builder.ConnectionString); $sessionA.Open()
            $sessionB=[System.Data.SqlClient.SqlConnection]::new($builder.ConnectionString); $sessionB.Open()
            $getSpid=$sessionA.CreateCommand(); $getSpid.CommandText='SELECT @@SPID'; $spidA=[int]$getSpid.ExecuteScalar(); $getSpid.Dispose()
            $getSpid=$sessionB.CreateCommand(); $getSpid.CommandText='SELECT @@SPID'; $spidB=[int]$getSpid.ExecuteScalar(); $getSpid.Dispose()
            if($spidA -eq $spidB){throw 'Concurrency requires distinct SQL Server sessions.'}
            $transactionA=$sessionA.BeginTransaction([System.Data.IsolationLevel]::Serializable)
            $first=(Receive-Sql $voucher $line 60 1 $false).Replace('CONVERT(nvarchar(100),@action)',"N'first-receipt'")
            $commandA=$sessionA.CreateCommand(); $commandA.Transaction=$transactionA; $commandA.CommandTimeout=30; $commandA.CommandText=$first
            try{[void]$commandA.ExecuteNonQuery()}finally{$commandA.Dispose()}
            $second=Receive-Sql $voucher $line $scenario.Quantity 2 $scenario.Final
            if($scenario.Replay){$second=$second.Replace('CONVERT(nvarchar(100),@action)',"N'first-receipt'")}
            $commandB=$sessionB.CreateCommand(); $commandB.CommandTimeout=30
            $commandB.CommandText="SET XACT_ABORT ON; SET TRANSACTION ISOLATION LEVEL SERIALIZABLE; BEGIN TRY BEGIN TRAN;`n$second`nCOMMIT; END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK; THROW; END CATCH;"
            $executed.Add("-- Session A begins transaction and holds until B blocking is observed.`n$first")
            $executed.Add("-- Session B ($($scenario.Name)) overlaps A.`n"+$commandB.CommandText)
            $pending=$commandB.BeginExecuteNonQuery()
            $blocked=$false; $waitType=$null
            $deadline=[DateTime]::UtcNow.AddSeconds(10)
            while([DateTime]::UtcNow -lt $deadline -and !$pending.IsCompleted) {
                $probe=$connection.CreateCommand()
                $probe.CommandText="SELECT wait_type FROM sys.dm_exec_requests WHERE session_id=$spidB AND blocking_session_id=$spidA AND wait_type LIKE N'LCK_M_%'"
                try{$waitType=$probe.ExecuteScalar()}finally{$probe.Dispose()}
                if($waitType -is [string] -and $waitType.Length){$blocked=$true;break}
                Start-Sleep -Milliseconds 100
            }
            if(!$blocked){throw 'Did not observe the second SQL session blocked by the first; concurrency was not proven.'}
            $transactionA.Commit(); $transactionA.Dispose(); $transactionA=$null
            $sqlError=0
            try{[void]$commandB.EndExecuteNonQuery($pending)}catch{
                $inner=$_.Exception
                while($inner -and $inner -isnot [System.Data.SqlClient.SqlException]){$inner=$inner.InnerException}
                if(!$inner){throw}
                $sqlError=$inner.Number
            }
            $pending=$null
            $pass=$sqlError -eq $scenario.Error
            $results.Add([pscustomobject]@{name=$scenario.Name;passed=$pass;sessionA=$spidA;sessionB=$spidB;blockingObserved=$blocked;waitType=$waitType;expectedSqlError=$scenario.Error;actualSqlError=$sqlError})
            Write-Output ("{0}|{1}|sessions={2},{3}|wait={4}|SQL={5}" -f $(if($pass){'PASS'}else{'FAIL'}),$scenario.Name,$spidA,$spidB,$waitType,$sqlError)
            $expectedStatus=if($scenario.Final){2}else{1}
            Check-Sql ($scenario.Name+' committed evidence') @"
IF (SELECT COALESCE(SUM(ReceivedQuantity),0) FROM dbo.InventoryIssueVoucherReceiptLines WHERE InventoryIssueVoucherLineId='$line')<>$($scenario.Total)
   OR (SELECT COUNT(*) FROM dbo.InventoryIssueVoucherReceiptLines WHERE InventoryIssueVoucherLineId='$line')<>$($scenario.Rows)
   OR (SELECT COUNT(*) FROM dbo.InventoryIssueVoucherActions WHERE InventoryIssueVoucherId='$voucher' AND ReceiptIdempotencyKey IS NOT NULL)<>$($scenario.Rows)
   OR NOT EXISTS(SELECT 1 FROM dbo.InventoryIssueVouchers WHERE Id='$voucher' AND Status=$expectedStatus AND ReceiptSequence=$($scenario.Rows) AND FinancePostingEventId IS NULL AND FinanceJournalEntryId IS NULL)
   OR EXISTS(SELECT 1 FROM dbo.InventoryIssueVoucherLines WHERE Id='$line' AND Quantity<>100)
    THROW 51001,'Concurrent receipt committed-state invariant failed.',1;
"@
        } finally {
            if($transactionA){try{$transactionA.Rollback()}catch{}; $transactionA.Dispose()}
            if($pending -and $commandB){$commandB.Cancel(); try{[void]$commandB.EndExecuteNonQuery($pending)}catch{}}
            if($commandB){$commandB.Dispose()}
            if($sessionA){$sessionA.Dispose()}; if($sessionB){$sessionB.Dispose()}
        }
    }
}
catch {
    $number=if($_.Exception -is [System.Data.SqlClient.SqlException]){$_.Exception.Number}else{$null}
    $safeMessage=if($connection -and $connection.State -eq 'Open' -and $connection.Database -eq $database){$_.Exception.Message}else{'Connection/setup failure details suppressed to protect user secrets.'}
    $results.Add([pscustomobject]@{name='Harness execution';passed=$false;sqlError=$number;errorType=$_.Exception.GetType().FullName;message=$safeMessage})
    Write-Output ('FAIL|Harness execution|'+$_.Exception.GetType().Name+'|SQL='+$number+'|Stage='+$stage)
}
finally {
    if($connection){$connection.Dispose()};$builder=$null
    [IO.File]::WriteAllText($sqlEvidence,($executed -join "`r`nGO`r`n"))
    [pscustomobject]@{database=$database;databaseCreated=$databaseCreated;stage=$stage;server=$server;utc=[DateTime]::UtcNow;scope='Two concurrent SQL Server sessions using isolated minimal tables, exact archived parent/action triggers and current receipt guards. Includes DMV-observed blocking and committed-state checks. Not full EF migration/schema or HTTP/service concurrency certification.';results=$results} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $evidence -Encoding utf8
    Write-Output "EVIDENCE|$evidence"
    Write-Output "SQL|$sqlEvidence"
    if($databaseCreated){Write-Output "DATABASE_RETAINED|$database"}
}
if(@($results|Where-Object {!$_.passed}).Count){exit 1}
