<# Isolated SQL fixture for exact count/recount guards. Does not change the application database. #>
param([string]$Workspace = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path)
$ErrorActionPreference = 'Stop'
$Workspace = (Resolve-Path -LiteralPath $Workspace).Path
$run = [DateTime]::UtcNow.ToString('yyyyMMdd_HHmmss') + '_' + [Guid]::NewGuid().ToString('N').Substring(0,8)
$database = 'RhemaERP_CountGuard_' + $run
$evidence = Join-Path $Workspace "tmp\count-guards-$run.json"
$results = [Collections.Generic.List[object]]::new()
$connection = [Data.SqlClient.SqlConnection]::new('Server=RHEMA-MICHAEL\SQL2017;Database=RhemaERP_ReceiptUpgrade_20260927_023608_918225d0;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=15')
$ids = @'
DECLARE @tenant uniqueidentifier='10000000-0000-0000-0000-000000000001',@actor uniqueidentifier='10000000-0000-0000-0000-000000000002',
@warehouse uniqueidentifier='20000000-0000-0000-0000-000000000001',@bin uniqueidentifier='20000000-0000-0000-0000-000000000002',
@root uniqueidentifier='30000000-0000-0000-0000-000000000001',@child uniqueidentifier='30000000-0000-0000-0000-000000000002',
@original uniqueidentifier='40000000-0000-0000-0000-000000000001',@line uniqueidentifier='40000000-0000-0000-0000-000000000002',
@item uniqueidentifier='50000000-0000-0000-0000-000000000001',@passedItem uniqueidentifier='50000000-0000-0000-0000-000000000002';
'@
function Run-Sql([string]$sql) {
    $command=$connection.CreateCommand(); $command.CommandTimeout=30; $command.CommandText=$sql
    try { [void]$command.ExecuteNonQuery() } finally { $command.Dispose() }
}
function Guard([string]$file,[string]$name) {
    $source=[IO.File]::ReadAllText((Join-Path $Workspace $file))
    $match=[regex]::Match($source,'public const string '+[regex]::Escape($name)+' = """\r?\n([\s\S]*?)\r?\n\s*""";')
    if (!$match.Success) { throw "Guard $name not found." }
    return [regex]::Replace($match.Groups[1].Value,'(?m)^        ','')
}
function Check([string]$name,[string]$sql,[int]$expected=0) {
    $command=$connection.CreateCommand(); $command.CommandTimeout=30
    $command.CommandText="SET XACT_ABORT ON; BEGIN TRY BEGIN TRAN;`n$ids`n$sql`nROLLBACK; SELECT 0; END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK; SELECT ERROR_NUMBER(); END CATCH;"
    try { $actual=[int]$command.ExecuteScalar() } finally { $command.Dispose() }
    $pass=$actual -eq $expected
    $results.Add([pscustomobject]@{name=$name;passed=$pass;expectedSqlError=$expected;actualSqlError=$actual})
    Write-Output ("{0}|{1}|SQL={2}" -f $(if($pass){'PASS'}else{'FAIL'}),$name,$actual)
}
try {
    if($database -notmatch '^RhemaERP_CountGuard_[0-9]{8}_[0-9]{6}_[a-f0-9]{8}$') { throw 'Invalid isolated fixture name.' }
    $connection.Open()
    $freezes=@()
    foreach($name in @('TR_WarehouseQuantities_PhysicalCountFreeze','TR_InventoryItems_PhysicalCountFreeze','TR_StockMovements_PhysicalCountFreeze')) {
        $command=$connection.CreateCommand(); $command.CommandText='SELECT OBJECT_DEFINITION(OBJECT_ID(@name))'
        [void]$command.Parameters.AddWithValue('@name','dbo.'+$name)
        try { $definition=[string]$command.ExecuteScalar() } finally { $command.Dispose() }
        if(!$definition) { throw "Current freeze guard $name is missing." }
        $freezes += $definition
    }
    $connection.ChangeDatabase('master')
    Run-Sql "IF DB_ID(N'$database') IS NOT NULL THROW 51000,'Fixture already exists.',1; CREATE DATABASE [$database];"
    $connection.ChangeDatabase($database)
    Run-Sql @'
CREATE TABLE dbo.PhysicalCounts(Id uniqueidentifier PRIMARY KEY,TenantId uniqueidentifier,RootPhysicalCountId uniqueidentifier NULL,ParentPhysicalCountId uniqueidentifier NULL,RecountAttempt int DEFAULT 0,RecountRequestKey nvarchar(100) NULL,RecountRequestHash nvarchar(64) NULL,WarehouseId uniqueidentifier,IsDeleted bit DEFAULT 0,Status nvarchar(50),FreezeInventory bit DEFAULT 1,FreezeStartedAtUtc datetime2 DEFAULT SYSUTCDATETIME(),FreezeReleasedAtUtc datetime2 NULL,ObservationSubmittedAtUtc datetime2 NULL,InitiatedById uniqueidentifier NULL,CountedById uniqueidentifier NULL,StoresApprovedById uniqueidentifier NULL,FinanceApprovedById uniqueidentifier NULL,AuditAttestedById uniqueidentifier NULL,StockAdjustmentId uniqueidentifier NULL,ApprovalRequired bit DEFAULT 0);
CREATE TABLE dbo.PhysicalCountItems(Id uniqueidentifier PRIMARY KEY,TenantId uniqueidentifier,PhysicalCountId uniqueidentifier,RootPhysicalCountItemId uniqueidentifier NULL,PredecessorPhysicalCountItemId uniqueidentifier NULL,SupersededByPhysicalCountId uniqueidentifier NULL,RecountReason nvarchar(2000) NULL,InventoryItemId uniqueidentifier,LocationId uniqueidentifier NULL,SystemQuantity decimal(18,4),CountedQuantity decimal(18,4),DefectiveQuantity decimal(18,4) DEFAULT 0,DefectiveNotes nvarchar(2000) NULL,VarianceQuantity decimal(18,4) DEFAULT 0,UnitCost decimal(18,4),LotNumber nvarchar(100) NULL,SerialNumber nvarchar(100) NULL,Notes nvarchar(1000) NULL,CountedById uniqueidentifier NULL,IsCounted bit DEFAULT 0,RequiresRecount bit DEFAULT 0,IsDeleted bit DEFAULT 0,CONSTRAINT CK_Defects CHECK(DefectiveQuantity>=0 AND DefectiveQuantity<=CountedQuantity));
ALTER TABLE dbo.PhysicalCountItems ADD RecountedById uniqueidentifier NULL;
CREATE TABLE dbo.PhysicalCountActions(Id uniqueidentifier PRIMARY KEY,TenantId uniqueidentifier,PhysicalCountId uniqueidentifier,ActionType int,ActorRole nvarchar(100),ActorUserId uniqueidentifier,IsDeleted bit DEFAULT 0,Comment nvarchar(2000),SnapshotJson nvarchar(max),OccurredAtUtc datetime2);
CREATE TABLE dbo.PhysicalCountCounters(Id uniqueidentifier PRIMARY KEY,PhysicalCountId uniqueidentifier,TenantId uniqueidentifier,EmployeeId uniqueidentifier,UserId uniqueidentifier,EmployeeNumber nvarchar(50),EmployeeName nvarchar(250),EmailAddress nvarchar(256),AssignedById uniqueidentifier,AssignedAtUtc datetime2,RemovedById uniqueidentifier NULL,RemovedAtUtc datetime2 NULL,ChangeReason nvarchar(1000) NULL,InAppNotificationId uniqueidentifier,EmailNotificationId uniqueidentifier,IsActive bit,IsDeleted bit);
CREATE TABLE dbo.Employees(Id uniqueidentifier PRIMARY KEY,TenantId uniqueidentifier,IsActive bit,IsDeleted bit);
CREATE TABLE dbo.Users(Id uniqueidentifier PRIMARY KEY,EmployeeId uniqueidentifier,IsActive bit);
CREATE TABLE dbo.UserTenants(TenantId uniqueidentifier,UserId uniqueidentifier,Status int,ExpiresAt datetime2 NULL,IsDeleted bit);
CREATE TABLE dbo.Notifications(Id uniqueidentifier PRIMARY KEY,TenantId uniqueidentifier,RecipientId uniqueidentifier,EntityId uniqueidentifier,DeliveryMethods nvarchar(100),NotificationType nvarchar(100),AdditionalData nvarchar(max),EmailAddress nvarchar(256));
CREATE TABLE dbo.PhysicalCountAdjustmentClaims(Id uniqueidentifier PRIMARY KEY,TenantId uniqueidentifier,RootPhysicalCountItemId uniqueidentifier,PhysicalCountId uniqueidentifier,PhysicalCountItemId uniqueidentifier,StockAdjustmentId uniqueidentifier NULL,StockAdjustmentItemId uniqueidentifier NULL,ClaimedById uniqueidentifier,ClaimedAtUtc datetime2 DEFAULT SYSUTCDATETIME(),SystemQuantity decimal(18,4),CountedQuantity decimal(18,4),VarianceQuantity decimal(18,4),IsDeleted bit DEFAULT 0);
CREATE UNIQUE INDEX IX_CountClaimRoot ON dbo.PhysicalCountAdjustmentClaims(TenantId,RootPhysicalCountItemId);
CREATE TABLE dbo.StockAdjustments(Id uniqueidentifier PRIMARY KEY,TenantId uniqueidentifier,Status nvarchar(50),AdjustmentNumber nvarchar(50),ApprovalRequired bit,PostedById uniqueidentifier);
CREATE TABLE dbo.StockAdjustmentItems(Id uniqueidentifier PRIMARY KEY,TenantId uniqueidentifier,AdjustmentId uniqueidentifier,InventoryItemId uniqueidentifier,LocationId uniqueidentifier NULL,AdjustmentQuantity decimal(18,4),UnitCost decimal(18,4),IsDeleted bit DEFAULT 0);
CREATE TABLE dbo.InventoryItems(Id uniqueidentifier PRIMARY KEY,TenantId uniqueidentifier,CurrentStock decimal(18,4),AvailableStock decimal(18,4),AllocatedStock decimal(18,4));
CREATE TABLE dbo.WarehouseQuantities(Id uniqueidentifier PRIMARY KEY,TenantId uniqueidentifier,WarehouseId uniqueidentifier,InventoryItemId uniqueidentifier,CurrentStock decimal(18,4),AvailableStock decimal(18,4),AllocatedStock decimal(18,4));
CREATE TABLE dbo.StockMovements(Id uniqueidentifier PRIMARY KEY,TenantId uniqueidentifier,WarehouseId uniqueidentifier,InventoryItemId uniqueidentifier,LocationId uniqueidentifier NULL,Quantity decimal(18,4),ReferenceType int,ReferenceId uniqueidentifier,ReferenceNumber nvarchar(50),ProcessedById uniqueidentifier,IsDeleted bit DEFAULT 0);
'@
    Run-Sql ($ids+@'
INSERT dbo.PhysicalCounts(Id,TenantId,WarehouseId,Status,FreezeReleasedAtUtc,CountedById) VALUES(@root,@tenant,@warehouse,N'Posted',SYSUTCDATETIME(),@actor);
INSERT dbo.PhysicalCounts(Id,TenantId,RootPhysicalCountId,ParentPhysicalCountId,RecountAttempt,RecountRequestKey,RecountRequestHash,WarehouseId,Status,CountedById) VALUES(@child,@tenant,@root,@root,1,N'recount',N'fixture',@warehouse,N'Draft',@actor);
INSERT dbo.PhysicalCountItems(Id,TenantId,PhysicalCountId,InventoryItemId,LocationId,SystemQuantity,CountedQuantity,UnitCost,IsCounted,SupersededByPhysicalCountId,RequiresRecount,CountedById) VALUES(@original,@tenant,@root,@item,@bin,100,90,1,1,@child,1,@actor);
INSERT dbo.PhysicalCountItems(Id,TenantId,PhysicalCountId,RootPhysicalCountItemId,PredecessorPhysicalCountItemId,InventoryItemId,LocationId,SystemQuantity,CountedQuantity,UnitCost,RecountReason,CountedById) VALUES(@line,@tenant,@child,@original,@original,@item,@bin,100,0,1,N'Check quantity',@actor);
'@)
    foreach($definition in $freezes) { Run-Sql $definition }
    $recount='src\ErpSystem.Data\Migrations\PhysicalCountRecountGuards.cs'
    foreach($name in @('FreezePatch','LineGuard','ClaimGuard','CountGuard','AdjustmentGuard')) { Run-Sql (Guard $recount $name) }
    Run-Sql (Guard 'src\ErpSystem.Data\Migrations\PhysicalCountCommitteeGuards.cs' 'DefectiveObservationGuard')
    Run-Sql (Guard 'src\ErpSystem.Data\Migrations\PhysicalCountCommitteeGuards.cs' 'CounterGuard')
    Run-Sql (Guard 'src\ErpSystem.Data\Migrations\PhysicalCountCommitteeGuards.cs' 'CountGuard')
    $movement="INSERT dbo.StockMovements(Id,TenantId,WarehouseId,InventoryItemId,LocationId,Quantity,ReferenceType,ReferenceId,ReferenceNumber,ProcessedById) VALUES(NEWID(),@tenant,@warehouse,@item,@bin,1,1,NEWID(),N'TEST',@actor);"
    Check 'Parent posted does not release draft child stock scope' $movement 51943
    Check 'Unrelated passed item is not frozen by selective child' ($movement.Replace('@item,','@passedItem,'))
    Check 'Defective observation remains part of physical quantity' "UPDATE dbo.PhysicalCounts SET Status=N'InProgress' WHERE Id=@child; UPDATE dbo.PhysicalCountItems SET CountedQuantity=100,IsCounted=1,DefectiveQuantity=5 WHERE Id=@line; IF NOT EXISTS(SELECT 1 FROM dbo.PhysicalCountItems WHERE Id=@line AND CountedQuantity-SystemQuantity=0 AND DefectiveQuantity=5) THROW 51000,'Variance changed.',1;"
    Check 'Defects cannot exceed physical quantity' "UPDATE dbo.PhysicalCounts SET Status=N'InProgress' WHERE Id=@child; UPDATE dbo.PhysicalCountItems SET CountedQuantity=100,DefectiveQuantity=101 WHERE Id=@line;" 547
    Check 'Submitted physical observation cannot be overwritten' "UPDATE dbo.PhysicalCounts SET ObservationSubmittedAtUtc=SYSUTCDATETIME() WHERE Id=@child; UPDATE dbo.PhysicalCountItems SET CountedQuantity=10 WHERE Id=@line;" 51989
    Check 'Original selected observation remains immutable' 'UPDATE dbo.PhysicalCountItems SET CountedQuantity=95 WHERE Id=@original;' 51989
    Check 'Child baseline cannot diverge from original' 'UPDATE dbo.PhysicalCountItems SET SystemQuantity=101 WHERE Id=@line;' 51989
    Check 'Selected recount lines cannot be deleted' 'DELETE dbo.PhysicalCountItems WHERE Id=@line;' 51989
    Check 'Child cannot cancel away its stock obligation' "UPDATE dbo.PhysicalCounts SET Status=N'Cancelled' WHERE Id=@child;" 51991
    Check 'Original counter cannot approve a descendant' 'UPDATE dbo.PhysicalCounts SET StoresApprovedById=@actor WHERE Id=@child;' 51991
    $prepare="UPDATE dbo.PhysicalCountItems SET CountedQuantity=100,IsCounted=1,VarianceQuantity=0 WHERE Id=@line; UPDATE dbo.PhysicalCounts SET Status=N'ReadyToPost' WHERE Id=@child;"
    $claim="INSERT dbo.PhysicalCountAdjustmentClaims(Id,TenantId,RootPhysicalCountItemId,PhysicalCountId,PhysicalCountItemId,ClaimedById,SystemQuantity,CountedQuantity,VarianceQuantity) VALUES(NEWID(),@tenant,@original,@child,@line,@actor,100,100,0);"
    Check 'Zero variance resolves through retained claim without adjustment' ($prepare+$claim+"UPDATE dbo.PhysicalCounts SET Status=N'Posted',FreezeReleasedAtUtc=SYSUTCDATETIME() WHERE Id=@child;")
    Check 'Duplicate original-line resolution is rejected' ($prepare+$claim+$claim) 2601
    Check 'Resolution claim cannot be deleted' ($prepare+$claim+'DELETE dbo.PhysicalCountAdjustmentClaims WHERE RootPhysicalCountItemId=@original;') 51990
    Check 'Resolved posting cannot omit its root claim' ($prepare+"UPDATE dbo.PhysicalCounts SET Status=N'Posted' WHERE Id=@child;") 51991
    Check 'Wrong-tenant claim is rejected' ($prepare+$claim.Replace('NEWID(),@tenant,@original','NEWID(),NEWID(),@original')) 51990
    $committee=@'
DECLARE @committee uniqueidentifier='30000000-0000-0000-0000-000000000003',@employee uniqueidentifier='60000000-0000-0000-0000-000000000001',@member uniqueidentifier='60000000-0000-0000-0000-000000000002',@assignment uniqueidentifier='60000000-0000-0000-0000-000000000003',@inApp uniqueidentifier='60000000-0000-0000-0000-000000000004',@email uniqueidentifier='60000000-0000-0000-0000-000000000005';
INSERT dbo.PhysicalCounts(Id,TenantId,WarehouseId,Status,CountedById) VALUES(@committee,@tenant,@warehouse,N'Draft',@actor);
INSERT dbo.Employees VALUES(@employee,@tenant,1,0);
INSERT dbo.Users VALUES(@member,@employee,1);
INSERT dbo.UserTenants VALUES(@tenant,@member,0,NULL,0);
INSERT dbo.Notifications VALUES(@inApp,@tenant,@member,@committee,N'InApp',N'PhysicalCountCounterAssigned',N'{"counterAssignmentId":"60000000-0000-0000-0000-000000000003"}',NULL);
INSERT dbo.Notifications VALUES(@email,@tenant,'00000000-0000-0000-0000-000000000000',@committee,N'Email',N'PhysicalCountCounterAssigned',N'{"counterAssignmentId":"60000000-0000-0000-0000-000000000003"}',N'counter@example.invalid');
'@
    $assign="INSERT dbo.PhysicalCountCounters(Id,PhysicalCountId,TenantId,EmployeeId,UserId,EmployeeNumber,EmployeeName,EmailAddress,AssignedById,AssignedAtUtc,InAppNotificationId,EmailNotificationId,IsActive,IsDeleted) VALUES(@assignment,@committee,@tenant,@employee,@member,N'EMP-TEST',N'Counter',N'counter@example.invalid',@actor,SYSUTCDATETIME(),@inApp,@email,1,0);"
    Check 'Typed committee assignment requires both retained notification records' ($committee+$assign)
    Check 'Email-only notification cannot duplicate the member inbox' ($committee+'UPDATE dbo.Notifications SET RecipientId=@member WHERE Id=@email;'+$assign) 51982
    Check 'Employee linked-user identity cannot be guessed or changed' ($committee+'UPDATE dbo.Users SET EmployeeId=NEWID() WHERE Id=@member;'+$assign) 51982
    Check 'Active committee member can start counting' ($committee+$assign+"UPDATE dbo.PhysicalCounts SET Status=N'InProgress',CountedById=@member WHERE Id=@committee;")
    Check 'Legacy starter cannot bypass explicit committee' ($committee+$assign+"UPDATE dbo.PhysicalCounts SET Status=N'InProgress' WHERE Id=@committee;") 51984
    Check 'Assignment history cannot be deleted' ($committee+$assign+'DELETE dbo.PhysicalCountCounters WHERE Id=@assignment;') 51981
    Check 'Assignment identity snapshots cannot be rewritten' ($committee+$assign+"UPDATE dbo.PhysicalCountCounters SET EmployeeName=N'Changed' WHERE Id=@assignment;") 51983
    Check 'Removal remains possible after HR relinks the employee' ($committee+$assign+'UPDATE dbo.Users SET EmployeeId=NEWID() WHERE Id=@member;UPDATE dbo.PhysicalCountCounters SET IsActive=0,RemovedById=@actor,RemovedAtUtc=SYSUTCDATETIME() WHERE Id=@assignment;')
    $recovery=@'
UPDATE dbo.PhysicalCounts SET Status=N'UnderInvestigation',ObservationSubmittedAtUtc=SYSUTCDATETIME(),InitiatedById=NEWID(),CountedById=@member WHERE Id=@committee;
INSERT dbo.PhysicalCountActions(Id,TenantId,PhysicalCountId,ActionType,ActorRole,ActorUserId,Comment,SnapshotJson,OccurredAtUtc)
VALUES(NEWID(),@tenant,@committee,17,N'LegacyCommitteeRecovery',@actor,N'Independent investigation',N'{"payload":{"employeeIds":["60000000-0000-0000-0000-000000000001"]}}',SYSUTCDATETIME());
'@
    Check 'Audited initial legacy investigation committee can be assigned' ($committee+$recovery+$assign)
    Check 'Legacy recovery requires retained investigator action' ($committee+$recovery+'DELETE dbo.PhysicalCountActions WHERE PhysicalCountId=@committee;'+$assign) 51982
    Check 'Legacy recovery action must name the actual independent actor' ($committee+$recovery+'UPDATE dbo.PhysicalCountActions SET ActorUserId=NEWID() WHERE PhysicalCountId=@committee;'+$assign) 51982
    Check 'Legacy recovery action must authorize the selected employee' ($committee+$recovery+'UPDATE dbo.PhysicalCountActions SET SnapshotJson=N''{"payload":{"employeeIds":[]}}'' WHERE PhysicalCountId=@committee;'+$assign) 51982
    Check 'Original counter cannot assign recovery committee' ($committee+$recovery+'UPDATE dbo.PhysicalCounts SET CountedById=@actor WHERE Id=@committee;'+$assign) 51982
    Check 'Recovery investigator cannot join the committee' ($committee+$recovery+$assign.Replace("@actor,SYSUTCDATETIME()","@member,SYSUTCDATETIME()")) 51982
    Check 'Recovery cannot bypass an active adjustment' ($committee+$recovery+'UPDATE dbo.PhysicalCounts SET StockAdjustmentId=NEWID() WHERE Id=@committee;'+$assign) 51982
    Check 'Recovery cannot rewrite an assigned committee' ($committee+$recovery+$assign+"UPDATE dbo.PhysicalCountCounters SET EmployeeName=N'Changed' WHERE Id=@assignment;") 51982
    Check 'Child recount is not a legacy recovery root' ($committee+"UPDATE dbo.PhysicalCounts SET Status=N'UnderInvestigation',ObservationSubmittedAtUtc=SYSUTCDATETIME() WHERE Id=@child; SET @committee=@child; UPDATE dbo.Notifications SET EntityId=@child;"+$recovery+$assign) 51982
    $observation="INSERT dbo.PhysicalCountItems(Id,TenantId,PhysicalCountId,InventoryItemId,LocationId,SystemQuantity,CountedQuantity,UnitCost,IsCounted,CountedById) VALUES(NEWID(),@tenant,@committee,@item,@bin,100,90,1,1,@member);"
    Check 'Recovery preserves submitted original observation' ($committee+$observation+$recovery+$assign+'UPDATE dbo.PhysicalCountItems SET CountedQuantity=95 WHERE PhysicalCountId=@committee;') 51989
    # Require real lock contention between two SQL sessions, not just a sequential duplicate.
    Run-Sql ($ids+$prepare)
    $fixtureConnection=[Data.SqlClient.SqlConnectionStringBuilder]::new($connection.ConnectionString)
    $fixtureConnection['Initial Catalog']=$database
    if($fixtureConnection['Initial Catalog'] -notmatch '^RhemaERP_CountGuard_[0-9]{8}_[0-9]{6}_[a-f0-9]{8}$') { throw 'Concurrent sessions must target the isolated fixture.' }
    $a=[Data.SqlClient.SqlConnection]::new($fixtureConnection.ConnectionString)
    $b=[Data.SqlClient.SqlConnection]::new($fixtureConnection.ConnectionString)
    $ta=$null;$tb=$null;$ca=$null;$cb=$null
    try {
        $a.Open();$b.Open()
        $ca=$a.CreateCommand();$ca.CommandText='SELECT @@SPID';$sa=[int]$ca.ExecuteScalar()
        $cb=$b.CreateCommand();$cb.CommandText='SELECT @@SPID';$sb=[int]$cb.ExecuteScalar()
        $ta=$a.BeginTransaction([Data.IsolationLevel]::Serializable);$tb=$b.BeginTransaction([Data.IsolationLevel]::Serializable)
        $ca.Transaction=$ta;$cb.Transaction=$tb
        $ca.CommandText=$ids+$claim;$cb.CommandText=$ids+$claim;$cb.CommandTimeout=20
        [void]$ca.ExecuteNonQuery();$pending=$cb.ExecuteNonQueryAsync()
        $observed=$false;$watch=[Diagnostics.Stopwatch]::StartNew()
        while($watch.Elapsed.TotalSeconds -lt 8 -and !$pending.IsCompleted) {
            $probe=$connection.CreateCommand();$probe.CommandTimeout=5
            $probe.CommandText='SELECT COUNT(*) FROM sys.dm_exec_requests WHERE session_id=@waiting AND blocking_session_id=@holding AND wait_type LIKE N''LCK%'''
            [void]$probe.Parameters.AddWithValue('@waiting',$sb);[void]$probe.Parameters.AddWithValue('@holding',$sa)
            try{$observed=[int]$probe.ExecuteScalar() -gt 0}finally{$probe.Dispose()}
            if($observed){break};Start-Sleep -Milliseconds 50
        }
        $ta.Commit();$ta=$null
        $errorCode=0
        try{[void]$pending.GetAwaiter().GetResult()}catch{if($_.Exception.InnerException -is [Data.SqlClient.SqlException]){$errorCode=$_.Exception.InnerException.Number}elseif($_.Exception -is [Data.SqlClient.SqlException]){$errorCode=$_.Exception.Number}else{throw}}
        if($tb){$tb.Rollback();$tb=$null}
        $pass=$observed -and $errorCode -eq 2601
        $results.Add([pscustomobject]@{name='Concurrent root-line claims serialize and reject the second poster';passed=$pass;blockedSessionObserved=$observed;holdingSession=$sa;waitingSession=$sb;actualSqlError=$errorCode})
        Write-Output ("{0}|Concurrent root-line claims|Blocked={1}|SQL={2}" -f $(if($pass){'PASS'}else{'FAIL'}),$observed,$errorCode)
        Check 'Concurrent claims leave exactly one retained resolution' "IF (SELECT COUNT(*) FROM dbo.PhysicalCountAdjustmentClaims WHERE TenantId=@tenant AND RootPhysicalCountItemId=@original)<>1 THROW 51000,'Claim count differs.',1;"
    } finally {
        if($ta){$ta.Rollback();$ta.Dispose()};if($tb){$tb.Rollback();$tb.Dispose()}
        if($ca){$ca.Dispose()};if($cb){$cb.Dispose()};$a.Dispose();$b.Dispose()
    }
    $failed=@($results | Where-Object {!$_.passed}).Count
    [pscustomobject]@{database=$database;passed=($failed -eq 0);checks=$results;scope='Exact new guards plus existing live freeze definitions; not a full EF migration or HTTP workflow test.'}|ConvertTo-Json -Depth 6|Set-Content -LiteralPath $evidence -Encoding UTF8
    Write-Output "EVIDENCE|$evidence"
    if($failed) { throw "$failed count guard checks failed." }
} finally { $connection.Dispose() }
