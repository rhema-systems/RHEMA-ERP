param(
    [Parameter(Mandatory)][System.Data.Common.DbConnection]$SourceConnection,
    [Parameter(Mandatory)][Guid]$TenderId,
    [Parameter(Mandatory)][Guid]$TemplateId
)
$ErrorActionPreference = 'Stop'
$taskSourceDatabase = $SourceConnection.Database
$taskDatabase = 'RhemaERP_FirstBindingTest_' + [Guid]::NewGuid().ToString('N')
$taskCreated = $false
function Invoke-ProbeSql([string]$Sql, [switch]$Scalar) {
    $taskCommand = $SourceConnection.CreateCommand()
    try {
        $taskCommand.CommandText = $Sql
        $taskCommand.CommandTimeout = 60
        if ($Scalar) { return $taskCommand.ExecuteScalar() }
        [void]$taskCommand.ExecuteNonQuery()
    } finally { $taskCommand.Dispose() }
}
function Test-Insert([string]$Name, [string]$Setup, [bool]$Allowed, [string]$After = '') {
    $taskTransaction = $SourceConnection.BeginTransaction()
    $taskCommand = $SourceConnection.CreateCommand()
    try {
        $taskCommand.Transaction = $taskTransaction
        $taskCommand.CommandText = $Setup + '; INSERT INTO dbo.ProcurementTenderDocumentRegisters SELECT * FROM dbo.BindingPrototype; ' + $After
        [void]$taskCommand.ExecuteNonQuery()
        if (-not $Allowed) { throw "Guard did not reject: $Name" }
        "PASS: $Name"
    } catch {
        if ($Allowed -or $_.Exception.ToString() -notmatch '51206|51207|lineage is invalid|lineage is immutable') { throw }
        "PASS: $Name rejected"
    } finally {
        try { $taskTransaction.Rollback() } catch { }
        $taskCommand.Dispose(); $taskTransaction.Dispose()
    }
}
try {
    $taskTrigger = Invoke-ProbeSql "SELECT OBJECT_DEFINITION(OBJECT_ID(N'dbo.TR_ProcurementTenderDocumentRegisters_Immutable'))" -Scalar
    if (-not $taskTrigger -or $taskTrigger -notmatch 'OR i.OriginalSubmissionDeadlineUtc <= i.BoundAtUtc') { throw 'Source guard is not the expected pre-upgrade version.' }
    $taskSourceQuoted = '[' + $taskSourceDatabase.Replace(']', ']]') + ']'
    Invoke-ProbeSql "CREATE DATABASE [$taskDatabase]"
    $taskCreated = $true
    $SourceConnection.ChangeDatabase($taskDatabase)
    $taskTables = @('ProcurementTenderDocumentRegisters','ProcurementSourcingCases','ProcurementPolicyMethodRules','ProcurementPolicySets','ProcurementConfigurationProfiles','ProcurementTenderDocumentTemplateVersions','ProcurementTenderDocumentTemplateMethods','Tenders','RequestForQuotations','TenderBids','ProcurementTenderControls')
    foreach ($taskTable in $taskTables) {
        $taskColumns = Invoke-ProbeSql "SELECT STRING_AGG(CAST(CASE WHEN c.system_type_id=189 THEN 'CAST(' + QUOTENAME(c.name) + ' AS binary(8)) AS ' + QUOTENAME(c.name) ELSE QUOTENAME(c.name) END AS nvarchar(max)), ',') WITHIN GROUP (ORDER BY c.column_id) FROM $taskSourceQuoted.sys.columns c JOIN $taskSourceQuoted.sys.tables t ON t.object_id=c.object_id WHERE t.name='$taskTable'" -Scalar
        Invoke-ProbeSql "SELECT TOP (0) $taskColumns INTO dbo.[$taskTable] FROM $taskSourceQuoted.dbo.[$taskTable]"
    }
    Invoke-ProbeSql @"
INSERT INTO dbo.Tenders SELECT * FROM $taskSourceQuoted.dbo.Tenders WHERE Id='$TenderId';
INSERT INTO dbo.ProcurementSourcingCases SELECT * FROM $taskSourceQuoted.dbo.ProcurementSourcingCases WHERE Id=(SELECT SourcingCaseId FROM dbo.Tenders);
INSERT INTO dbo.ProcurementPolicyMethodRules SELECT * FROM $taskSourceQuoted.dbo.ProcurementPolicyMethodRules WHERE Id=(SELECT MethodRuleId FROM dbo.ProcurementSourcingCases);
INSERT INTO dbo.ProcurementPolicySets SELECT * FROM $taskSourceQuoted.dbo.ProcurementPolicySets WHERE Id=(SELECT PolicySetId FROM dbo.ProcurementSourcingCases);
INSERT INTO dbo.ProcurementConfigurationProfiles SELECT * FROM $taskSourceQuoted.dbo.ProcurementConfigurationProfiles WHERE Id=(SELECT SourceConfigurationProfileId FROM dbo.ProcurementPolicySets);
INSERT INTO dbo.ProcurementTenderDocumentTemplateVersions SELECT * FROM $taskSourceQuoted.dbo.ProcurementTenderDocumentTemplateVersions WHERE Id='$TemplateId';
INSERT INTO dbo.ProcurementTenderDocumentTemplateMethods SELECT * FROM $taskSourceQuoted.dbo.ProcurementTenderDocumentTemplateMethods WHERE TemplateVersionId='$TemplateId';
SELECT TOP (1) * INTO dbo.BindingPrototype FROM $taskSourceQuoted.dbo.ProcurementTenderDocumentRegisters WHERE SourceType=0 AND IsDeleted=0;
IF (SELECT COUNT(*) FROM dbo.BindingPrototype)<>1 OR (SELECT COUNT(*) FROM dbo.Tenders)<>1 THROW 50000, 'Missing isolated fixture',1;
UPDATE dbo.Tenders SET Status='Approved', PublishDate=NULL, PublishedById=NULL, RequiresPrequalification=0, UseQCBSEvaluation=0,
 SubmissionDeadline=DATEADD(hour,-2,SYSUTCDATETIME()), OpeningDate=DATEADD(hour,-1,SYSUTCDATETIME());
UPDATE p SET Id=NEWID(), TenantId=t.TenantId, SourceType=0, TenderId=t.Id, RequestForQuotationId=NULL,
 SourcingCaseId=sc.Id, MethodRuleId=sc.MethodRuleId, Method=sc.SelectedMethod, MethodRuleCode=sc.MethodRuleCode,
 PolicySetId=sc.PolicySetId, PolicySetCode=sc.PolicyCode, PolicySetVersion=sc.PolicyVersion,
 SourceConfigurationProfileId=ps.SourceConfigurationProfileId, InitialTemplateVersionId='$TemplateId',
 OriginalSubmissionDeadlineUtc=t.SubmissionDeadline, OpeningScheduledAtUtc=t.OpeningDate,
 OriginalBidValidityUntilUtc=DATEADD(day,90,SYSUTCDATETIME()), BoundAtUtc=SYSUTCDATETIME(), CurrencyCode=t.Currency
 FROM dbo.BindingPrototype p CROSS JOIN dbo.Tenders t JOIN dbo.ProcurementSourcingCases sc ON sc.Id=t.SourcingCaseId
 JOIN dbo.ProcurementPolicySets ps ON ps.Id=sc.PolicySetId;
"@
    Invoke-ProbeSql $taskTrigger
    Test-Insert 'Original guard rejects elapsed first binding' '' $false
    $taskMigrationPath = Join-Path $PSScriptRoot '../../src/ErpSystem.Data/Migrations/20260907161000_AllowUnpublishedFirstBindingScheduleApproval.cs'
    $taskMigration = Get-Content -Raw -LiteralPath $taskMigrationPath
    $taskSql = [regex]::Match($taskMigration, '(?s)UpgradeGuard\s*=\s*"""\s*(.*?)\s*""";').Groups[1].Value
    if (-not $taskSql) { throw 'Migration SQL missing.' }
    Invoke-ProbeSql $taskSql
    Test-Insert 'Eligible expired NCT retains original dates' '' $true
    Test-Insert 'Published tender' "UPDATE dbo.Tenders SET Status='Published', PublishDate=SYSUTCDATETIME()" $false
    Test-Insert 'Historically published tender' 'UPDATE dbo.Tenders SET PublishedById=NEWID()' $false
    Test-Insert 'Prequalification' 'UPDATE dbo.Tenders SET RequiresPrequalification=1' $false
    Test-Insert 'QCBS' 'UPDATE dbo.Tenders SET UseQCBSEvaluation=1' $false
    Test-Insert 'Existing bid' "INSERT INTO dbo.TenderBids SELECT TOP (1) * FROM $taskSourceQuoted.dbo.TenderBids WHERE IsDeleted=0; UPDATE b SET TenderId=t.Id,TenantId=t.TenantId FROM dbo.TenderBids b CROSS JOIN dbo.Tenders t" $false
    Test-Insert 'Advertised statutory controls' @'
INSERT INTO dbo.ProcurementTenderControls
(Id,TenderId,SourcingCaseId,MethodRuleId,AuthorityRouteId,Method,MethodRuleCode,AuthorityRouteReference,Status,
 AdvertisementReference,PublicationChannel,TenderDocumentReference,TenderDocumentVersion,DocumentFee,AdvertisementEvidenceReference,
 AdvertisedAtUtc,SubmissionDeadlineUtc,OpeningScheduledAtUtc,ApprovalActorsJson,LifecycleSnapshotJson,IntegrityHash,RowVersion,CreatedAt,IsDeleted,TenantId)
SELECT NEWID(),TenderId,SourcingCaseId,MethodRuleId,NEWID(),Method,MethodRuleCode,'UAT',0,
 'UAT','UAT','UAT','v1',0,'UAT',SYSUTCDATETIME(),OriginalSubmissionDeadlineUtc,OpeningScheduledAtUtc,'[]','{}',REPLICATE('a',64),0x00,SYSUTCDATETIME(),0,TenantId
FROM dbo.BindingPrototype
'@ $false
    Test-Insert 'Expired validity' 'UPDATE dbo.BindingPrototype SET OriginalBidValidityUntilUtc=DATEADD(minute,-1,BoundAtUtc)' $false
    Test-Insert 'Policy mismatch' 'UPDATE dbo.BindingPrototype SET PolicySetVersion=PolicySetVersion+1' $false
    Test-Insert 'Opening mismatch' 'UPDATE dbo.BindingPrototype SET OpeningScheduledAtUtc=DATEADD(minute,1,OpeningScheduledAtUtc)' $false
    Test-Insert 'Currency mismatch' "UPDATE dbo.BindingPrototype SET CurrencyCode='USD'" $false
    Test-Insert 'Original history remains immutable' '' $false 'UPDATE dbo.ProcurementTenderDocumentRegisters SET OriginalSubmissionDeadlineUtc=DATEADD(day,1,OriginalSubmissionDeadlineUtc)'
    Test-Insert 'Normal future binding still works' 'UPDATE dbo.Tenders SET SubmissionDeadline=DATEADD(day,1,SYSUTCDATETIME()), OpeningDate=DATEADD(day,2,SYSUTCDATETIME()); UPDATE p SET OriginalSubmissionDeadlineUtc=t.SubmissionDeadline,OpeningScheduledAtUtc=t.OpeningDate FROM dbo.BindingPrototype p CROSS JOIN dbo.Tenders t' $true
    'SQL guard probe passed. Only the disposable test database was modified.'
} finally {
    if ($taskCreated) {
        if ($taskDatabase -notmatch '^RhemaERP_FirstBindingTest_[a-f0-9]{32}$' -or $taskDatabase -eq $taskSourceDatabase) { throw 'Unsafe disposable database cleanup target.' }
        $SourceConnection.ChangeDatabase('master')
        Invoke-ProbeSql "ALTER DATABASE [$taskDatabase] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$taskDatabase]"
        'Removed the generated disposable test database; source records were unchanged.'
    }
    $SourceConnection.ChangeDatabase($taskSourceDatabase)
}
