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

CREATE PROCEDURE #SeedQs0501
    @WorksheetId uniqueidentifier OUTPUT,
    @LineId uniqueidentifier OUTPUT,
    @RevisionId uniqueidentifier OUTPUT
AS
BEGIN
    DECLARE @TenantId uniqueidentifier, @ProjectId uniqueidentifier, @UserId uniqueidentifier,
            @Currency nvarchar(10), @VersionId uniqueidentifier=NEWID(), @BoqLineId uniqueidentifier=NEWID(),
            @LineKey uniqueidentifier=NEWID(), @ValuationId uniqueidentifier=NEWID(), @Now datetime2=SYSUTCDATETIME();
    SELECT TOP (1) @TenantId=p.TenantId, @ProjectId=p.Id, @UserId=u.Id,
        @Currency=COALESCE(NULLIF(p.BaseCurrencyCode,''),'GHS')
    FROM Projects p JOIN Users u ON u.TenantId=p.TenantId
    WHERE p.IsDeleted=0 ORDER BY p.CreatedAt, u.Id;
    IF @ProjectId IS NULL THROW 51129, 'QS-0501 SQL gate requires one existing project and tenant user.', 1;
    SET @WorksheetId=NEWID(); SET @LineId=NEWID(); SET @RevisionId=NEWID();

    INSERT ProjectBoqVersions
      (Id,ProjectId,VersionNumber,VersionType,Status,ApprovalStatus,ApprovedById,ApprovedAt,
       ChangeSummary,AuditAction,SnapshotHash,LineCount,SnapshotAt,ActorRoles,CorrelationId,
       CreatedAt,CreatedBy,CreatedById,IsDeleted,TenantId)
    VALUES
      (@VersionId,@ProjectId,900000+ABS(CHECKSUM(NEWID()))%90000,2,'Draft','Draft',NULL,NULL,
       'QS-0501 rollback-only SQL proof','ApproveBoqVersion',REPLICATE('a',64),1,@Now,'TDC_SUPERVISING_QUANTITY_SURVEYOR','qs0501-sql-gate',
       @Now,'QS0501 SQL gate',@UserId,0,@TenantId);

    INSERT ProjectBoqVersionLines
      (Id,ProjectId,ProjectBoqVersionId,LineKey,LineNumber,ItemCode,ItemType,Description,Quantity,
       UnitOfMeasure,UnitRate,LineAmount,Currency,SortOrder,CreatedAt,CreatedBy,CreatedById,IsDeleted,TenantId)
    VALUES
      (@BoqLineId,@ProjectId,@VersionId,@LineKey,'1.01','QS0501','Item','QS-0501 rollback-only concrete work',12,
       'm3',100,1200,@Currency,1,@Now,'QS0501 SQL gate',@UserId,0,@TenantId);

    UPDATE ProjectBoqVersions SET Status='Approved',ApprovalStatus='Approved',ApprovedById=@UserId,
        ApprovedAt=@Now,PublishedById=@UserId,PublishedAt=@Now
    WHERE Id=@VersionId;

    INSERT ProjectInterimValuations
      (Id,ProjectId,ValuationNumber,Title,Status,ValuationDate,GrossWorkValue,MaterialsOnSiteValue,
       VariationValue,RetentionPercentage,RetentionAmount,PreviousCertifiedAmount,NetValuationAmount,
       Currency,CreatedAt,CreatedBy,CreatedById,IsDeleted,TenantId)
    VALUES
      (@ValuationId,@ProjectId,'QS0501-GATE','QS-0501 rollback-only valuation','Draft',@Now,700,0,
       0,10,70,200,430,@Currency,@Now,'QS0501 SQL gate',@UserId,0,@TenantId);

    INSERT QuantitySurveyValuationWorksheets
      (Id,ProjectId,ProjectInterimValuationId,ProjectBoqVersionId,ClientRequestId,RequestHash,Status,
       RetentionPercentage,MeasuredToDateValue,PreviouslyCertifiedValue,CurrentClaimedValue,
       CurrentCertifiedValue,CurrentPeriodCertifiedValue,DisputedValue,RetentionToDateValue,
       CurrentRetentionValue,NetCurrentValue,LineCount,PreparedById,PreparedByName,PreparedAt,
       AuditAction,CorrelationId,ActorRoles,TenantId,CreatedAt,CreatedBy,CreatedById,IsDeleted)
    VALUES
      (@WorksheetId,@ProjectId,@ValuationId,@VersionId,NEWID(),REPLICATE('b',64),'Draft',
       10,1000,200,800,700,500,100,70,50,450,1,@UserId,'QS-0501 SQL gate',@Now,
       'CreateValuationWorksheet','qs0501-sql-gate','TDC_QUANTITY_SURVEYOR',@TenantId,@Now,'QS0501 SQL gate',@UserId,0);

    INSERT QuantitySurveyValuationWorksheetLines
      (Id,WorksheetId,ProjectBoqVersionLineId,BoqLineKey,Sequence,LineNumberSnapshot,ItemCodeSnapshot,
       DescriptionSnapshot,UnitOfMeasureSnapshot,CurrencySnapshot,BoqQuantitySnapshot,UnitRateSnapshot,
       MeasuredToDateQuantity,PreviouslyCertifiedQuantity,CurrentClaimedQuantity,CurrentCertifiedQuantity,
       DisputedQuantity,MeasuredToDateValue,PreviouslyCertifiedValue,CurrentClaimedValue,CurrentCertifiedValue,
       CurrentPeriodCertifiedValue,DisputedValue,PreviousRetentionValue,RetentionToDateValue,
       CurrentRetentionValue,NetCurrentValue,ReviewNote,TenantId,CreatedAt,CreatedBy,CreatedById,IsDeleted)
    VALUES
      (@LineId,@WorksheetId,@BoqLineId,@LineKey,1,'1.01','QS0501','QS-0501 rollback-only concrete work',
       'm3',@Currency,12,100,10,2,8,7,1,1000,200,800,700,500,100,20,70,50,450,
       'One measured unit remains disputed.',@TenantId,@Now,'QS0501 SQL gate',@UserId,0);

    INSERT QuantitySurveyValuationWorksheetRevisions
      (Id,WorksheetId,Action,ActorUserId,ActorName,ActorRoles,CorrelationId,AfterJson,
       TenantId,CreatedAt,CreatedBy,CreatedById,IsDeleted)
    VALUES
      (@RevisionId,@WorksheetId,'CreateValuationWorksheet',@UserId,'QS-0501 SQL gate',
       'TDC_QUANTITY_SURVEYOR','qs0501-sql-gate','{}',@TenantId,@Now,'QS0501 SQL gate',@UserId,0);
