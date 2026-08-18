SET NOCOUNT ON;
SET XACT_ABORT OFF;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
GO

IF NOT EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId='20260810134646_AddQuantitySurveyInterimValuationWorkflow')
    THROW 51979, 'QS-0502 migration is not applied.', 1;
IF EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE parent_object_id IN (OBJECT_ID('dbo.QuantitySurveyValuationWorksheets'),
                               OBJECT_ID('dbo.QuantitySurveyValuationWorksheetEvidence'),
                               OBJECT_ID('dbo.QuantitySurveyValuationWorksheetRevisions'))
      AND (is_disabled=1 OR is_not_trusted=1))
    THROW 51979, 'QS-0502 has a disabled or untrusted foreign key.', 1;
IF EXISTS (
    SELECT required.Name
    FROM (VALUES
        ('TR_QsValuationWorksheets_Guard'),
        ('TR_QsValuationWorksheetLines_Guard'),
        ('TR_QsValuationWorksheetEvidence_AppendOnly'),
        ('TR_QsValuationWorksheetRevisions_AppendOnly')) required(Name)
    LEFT JOIN sys.triggers actual ON actual.name=required.Name AND actual.is_disabled=0
    WHERE actual.object_id IS NULL)
    THROW 51979, 'A required QS-0502 SQL guard is missing or disabled.', 1;
PRINT 'QS0502_SCHEMA_AND_TRIGGERS=PASS';
GO

CREATE PROCEDURE #SeedQs0502
    @WorksheetId uniqueidentifier OUTPUT,
    @LineId uniqueidentifier OUTPUT,
    @RevisionId uniqueidentifier OUTPUT,
    @UserId uniqueidentifier OUTPUT
