-- Operator-approved, one-off LOCAL UAT repair. NOT a migration or production recovery path.
-- Called by Invoke-LocalUatEvaluationCorrection.ps1. No triggers/constraints are disabled.
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
IF DB_NAME() <> 'RhemaERP' OR CONVERT(nvarchar(128), SERVERPROPERTY('MachineName')) <> 'RHEMA-MICHAEL'
   OR CONVERT(nvarchar(128), SERVERPROPERTY('InstanceName')) <> 'SQL2017'
    THROW 51601, 'This correction is restricted to the reviewed local SQL instance and database.', 1;
IF @Mode NOT IN ('Preview','Rehearse','Apply') THROW 51602, 'Invalid correction mode.', 1;
DECLARE @tenant uniqueidentifier='00000000-0000-0000-0000-000000000001',
        @tender uniqueidentifier='55d2a153-a1ee-4217-9a8d-6014cd27a520',
        @bid uniqueidentifier='5367ed43-04bf-4b1e-a6c3-4d6b174b6b90',
        @committee uniqueidentifier='0a503d49-a8c2-4550-a7b8-ca12ec69ee37',
        @oldTemplate uniqueidentifier='81ffb484-258f-474b-8ae8-1d9a31ef4c36',
        @newTemplate uniqueidentifier='dc2839e9-d639-4898-a674-15c0e1f917e7',
        @audit uniqueidentifier='7c299fac-7af0-459a-8656-aa2f6c43c9d5',
        @actor uniqueidentifier='088b60a2-cdb3-4c78-b553-202a02e70b65';