END;
GO

DECLARE @WorksheetId uniqueidentifier, @LineId uniqueidentifier, @RevisionId uniqueidentifier;
BEGIN TRANSACTION;
EXEC #SeedQs0501 @WorksheetId OUTPUT,@LineId OUTPUT,@RevisionId OUTPUT;
IF NOT EXISTS (
    SELECT 1 FROM QuantitySurveyValuationWorksheets w
    JOIN QuantitySurveyValuationWorksheetLines l ON l.WorksheetId=w.Id AND l.TenantId=w.TenantId
    WHERE w.Id=@WorksheetId AND w.CurrentPeriodCertifiedValue=500 AND w.CurrentRetentionValue=50
      AND w.NetCurrentValue=450 AND l.DisputedQuantity=1 AND l.NetCurrentValue=450)
    THROW 51129, 'QS-0501 positive reconciliation proof failed.', 1;
ROLLBACK;
PRINT 'QS0501_POSITIVE_RECONCILIATION=PASS';

DECLARE @BlockedWorksheet uniqueidentifier, @BlockedLine uniqueidentifier, @BlockedRevision uniqueidentifier,
        @LineageError int=0;
BEGIN TRANSACTION;
EXEC #SeedQs0501 @BlockedWorksheet OUTPUT,@BlockedLine OUTPUT,@BlockedRevision OUTPUT;
BEGIN TRY
    UPDATE QuantitySurveyValuationWorksheetLines SET BoqLineKey=NEWID() WHERE Id=@BlockedLine;
END TRY
BEGIN CATCH
    SET @LineageError=ERROR_NUMBER();
END CATCH;
IF XACT_STATE() <> 0 ROLLBACK;
IF @LineageError <> 51120 THROW 51129, 'QS-0501 stable-line SQL bypass was not blocked.', 1;
PRINT 'QS0501_STABLE_LINE_BYPASS=BLOCKED_51120';

DECLARE @AuditWorksheet uniqueidentifier, @AuditLine uniqueidentifier, @AuditRevision uniqueidentifier,
        @AuditError int=0;
BEGIN TRANSACTION;
EXEC #SeedQs0501 @AuditWorksheet OUTPUT,@AuditLine OUTPUT,@AuditRevision OUTPUT;
BEGIN TRY
    DELETE QuantitySurveyValuationWorksheetRevisions WHERE Id=@AuditRevision;
END TRY
BEGIN CATCH
    SET @AuditError=ERROR_NUMBER();
END CATCH;
IF XACT_STATE() <> 0 ROLLBACK;
IF @AuditError <> 51122 THROW 51129, 'QS-0501 append-only audit bypass was not blocked.', 1;
PRINT 'QS0501_APPEND_ONLY_BYPASS=BLOCKED_51122';

DROP PROCEDURE #SeedQs0501;