AS
BEGIN
    DECLARE @TenantId uniqueidentifier, @ProjectId uniqueidentifier,
            @Currency nvarchar(10), @VersionId uniqueidentifier=NEWID(), @BoqLineId uniqueidentifier=NEWID(),
            @LineKey uniqueidentifier=NEWID(), @ValuationId uniqueidentifier=NEWID(), @Now datetime2=SYSUTCDATETIME();
    SELECT TOP (1) @TenantId=p.TenantId, @ProjectId=p.Id, @UserId=u.Id,
        @Currency=COALESCE(NULLIF(p.BaseCurrencyCode,''),'GHS')
    FROM Projects p JOIN Users u ON u.TenantId=p.TenantId
    WHERE p.IsDeleted=0 ORDER BY p.CreatedAt, u.Id;
    IF @ProjectId IS NULL THROW 51979, 'QS-0502 SQL gate requires one existing project and tenant user.', 1;
    SET @WorksheetId=NEWID(); SET @LineId=NEWID(); SET @RevisionId=NEWID();

    INSERT ProjectBoqVersions
      (Id,ProjectId,VersionNumber,VersionType,Status,ApprovalStatus,ApprovedById,ApprovedAt,
       ChangeSummary,AuditAction,SnapshotHash,LineCount,SnapshotAt,ActorRoles,CorrelationId,
       CreatedAt,CreatedBy,CreatedById,IsDeleted,TenantId)
    VALUES
      (@VersionId,@ProjectId,910000+ABS(CHECKSUM(NEWID()))%80000,2,'Draft','Draft',NULL,NULL,
       'QS-0502 rollback-only SQL proof','ApproveBoqVersion',REPLICATE('a',64),1,@Now,
       'TDC_SUPERVISING_QUANTITY_SURVEYOR','qs0502-sql-gate',@Now,'QS0502 SQL gate',@UserId,0,@TenantId);

    INSERT ProjectBoqVersionLines
      (Id,ProjectId,ProjectBoqVersionId,LineKey,LineNumber,ItemCode,ItemType,Description,Quantity,
       UnitOfMeasure,UnitRate,LineAmount,Currency,SortOrder,CreatedAt,CreatedBy,CreatedById,IsDeleted,TenantId)
    VALUES
      (@BoqLineId,@ProjectId,@VersionId,@LineKey,'1.01','QS0502','Item','QS-0502 rollback-only concrete work',12,
       'm3',100,1200,@Currency,1,@Now,'QS0502 SQL gate',@UserId,0,@TenantId);

    UPDATE ProjectBoqVersions SET Status='Approved',ApprovalStatus='Approved',ApprovedById=@UserId,
        ApprovedAt=@Now,PublishedById=@UserId,PublishedAt=@Now WHERE Id=@VersionId;

    INSERT ProjectInterimValuations
      (Id,ProjectId,ValuationNumber,Title,Status,ValuationDate,GrossWorkValue,MaterialsOnSiteValue,
       VariationValue,RetentionPercentage,RetentionAmount,PreviousCertifiedAmount,NetValuationAmount,
       Currency,CreatedAt,CreatedBy,CreatedById,IsDeleted,TenantId)
    VALUES
      (@ValuationId,@ProjectId,'QS0502-GATE','QS-0502 rollback-only valuation','Draft',@Now,700,0,
       0,10,70,200,430,@Currency,@Now,'QS0502 SQL gate',@UserId,0,@TenantId);

    INSERT QuantitySurveyValuationWorksheets
      (Id,ProjectId,ProjectInterimValuationId,ProjectBoqVersionId,ClientRequestId,RequestHash,Status,
       RetentionPercentage,MeasuredToDateValue,PreviouslyCertifiedValue,CurrentClaimedValue,
       CurrentCertifiedValue,CurrentPeriodCertifiedValue,DisputedValue,RetentionToDateValue,
       CurrentRetentionValue,NetCurrentValue,LineCount,PreparedById,PreparedByName,PreparedAt,
       AuditAction,CorrelationId,ActorRoles,TenantId,CreatedAt,CreatedBy,CreatedById,IsDeleted)
    VALUES
      (@WorksheetId,@ProjectId,@ValuationId,@VersionId,NEWID(),REPLICATE('b',64),'Draft',
       10,1000,200,800,700,500,100,70,50,450,1,@UserId,'QS-0502 SQL gate',@Now,
       'CreateValuationWorksheet','qs0502-sql-gate','TDC_QUANTITY_SURVEYOR',@TenantId,@Now,'QS0502 SQL gate',@UserId,0);

    INSERT QuantitySurveyValuationWorksheetLines
      (Id,WorksheetId,ProjectBoqVersionLineId,BoqLineKey,Sequence,LineNumberSnapshot,ItemCodeSnapshot,
       DescriptionSnapshot,UnitOfMeasureSnapshot,CurrencySnapshot,BoqQuantitySnapshot,UnitRateSnapshot,
       MeasuredToDateQuantity,PreviouslyCertifiedQuantity,CurrentClaimedQuantity,CurrentCertifiedQuantity,
       DisputedQuantity,MeasuredToDateValue,PreviouslyCertifiedValue,CurrentClaimedValue,CurrentCertifiedValue,
       CurrentPeriodCertifiedValue,DisputedValue,PreviousRetentionValue,RetentionToDateValue,
       CurrentRetentionValue,NetCurrentValue,ReviewNote,TenantId,CreatedAt,CreatedBy,CreatedById,IsDeleted)
    VALUES
      (@LineId,@WorksheetId,@BoqLineId,@LineKey,1,'1.01','QS0502','QS-0502 rollback-only concrete work',
       'm3',@Currency,12,100,10,2,8,7,1,1000,200,800,700,500,100,20,70,50,450,
       'One measured unit remains disputed.',@TenantId,@Now,'QS0502 SQL gate',@UserId,0);

    INSERT QuantitySurveyValuationWorksheetRevisions
      (Id,WorksheetId,Action,ActorUserId,ActorName,ActorRoles,CorrelationId,AfterJson,
       TenantId,CreatedAt,CreatedBy,CreatedById,IsDeleted)
    VALUES
      (@RevisionId,@WorksheetId,'CreateValuationWorksheet',@UserId,'QS-0502 SQL gate',
       'TDC_QUANTITY_SURVEYOR','qs0502-sql-gate','{}',@TenantId,@Now,'QS0502 SQL gate',@UserId,0);
END;
GO

DECLARE @Worksheet uniqueidentifier,@Line uniqueidentifier,@Revision uniqueidentifier,@User uniqueidentifier;
BEGIN TRANSACTION;
EXEC #SeedQs0502 @Worksheet OUTPUT,@Line OUTPUT,@Revision OUTPUT,@User OUTPUT;
IF NOT EXISTS (SELECT 1 FROM QuantitySurveyValuationWorksheets WHERE Id=@Worksheet AND Status='Draft' AND ApprovalStatus='Draft')
    THROW 51979, 'QS-0502 default aligned state proof failed.', 1;
