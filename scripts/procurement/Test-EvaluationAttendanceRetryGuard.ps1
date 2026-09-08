param([Parameter(Mandatory)][System.Data.Common.DbConnection]$SourceConnection)
$ErrorActionPreference = 'Stop'
$taskOriginalDatabase = $SourceConnection.Database
$taskDatabase = 'RhemaERP_AttendanceRetryTest_' + [guid]::NewGuid().ToString('N')
if ($taskDatabase -notmatch '^RhemaERP_AttendanceRetryTest_[a-f0-9]{32}$') { throw 'Invalid owned test database name.' }
function Invoke-TestSql([string]$sql) {
    $taskCommand = $SourceConnection.CreateCommand()
    try { $taskCommand.CommandText = $sql; $taskCommand.CommandTimeout = 60; [void]$taskCommand.ExecuteNonQuery() }
    finally { $taskCommand.Dispose() }
}
function Assert-Scalar([string]$sql, [int]$expected) {
    $taskCommand = $SourceConnection.CreateCommand()
    try {
        $taskCommand.CommandText = $sql
        if ([int]$taskCommand.ExecuteScalar() -ne $expected) { throw 'Attendance retry state assertion failed.' }
    } finally { $taskCommand.Dispose() }
}
function Assert-Blocked([string]$name, [string]$sql, [int]$number) {
    Invoke-TestSql 'BEGIN TRANSACTION'
    $taskCaught = $false
    try { Invoke-TestSql $sql }
    catch {
        $taskError = $_.Exception
        while ($taskError.InnerException) { $taskError = $taskError.InnerException }
        if ($taskError.Number -ne $number) { throw }
        $taskCaught = $true
    } finally { Invoke-TestSql 'IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION' }
    if (-not $taskCaught) { throw "Expected SQL guard was bypassed: $name" }
    "PASS: $name ($number)"
}
$taskCreated = $false
try {
    $SourceConnection.ChangeDatabase('master')
    Invoke-TestSql "CREATE DATABASE [$taskDatabase]"
    $taskCreated = $true
    $SourceConnection.ChangeDatabase($taskDatabase)
    # Isolated minimum schema for the actual production meeting/attendance triggers.
    # No live UAT rows or triggers are changed.
    Invoke-TestSql @'
CREATE TABLE dbo.ProcurementEvaluationCommitteeControls
(Id uniqueidentifier PRIMARY KEY,TenantId uniqueidentifier,IsDeleted bit,Status int,
 EffectiveFromUtc datetime2,EffectiveToUtc datetime2 NULL,RequiredQuorum int);
CREATE TABLE dbo.ProcurementEvaluationCommitteeAppointments
(Id uniqueidentifier PRIMARY KEY,CommitteeControlId uniqueidentifier,TenantId uniqueidentifier,
 IsDeleted bit,Status int,IsVoting bit,MemberKind int,RoleName nvarchar(100),
 EffectiveFromUtc datetime2,EffectiveToUtc datetime2 NULL);
CREATE TABLE dbo.ProcurementEvaluationConflictDeclarations
(Id uniqueidentifier PRIMARY KEY,AppointmentId uniqueidentifier,TenantId uniqueidentifier,
 IsDeleted bit,Outcome int,Version int,ValidFromUtc datetime2,ValidToUtc datetime2 NULL);
CREATE TABLE dbo.ProcurementEvaluationCommitteeRoleRequirements
(CommitteeControlId uniqueidentifier,TenantId uniqueidentifier,IsDeleted bit,IsRequiredForQuorum bit,
 MemberKind int,RoleName nvarchar(100),IsVoting bit,MinimumCount int);
CREATE TABLE dbo.ProcurementEvaluationMeetings
(Id uniqueidentifier PRIMARY KEY,CommitteeControlId uniqueidentifier,TenantId uniqueidentifier,
 IsDeleted bit,Sequence int,Phase int,Status int,MeetingMode nvarchar(20),MeetingChannel nvarchar(100),
 ScheduledAtUtc datetime2,CreatedAt datetime2,StartedAtUtc datetime2 NULL,ClosedAtUtc datetime2 NULL,
 IdempotencyKey nvarchar(100),EligibleVotingMemberCount int,SignedVotingAttendanceCount int,
 ChairPresent bit,SecretaryPresent bit,QuorumMet bit,EvidenceReference nvarchar(200),
 RemoteMeetingEvidenceReference nvarchar(200) NULL,QuorumIdempotencyKey nvarchar(100) NULL,
 QuorumSnapshotJson nvarchar(max),QuorumIntegrityHash nvarchar(64),UpdatedAt datetime2 NULL,
 CONSTRAINT CK_RetryMeeting_State CHECK
 (Sequence >= 1 AND Phase BETWEEN 0 AND 2 AND Status BETWEEN 0 AND 3
 AND EligibleVotingMemberCount >= 0 AND SignedVotingAttendanceCount >= 0
 AND LEN(QuorumIntegrityHash)=64 AND ISJSON(QuorumSnapshotJson)=1
 AND ((Status=0 AND QuorumMet=0) OR (Status=1 AND QuorumMet=1 AND StartedAtUtc IS NOT NULL AND ChairPresent=1 AND SecretaryPresent=1)
 OR (Status=2 AND QuorumMet=0 AND StartedAtUtc IS NOT NULL) OR (Status=3 AND ClosedAtUtc IS NOT NULL))));
CREATE TABLE dbo.ProcurementEvaluationAttendanceRecords
(Id uniqueidentifier PRIMARY KEY,MeetingId uniqueidentifier,AppointmentId uniqueidentifier,TenantId uniqueidentifier,
 IsDeleted bit,IsPresent bit,WasEligibleAtSignature bit,SignedAtUtc datetime2,
 CONSTRAINT UQ_RetryAttendance UNIQUE(MeetingId,AppointmentId));
DECLARE @tenant uniqueidentifier=NEWID(),@control uniqueidentifier=NEWID();
INSERT dbo.ProcurementEvaluationCommitteeControls VALUES(@control,@tenant,0,1,DATEADD(day,-1,SYSUTCDATETIME()),NULL,2);
INSERT dbo.ProcurementEvaluationCommitteeAppointments
SELECT NEWID(),@control,@tenant,0,1,1,kind,'TDC_EVALUATOR',DATEADD(day,-1,SYSUTCDATETIME()),NULL FROM (VALUES(0),(4),(1)) roles(kind);
INSERT dbo.ProcurementEvaluationConflictDeclarations
SELECT NEWID(),Id,@tenant,0,0,1,DATEADD(day,-1,SYSUTCDATETIME()),NULL FROM dbo.ProcurementEvaluationCommitteeAppointments;
INSERT dbo.ProcurementEvaluationCommitteeRoleRequirements
SELECT @control,@tenant,0,1,kind,'TDC_EVALUATOR',1,1 FROM (VALUES(0),(4)) roles(kind);
'@
    $taskMigration = Get-Content -LiteralPath (Join-Path $PSScriptRoot '../../src/ErpSystem.Data/Migrations/20260723214709_AddProcurementEvaluationCommitteeControls.cs') -Raw
    foreach ($taskTriggerName in @('TR_ProcurementEvaluationMeetings_Lifecycle','TR_ProcurementEvaluationAttendanceRecords_Immutable')) {
        $taskPattern = '(?s)CREATE TRIGGER \[dbo\]\.\[' + [regex]::Escape($taskTriggerName) + '\].*?(?=\s*""")'
        $taskTrigger = [regex]::Match($taskMigration,$taskPattern).Value
        if (-not $taskTrigger) { throw "Production trigger not found: $taskTriggerName" }
        Invoke-TestSql $taskTrigger
    }
    Invoke-TestSql @'
INSERT dbo.ProcurementEvaluationMeetings
SELECT NEWID(),Id,TenantId,0,1,0,0,'InPerson','SQL regression room',SYSUTCDATETIME(),SYSUTCDATETIME(),NULL,NULL,
 'retry-meeting',0,0,0,0,0,'agenda',NULL,NULL,'{}',REPLICATE('A',64),NULL
FROM dbo.ProcurementEvaluationCommitteeControls;
UPDATE dbo.ProcurementEvaluationMeetings SET Status=2,EligibleVotingMemberCount=3,
 StartedAtUtc=SYSUTCDATETIME(),QuorumIdempotencyKey='failed-attempt',QuorumSnapshotJson='{"attempt":"failed","attendees":0}';
'@
    $taskInsertChair = @'
INSERT dbo.ProcurementEvaluationAttendanceRecords
SELECT NEWID(),m.Id,a.Id,m.TenantId,0,1,1,SYSUTCDATETIME()
FROM dbo.ProcurementEvaluationMeetings m JOIN dbo.ProcurementEvaluationCommitteeAppointments a ON a.CommitteeControlId=m.CommitteeControlId WHERE a.MemberKind=0;
'@
    Assert-Blocked 'Old failed-quorum attendance save reproduces the reported error' ($taskInsertChair + 'UPDATE dbo.ProcurementEvaluationMeetings SET UpdatedAt=SYSUTCDATETIME();') 51333
    Assert-Scalar 'SELECT COUNT(*) FROM dbo.ProcurementEvaluationAttendanceRecords' 0
    # Both EF command orderings must be safe within one SaveChanges transaction.
    foreach ($taskInsertFirst in @($true,$false)) {
        Invoke-TestSql 'BEGIN TRANSACTION'
        try {
            if ($taskInsertFirst) { Invoke-TestSql $taskInsertChair }
            Invoke-TestSql 'UPDATE dbo.ProcurementEvaluationMeetings SET Status=0,UpdatedAt=SYSUTCDATETIME();'
            if (-not $taskInsertFirst) { Invoke-TestSql $taskInsertChair }
            Assert-Scalar 'SELECT COUNT(*) FROM dbo.ProcurementEvaluationAttendanceRecords' 1
            Assert-Scalar 'SELECT COUNT(*) FROM dbo.ProcurementEvaluationMeetings WHERE Status=0 AND QuorumMet=0 AND QuorumIdempotencyKey=''failed-attempt'' AND QuorumSnapshotJson=''{"attempt":"failed","attendees":0}''' 1
        } finally { Invoke-TestSql 'IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION' }
        "PASS: Failed-quorum retry preserves the snapshot; insert-first=$taskInsertFirst"
    }
    Invoke-TestSql ('BEGIN TRANSACTION; UPDATE dbo.ProcurementEvaluationMeetings SET Status=0;' + $taskInsertChair + 'COMMIT;')
    Assert-Blocked 'Insufficient attendance cannot be confirmed' 'UPDATE dbo.ProcurementEvaluationMeetings SET Status=1,QuorumMet=1,SignedVotingAttendanceCount=1,ChairPresent=1,SecretaryPresent=1;' 51333
    Assert-Blocked 'Cross-tenant attendance is rejected' @'
INSERT dbo.ProcurementEvaluationAttendanceRecords
SELECT NEWID(),m.Id,a.Id,NEWID(),0,1,1,SYSUTCDATETIME() FROM dbo.ProcurementEvaluationMeetings m
JOIN dbo.ProcurementEvaluationCommitteeAppointments a ON a.CommitteeControlId=m.CommitteeControlId WHERE a.MemberKind=4;
'@ 51341
    Invoke-TestSql @'
INSERT dbo.ProcurementEvaluationAttendanceRecords
SELECT NEWID(),m.Id,a.Id,m.TenantId,0,1,1,SYSUTCDATETIME() FROM dbo.ProcurementEvaluationMeetings m
JOIN dbo.ProcurementEvaluationCommitteeAppointments a ON a.CommitteeControlId=m.CommitteeControlId WHERE a.MemberKind<>0;
UPDATE dbo.ProcurementEvaluationMeetings SET UpdatedAt=SYSUTCDATETIME();
'@
    Assert-Scalar 'SELECT COUNT(*) FROM dbo.ProcurementEvaluationMeetings WHERE Status=0 AND QuorumMet=0' 1
    Invoke-TestSql @'
UPDATE dbo.ProcurementEvaluationMeetings SET Status=1,QuorumMet=1,SignedVotingAttendanceCount=3,
 ChairPresent=1,SecretaryPresent=1,QuorumIdempotencyKey='fresh-attempt',QuorumSnapshotJson='{"attempt":"confirmed","attendees":3}';
'@
    Assert-Scalar 'SELECT COUNT(*) FROM dbo.ProcurementEvaluationMeetings WHERE Status=1 AND QuorumMet=1 AND SignedVotingAttendanceCount=3' 1
    'PASS: A separate confirmation admits all three signed eligible members'
    Assert-Blocked 'Confirmed meeting cannot reopen' 'UPDATE dbo.ProcurementEvaluationMeetings SET Status=0,QuorumMet=0;' 51332
    Assert-Blocked 'Confirmed attendance remains append-only' 'UPDATE dbo.ProcurementEvaluationAttendanceRecords SET IsPresent=0;' 51340
    Assert-Blocked 'Confirmed attendance cannot be deleted' 'DELETE dbo.ProcurementEvaluationAttendanceRecords;' 51340
    'PASSED: SQL Server attendance retry and quorum safeguards'
} finally {
    $SourceConnection.ChangeDatabase('master')
    if ($taskCreated) { Invoke-TestSql "ALTER DATABASE [$taskDatabase] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$taskDatabase]" }
    $SourceConnection.ChangeDatabase($taskOriginalDatabase)
}