BEGIN TRY
    BEGIN TRANSACTION;
    IF EXISTS(SELECT 1 FROM dbo.AuditLogs WITH (HOLDLOCK) WHERE Id=@audit AND TenantId=@tenant
        AND Action='LocalUatEvaluationCorrection')
    BEGIN
        IF NOT EXISTS(SELECT 1 FROM dbo.Tenders t JOIN dbo.EvaluationTemplates e ON e.Id=t.EvaluationTemplateId
            AND e.TenantId=t.TenantId WHERE t.Id=@tender AND t.TenantId=@tenant AND e.Id=@newTemplate
            AND e.ScoringMethod='WeightedAverage' AND t.UseQCBSEvaluation=0)
            THROW 51603, 'Recorded correction no longer matches the tender; manual review required.', 1;
        ROLLBACK;
        SELECT 'AlreadyApplied' AS Result, @tender AS TenderId, @newTemplate AS TemplateId, @audit AS AuditId;
        RETURN;
    END;
    IF NOT EXISTS(SELECT 1 FROM dbo.Tenders WITH (UPDLOCK,HOLDLOCK)
        WHERE Id=@tender AND TenantId=@tenant AND IsDeleted=0 AND Status='Closed'
        AND TenderNumber='TND-2026-0001' AND UseQCBSEvaluation=0 AND EvaluationTemplateId=@oldTemplate
        AND Description LIKE 'LOCAL UAT ONLY - PROC-STORE-UAT-20260905-01.%')
        THROW 51604, 'The exact authorized LOCAL UAT tender no longer matches the reviewed state.', 1;
    IF (SELECT COUNT(*) FROM dbo.TenderBids WITH (UPDLOCK,HOLDLOCK) WHERE TenderId=@tender AND TenantId=@tenant AND IsDeleted=0)<>1
        OR NOT EXISTS(SELECT 1 FROM dbo.TenderBids WHERE Id=@bid AND TenderId=@tender AND TenantId=@tenant
            AND Status='Opened' AND TotalBidAmount=52000 AND OpenedDate IS NOT NULL)
        THROW 51605, 'Expected one opened, unscored local test bid.', 1;
    IF EXISTS(SELECT 1 FROM dbo.TenderEvaluations WITH (HOLDLOCK) WHERE TenantId=@tenant AND TenderBidId=@bid)
        THROW 51606, 'Evaluation history exists; this correction must not change scoring after evaluation.', 1;
    IF NOT EXISTS(SELECT 1 FROM dbo.Users WHERE Id=@actor AND Username='procurementofficer' AND IsActive=1)
        THROW 51607, 'Expected local operator context not found.', 1;
    IF NOT EXISTS(SELECT 1 FROM dbo.EvaluationTemplates WITH (UPDLOCK,HOLDLOCK)
        WHERE Id=@oldTemplate AND TenantId=@tenant AND IsDeleted=0 AND IsActive=1 AND Category='Goods'
        AND TenderType='ITB' AND ScoringMethod='QCBS' AND PassingScore=80)
        THROW 51608, 'Original template no longer matches the reviewed Goods configuration.', 1;
    IF EXISTS(SELECT 1 FROM dbo.EvaluationTemplates WHERE Id=@newTemplate OR
        (TenantId=@tenant AND TemplateCode='LOCAL-UAT-GOODS-20260905'))
        THROW 51609, 'Correction template already exists without the expected audit record.', 1;
    IF (SELECT COUNT(*) FROM dbo.EvaluationTemplateCriteria WITH (HOLDLOCK)
        WHERE EvaluationTemplateId=@oldTemplate AND TenantId=@tenant AND IsDeleted=0)<>5
        OR (SELECT SUM(Weight) FROM dbo.EvaluationTemplateCriteria
        WHERE EvaluationTemplateId=@oldTemplate AND TenantId=@tenant AND IsDeleted=0)<>100
        THROW 51610, 'Expected the five original criteria with total weight 100.', 1;
    IF (SELECT COUNT(*) FROM dbo.ProcurementEvaluationCommitteeAppointments WITH (HOLDLOCK)
        WHERE TenantId=@tenant AND CommitteeControlId=@committee AND IsDeleted=0)<>3
        THROW 51611, 'Expected the existing three-member committee evidence.', 1;

    DECLARE @beforeTender nvarchar(max)=(SELECT * FROM dbo.Tenders WHERE Id=@tender AND TenantId=@tenant
            FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES),
        @templateJson nvarchar(max)=(SELECT * FROM dbo.EvaluationTemplates WHERE Id=@oldTemplate AND TenantId=@tenant
            FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES),
        @criteriaJson nvarchar(max)=(SELECT EvaluationCriterionId,Weight,MaxScore,IsMandatory,MinimumScore,DisplayOrder
            FROM dbo.EvaluationTemplateCriteria WHERE EvaluationTemplateId=@oldTemplate AND TenantId=@tenant AND IsDeleted=0
            ORDER BY DisplayOrder,EvaluationCriterionId FOR JSON PATH, INCLUDE_NULL_VALUES),
        @beforeProtected nvarchar(max)=(SELECT JSON_QUERY((SELECT * FROM dbo.TenderBids WITH (HOLDLOCK) WHERE TenantId=@tenant AND TenderId=@tender ORDER BY Id FOR JSON PATH, INCLUDE_NULL_VALUES)) AS Bids,
JSON_QUERY((SELECT * FROM dbo.TenderBidItems WITH (HOLDLOCK) WHERE TenantId=@tenant AND TenderBidId=@bid ORDER BY Id FOR JSON PATH, INCLUDE_NULL_VALUES)) AS BidItems,
JSON_QUERY((SELECT * FROM dbo.TenderBidLots WITH (HOLDLOCK) WHERE TenantId=@tenant AND TenderBidId=@bid ORDER BY Id FOR JSON PATH, INCLUDE_NULL_VALUES)) AS BidLots,
JSON_QUERY((SELECT * FROM dbo.TenderBidDocuments WITH (HOLDLOCK) WHERE TenantId=@tenant AND TenderBidId=@bid ORDER BY Id FOR JSON PATH, INCLUDE_NULL_VALUES)) AS BidDocuments,
JSON_QUERY((SELECT * FROM dbo.ProcurementEvaluationCommitteeControls WITH (HOLDLOCK) WHERE TenantId=@tenant AND SourceId=@tender ORDER BY Id FOR JSON PATH, INCLUDE_NULL_VALUES)) AS Committee,
JSON_QUERY((SELECT * FROM dbo.ProcurementEvaluationCommitteeAppointments WITH (HOLDLOCK) WHERE TenantId=@tenant AND CommitteeControlId=@committee ORDER BY Id FOR JSON PATH, INCLUDE_NULL_VALUES)) AS Appointments,
JSON_QUERY((SELECT * FROM dbo.ProcurementEvaluationConflictDeclarations WITH (HOLDLOCK) WHERE TenantId=@tenant AND AppointmentId IN (SELECT Id FROM dbo.ProcurementEvaluationCommitteeAppointments WHERE TenantId=@tenant AND CommitteeControlId=@committee) ORDER BY Id FOR JSON PATH, INCLUDE_NULL_VALUES)) AS Declarations,
JSON_QUERY((SELECT * FROM dbo.ProcurementEvaluationMeetings WITH (HOLDLOCK) WHERE TenantId=@tenant AND CommitteeControlId=@committee ORDER BY Id FOR JSON PATH, INCLUDE_NULL_VALUES)) AS Meetings,
JSON_QUERY((SELECT * FROM dbo.ProcurementEvaluationAttendanceRecords WITH (HOLDLOCK) WHERE TenantId=@tenant AND MeetingId IN (SELECT Id FROM dbo.ProcurementEvaluationMeetings WHERE TenantId=@tenant AND CommitteeControlId=@committee) ORDER BY Id FOR JSON PATH, INCLUDE_NULL_VALUES)) AS Attendance,
JSON_QUERY((SELECT * FROM dbo.Tenders WITH (HOLDLOCK) WHERE TenantId=@tenant AND Id<>@tender ORDER BY Id FOR JSON PATH, INCLUDE_NULL_VALUES)) AS OtherTenders,
JSON_QUERY((SELECT * FROM dbo.EvaluationTemplates WITH (HOLDLOCK) WHERE TenantId=@tenant AND Id<>@newTemplate ORDER BY Id FOR JSON PATH, INCLUDE_NULL_VALUES)) AS OriginalTemplates,
JSON_QUERY((SELECT * FROM dbo.EvaluationTemplateCriteria WITH (HOLDLOCK) WHERE TenantId=@tenant AND EvaluationTemplateId<>@newTemplate ORDER BY Id FOR JSON PATH, INCLUDE_NULL_VALUES)) AS OriginalCriteria FOR JSON PATH, WITHOUT_ARRAY_WRAPPER);
    DECLARE @fingerprint varchar(64)=CONVERT(varchar(64),HASHBYTES('SHA2_256',
        @beforeTender+@templateJson+@criteriaJson+@beforeProtected),2),
        @protectedHash varchar(64)=CONVERT(varchar(64),HASHBYTES('SHA2_256',@beforeProtected),2);
    IF @Mode='Preview'
    BEGIN
        ROLLBACK;
        SELECT 'Preview' AS Result,@fingerprint AS Fingerprint,@protectedHash AS PreservedEvidenceHash,
            @tender AS TenderId,@oldTemplate AS OriginalTemplateId,@newTemplate AS CorrectedTemplateId,
            'WeightedAverage' AS ProposedScoringMethod,5 AS CriteriaCount,80 AS PassingScore,@criteriaJson AS Criteria;
        RETURN;
    END;
    IF NULLIF(@ExpectedFingerprint,'') IS NULL OR @ExpectedFingerprint<>@fingerprint
        THROW 51612, 'Reviewed fingerprint required; state changed or preview was not supplied.', 1;

    DECLARE @now datetime2=SYSUTCDATETIME(), @reason nvarchar(1000)=
        'LOCAL UAT ONLY. Operator explicitly approved in Codex a recorded correction from the mismatched QCBS template to non-QCBS weighted scoring, retaining all five criteria, weights, passing score, submitted bid and committee evidence. Executed as local maintenance, not as a procurement workflow approval.';
    INSERT dbo.EvaluationTemplates
        (Id,TemplateName,TemplateCode,Description,Category,TenderType,IsDefault,IsActive,PassingScore,
         ScoringMethod,DisplayOrder,TechnicalWeight,FinancialWeight,MinimumTechnicalScore,CreatedById,CreatedAt,IsDeleted,TenantId)
    SELECT @newTemplate,'LOCAL UAT - Standard Goods Evaluation','LOCAL-UAT-GOODS-20260905',@reason,
        Category,TenderType,0,1,PassingScore,'WeightedAverage',DisplayOrder,TechnicalWeight,FinancialWeight,
        MinimumTechnicalScore,@actor,@now,0,@tenant
    FROM dbo.EvaluationTemplates WHERE Id=@oldTemplate AND TenantId=@tenant;
    IF @@ROWCOUNT<>1 THROW 51613, 'Expected exactly one cloned template.', 1;
    INSERT dbo.EvaluationTemplateCriteria
        (Id,EvaluationTemplateId,EvaluationCriterionId,Weight,MaxScore,IsMandatory,MinimumScore,DisplayOrder,
         CreatedAt,CreatedById,IsDeleted,TenantId)
    SELECT NEWID(),@newTemplate,EvaluationCriterionId,Weight,MaxScore,IsMandatory,MinimumScore,DisplayOrder,
        @now,@actor,0,@tenant FROM dbo.EvaluationTemplateCriteria
        WHERE EvaluationTemplateId=@oldTemplate AND TenantId=@tenant AND IsDeleted=0;
    IF @@ROWCOUNT<>5 THROW 51614, 'Expected exactly five cloned criteria.', 1;
    UPDATE dbo.Tenders SET EvaluationTemplateId=@newTemplate,UpdatedAt=@now,LastModifiedById=@actor,
        UpdatedBy='Codex operator-approved local UAT maintenance',
        Notes=CONCAT(Notes,CHAR(10),@reason,' Audit: ',CONVERT(nvarchar(36),@audit))
        WHERE Id=@tender AND TenantId=@tenant AND Status='Closed' AND EvaluationTemplateId=@oldTemplate;
    IF @@ROWCOUNT<>1 THROW 51615, 'Expected exactly one corrected tender.', 1;

    DECLARE @afterTender nvarchar(max)=(SELECT * FROM dbo.Tenders WHERE Id=@tender AND TenantId=@tenant
            FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES),
        @afterProtected nvarchar(max)=(SELECT JSON_QUERY((SELECT * FROM dbo.TenderBids WITH (HOLDLOCK) WHERE TenantId=@tenant AND TenderId=@tender ORDER BY Id FOR JSON PATH, INCLUDE_NULL_VALUES)) AS Bids,
JSON_QUERY((SELECT * FROM dbo.TenderBidItems WITH (HOLDLOCK) WHERE TenantId=@tenant AND TenderBidId=@bid ORDER BY Id FOR JSON PATH, INCLUDE_NULL_VALUES)) AS BidItems,
JSON_QUERY((SELECT * FROM dbo.TenderBidLots WITH (HOLDLOCK) WHERE TenantId=@tenant AND TenderBidId=@bid ORDER BY Id FOR JSON PATH, INCLUDE_NULL_VALUES)) AS BidLots,
JSON_QUERY((SELECT * FROM dbo.TenderBidDocuments WITH (HOLDLOCK) WHERE TenantId=@tenant AND TenderBidId=@bid ORDER BY Id FOR JSON PATH, INCLUDE_NULL_VALUES)) AS BidDocuments,
JSON_QUERY((SELECT * FROM dbo.ProcurementEvaluationCommitteeControls WITH (HOLDLOCK) WHERE TenantId=@tenant AND SourceId=@tender ORDER BY Id FOR JSON PATH, INCLUDE_NULL_VALUES)) AS Committee,
JSON_QUERY((SELECT * FROM dbo.ProcurementEvaluationCommitteeAppointments WITH (HOLDLOCK) WHERE TenantId=@tenant AND CommitteeControlId=@committee ORDER BY Id FOR JSON PATH, INCLUDE_NULL_VALUES)) AS Appointments,
JSON_QUERY((SELECT * FROM dbo.ProcurementEvaluationConflictDeclarations WITH (HOLDLOCK) WHERE TenantId=@tenant AND AppointmentId IN (SELECT Id FROM dbo.ProcurementEvaluationCommitteeAppointments WHERE TenantId=@tenant AND CommitteeControlId=@committee) ORDER BY Id FOR JSON PATH, INCLUDE_NULL_VALUES)) AS Declarations,
JSON_QUERY((SELECT * FROM dbo.ProcurementEvaluationMeetings WITH (HOLDLOCK) WHERE TenantId=@tenant AND CommitteeControlId=@committee ORDER BY Id FOR JSON PATH, INCLUDE_NULL_VALUES)) AS Meetings,
JSON_QUERY((SELECT * FROM dbo.ProcurementEvaluationAttendanceRecords WITH (HOLDLOCK) WHERE TenantId=@tenant AND MeetingId IN (SELECT Id FROM dbo.ProcurementEvaluationMeetings WHERE TenantId=@tenant AND CommitteeControlId=@committee) ORDER BY Id FOR JSON PATH, INCLUDE_NULL_VALUES)) AS Attendance,
JSON_QUERY((SELECT * FROM dbo.Tenders WITH (HOLDLOCK) WHERE TenantId=@tenant AND Id<>@tender ORDER BY Id FOR JSON PATH, INCLUDE_NULL_VALUES)) AS OtherTenders,
JSON_QUERY((SELECT * FROM dbo.EvaluationTemplates WITH (HOLDLOCK) WHERE TenantId=@tenant AND Id<>@newTemplate ORDER BY Id FOR JSON PATH, INCLUDE_NULL_VALUES)) AS OriginalTemplates,
JSON_QUERY((SELECT * FROM dbo.EvaluationTemplateCriteria WITH (HOLDLOCK) WHERE TenantId=@tenant AND EvaluationTemplateId<>@newTemplate ORDER BY Id FOR JSON PATH, INCLUDE_NULL_VALUES)) AS OriginalCriteria FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
        @clonedCriteria nvarchar(max)=(SELECT EvaluationCriterionId,Weight,MaxScore,IsMandatory,MinimumScore,DisplayOrder
            FROM dbo.EvaluationTemplateCriteria WHERE EvaluationTemplateId=@newTemplate AND TenantId=@tenant AND IsDeleted=0
            ORDER BY DisplayOrder,EvaluationCriterionId FOR JSON PATH, INCLUDE_NULL_VALUES);
    IF HASHBYTES('SHA2_256',@beforeProtected)<>HASHBYTES('SHA2_256',@afterProtected) OR @criteriaJson<>@clonedCriteria
        THROW 51616, 'Preservation check failed; all changes rolled back.', 1;
    DECLARE @expectedTender nvarchar(max)=JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(
        @beforeTender,'$.EvaluationTemplateId',JSON_VALUE(@afterTender,'$.EvaluationTemplateId')),
        '$.UpdatedAt',JSON_VALUE(@afterTender,'$.UpdatedAt')),'$.LastModifiedById',JSON_VALUE(@afterTender,'$.LastModifiedById')),
        '$.UpdatedBy',JSON_VALUE(@afterTender,'$.UpdatedBy')),'$.Notes',JSON_VALUE(@afterTender,'$.Notes'));
    IF HASHBYTES('SHA2_256',@expectedTender)<>HASHBYTES('SHA2_256',@afterTender)
        THROW 51617, 'Unexpected tender field changed; all changes rolled back.', 1;

    DECLARE @oldValues nvarchar(max)=(SELECT JSON_QUERY(@beforeTender) AS Tender,JSON_QUERY(@templateJson) AS OriginalTemplate,
        JSON_QUERY(@criteriaJson) AS Criteria,@protectedHash AS PreservedEvidenceHash FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),
        @newValues nvarchar(max)=(SELECT JSON_QUERY(@afterTender) AS Tender,@newTemplate AS CorrectedTemplateId,
        'WeightedAverage' AS ScoringMethod,JSON_QUERY(@clonedCriteria) AS Criteria,@protectedHash AS PreservedEvidenceHash,
        @reason AS OperatorAuthorization,CAST(0 AS bit) AS IsWorkflowApproval FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
    INSERT dbo.AuditLogs
        (Id,UserId,Username,Action,Resource,ResourceId,OldValues,NewValues,IpAddress,UserAgent,Timestamp,TenantId,
         CreatedAt,CreatedBy,CreatedById,IsDeleted)
    VALUES(@audit,@actor,'procurementofficer','LocalUatEvaluationCorrection','Tender',CONVERT(nvarchar(36),@tender),
        @oldValues,@newValues,'127.0.0.1','Codex operator-approved local maintenance; not a workflow approval',
        @now,@tenant,@now,'Codex local UAT maintenance',@actor,0);
    IF @Mode='Rehearse' ROLLBACK; ELSE COMMIT;
    SELECT CASE WHEN @Mode='Rehearse' THEN 'RehearsedAndRolledBack' ELSE 'Applied' END AS Result,
        @fingerprint AS Fingerprint,@protectedHash AS PreservedEvidenceHash,@tender AS TenderId,
        @newTemplate AS TemplateId,@audit AS AuditId,@now AS ObservedAtUtc,5 AS CriteriaCount,80 AS PassingScore;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT>0 ROLLBACK;
    THROW;
END CATCH;