ROLLBACK;
PRINT 'QS0502_DRAFT_DEFAULT_ALIGNMENT=PASS';

DECLARE @ReadyWorksheet uniqueidentifier,@ReadyLine uniqueidentifier,@ReadyRevision uniqueidentifier,@ReadyUser uniqueidentifier,
        @ReadinessError int=0;
BEGIN TRANSACTION;
EXEC #SeedQs0502 @ReadyWorksheet OUTPUT,@ReadyLine OUTPUT,@ReadyRevision OUTPUT,@ReadyUser OUTPUT;
BEGIN TRY
    UPDATE QuantitySurveyValuationWorksheets
    SET Status='QsVetted',QsVettedById=@ReadyUser,QsVettedAt=SYSUTCDATETIME(),QsReviewNote='SQL bypass attempt'
    WHERE Id=@ReadyWorksheet;
END TRY
BEGIN CATCH
    SET @ReadinessError=ERROR_NUMBER();
END CATCH;
IF XACT_STATE()<>0 ROLLBACK;
IF @ReadinessError<>51964 THROW 51979, 'QS-0502 missing-policy/readiness bypass was not blocked.', 1;
PRINT 'QS0502_READINESS_BYPASS=BLOCKED_51964';

DECLARE @DeleteWorksheet uniqueidentifier,@DeleteLine uniqueidentifier,@DeleteRevision uniqueidentifier,@DeleteUser uniqueidentifier,
        @DeleteError int=0;
BEGIN TRANSACTION;
EXEC #SeedQs0502 @DeleteWorksheet OUTPUT,@DeleteLine OUTPUT,@DeleteRevision OUTPUT,@DeleteUser OUTPUT;
BEGIN TRY
    DELETE QuantitySurveyValuationWorksheets WHERE Id=@DeleteWorksheet;
END TRY
BEGIN CATCH
    SET @DeleteError=ERROR_NUMBER();
END CATCH;
IF XACT_STATE()<>0 ROLLBACK;
IF @DeleteError NOT IN (547,51960) THROW 51979, 'QS-0502 retained worksheet delete bypass was not blocked.', 1;
PRINT 'QS0502_DELETE_BYPASS=BLOCKED_BY_TRUSTED_FK_OR_51960';

DECLARE @LineWorksheet uniqueidentifier,@BlockedLine uniqueidentifier,@LineRevision uniqueidentifier,@LineUser uniqueidentifier,
        @LineError int=0;
BEGIN TRANSACTION;
EXEC #SeedQs0502 @LineWorksheet OUTPUT,@BlockedLine OUTPUT,@LineRevision OUTPUT,@LineUser OUTPUT;
BEGIN TRY
    UPDATE QuantitySurveyValuationWorksheetLines SET MeasuredToDateQuantity=11 WHERE Id=@BlockedLine;
END TRY
BEGIN CATCH
    SET @LineError=ERROR_NUMBER();
END CATCH;
IF XACT_STATE()<>0 ROLLBACK;
IF @LineError<>51967 THROW 51979, 'QS-0502 source-measurement immutability bypass was not blocked.', 1;
PRINT 'QS0502_LINE_BYPASS=BLOCKED_51967';

DECLARE @AuditWorksheet uniqueidentifier,@AuditLine uniqueidentifier,@AuditRevision uniqueidentifier,@AuditUser uniqueidentifier,
        @AuditError int=0;
BEGIN TRANSACTION;
EXEC #SeedQs0502 @AuditWorksheet OUTPUT,@AuditLine OUTPUT,@AuditRevision OUTPUT,@AuditUser OUTPUT;
BEGIN TRY
    DELETE QuantitySurveyValuationWorksheetRevisions WHERE Id=@AuditRevision;
END TRY
BEGIN CATCH
    SET @AuditError=ERROR_NUMBER();
END CATCH;
IF XACT_STATE()<>0 ROLLBACK;
IF @AuditError<>51122 THROW 51979, 'QS-0502 append-only revision bypass was not blocked.', 1;
PRINT 'QS0502_REVISION_BYPASS=BLOCKED_51122';

DROP PROCEDURE #SeedQs0502;
PRINT 'QS0502_SQL_RELEASE_GATE=PASS';
