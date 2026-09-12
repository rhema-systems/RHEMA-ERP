using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260904203000_AllowTenderDocumentWorkflowContentBinding")]
public sealed class AllowTenderDocumentWorkflowContentBinding : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_ProcurementTenderDocumentTemplateVersions_State",
            table: "ProcurementTenderDocumentTemplateVersions");

        migrationBuilder.AddCheckConstraint(
            name: "CK_ProcurementTenderDocumentTemplateVersions_State",
            table: "ProcurementTenderDocumentTemplateVersions",
            sql: "[Version] >= 1 AND [PolicySetVersion] >= 1 AND [Status] BETWEEN 0 AND 3 " +
                 "AND ([EffectiveToUtc] IS NULL OR [EffectiveToUtc] > [EffectiveFromUtc]) " +
                 "AND (([Status] IN (0, 1) AND LEN([ContentChecksumSha256]) IN (0, 64)) " +
                 "OR ([Status] IN (2, 3) AND LEN([ContentChecksumSha256]) = 64)) " +
                 "AND LEN([IntegrityHash]) = 64 AND ISJSON([LifecycleSnapshotJson]) = 1");

        migrationBuilder.Sql(@"
DECLARE @definition nvarchar(max) = OBJECT_DEFINITION(OBJECT_ID(N'[dbo].[TR_ProcurementTenderDocumentTemplateVersions_Lifecycle]'));
IF @definition IS NULL
    THROW 51200, 'Tender-document lifecycle trigger was not found.', 1;
SET @definition = REPLACE(@definition, CHAR(13) + CHAR(10), CHAR(10));
DECLARE @normalized nvarchar(max) = LOWER(@definition);
IF CHARINDEX(N'controlled tender-document template versions cannot be physically deleted.', @normalized) = 0
   OR CHARINDEX(N'tender-document template lifecycle transition is invalid.', @normalized) = 0
   OR CHARINDEX(N'pending, published, and retired tender-document template content and control lineage are immutable.', @normalized) = 0
    THROW 51200, 'Tender-document lifecycle trigger is not a recognized governed source variant.', 1;

DECLARE @lockedOldGrouped nvarchar(max) = N'where (d.status in (1, 2)
               or (d.status = 0 and i.status <> 0))
          and exists (';
DECLARE @lockedOldDirect nvarchar(max) = N'where d.status in (1, 2)
          and exists (';
SET @lockedOldGrouped = REPLACE(@lockedOldGrouped, CHAR(13) + CHAR(10), CHAR(10));
SET @lockedOldDirect = REPLACE(@lockedOldDirect, CHAR(13) + CHAR(10), CHAR(10));
DECLARE @lockedBegin nvarchar(100) = N'/* tdc-f05b-content-binding-lock-begin */';
DECLARE @lockedEnd nvarchar(100) = N'/* tdc-f05b-content-binding-lock-end */';
DECLARE @lockedNew nvarchar(max) = N'/* TDC-F05B-CONTENT-BINDING-LOCK-BEGIN */
          WHERE ((d.Status IN (1, 2)
                AND NOT (
                    d.Status = 1 AND i.Status = 1
                    AND d.ContentWorkflowEvidenceDocumentId IS NULL
                    AND d.ContentFileUploadRecordId IS NULL
                    AND LEN(LTRIM(RTRIM(d.ContentReference))) = 0
                    AND LEN(d.ContentChecksumSha256) = 0
                    AND i.ContentWorkflowEvidenceDocumentId IS NOT NULL
                    AND i.ContentFileUploadRecordId IS NULL
                    AND LEN(LTRIM(RTRIM(i.ContentReference))) > 0
                    AND LEN(i.ContentChecksumSha256) = 64
                    AND NOT EXISTS (
                        SELECT d.TemplateCode, d.Name, d.Description, d.DocumentTypeCode,
                               d.PolicySetId, d.PolicySetCode, d.PolicySetVersion,
                               d.SourceConfigurationProfileId, d.WorkflowDefinitionId,
                               d.SupersedesVersionId, d.ChangeSummary, d.EffectiveFromUtc
                        EXCEPT
                        SELECT i.TemplateCode, i.Name, i.Description, i.DocumentTypeCode,
                               i.PolicySetId, i.PolicySetCode, i.PolicySetVersion,
                               i.SourceConfigurationProfileId, i.WorkflowDefinitionId,
                               i.SupersedesVersionId, i.ChangeSummary, i.EffectiveFromUtc
                    )
                ))
               OR (d.Status = 0 AND i.Status <> 0))
          AND EXISTS (
          /* TDC-F05B-CONTENT-BINDING-LOCK-END */';

DECLARE @lineageOld nvarchar(max) = N'           or len(ltrim(rtrim(i.contentreference))) = 0
           or i.contentchecksumsha256 like ''%[^0-9a-fa-f]%''';
SET @lineageOld = REPLACE(@lineageOld, CHAR(13) + CHAR(10), CHAR(10));
DECLARE @lineageBegin nvarchar(100) = N'/* tdc-f05b-content-binding-lineage-begin */';
DECLARE @lineageEnd nvarchar(100) = N'/* tdc-f05b-content-binding-lineage-end */';
DECLARE @lineageNew nvarchar(max) = N'/* TDC-F05B-CONTENT-BINDING-LINEAGE-BEGIN */
           OR (i.ContentWorkflowEvidenceDocumentId IS NULL AND
               (i.ContentFileUploadRecordId IS NOT NULL
                OR LEN(LTRIM(RTRIM(i.ContentReference))) > 0
                OR LEN(i.ContentChecksumSha256) > 0))
           OR (i.ContentWorkflowEvidenceDocumentId IS NOT NULL AND
               (i.ContentFileUploadRecordId IS NOT NULL
                OR LEN(LTRIM(RTRIM(i.ContentReference))) = 0
                OR LEN(i.ContentChecksumSha256) <> 64
                OR i.ContentChecksumSha256 LIKE ''%[^0-9A-Fa-f]%''))
           OR (i.Status IN (2, 3) AND i.ContentWorkflowEvidenceDocumentId IS NULL)';
        SET @lineageNew = @lineageNew + N'
           OR (i.Status IN (1, 2, 3) AND i.ContentWorkflowEvidenceDocumentId IS NOT NULL
               AND i.WorkflowInstanceId IS NOT NULL AND NOT EXISTS (
                   SELECT 1
                   FROM WorkflowEvidenceDocuments contentEvidence
                   JOIN WorkflowStepInstances contentStep ON contentStep.Id = contentEvidence.StepInstanceId
                   WHERE contentEvidence.Id = i.ContentWorkflowEvidenceDocumentId
                     AND contentEvidence.TenantId = i.TenantId
                     AND contentEvidence.IsDeleted = 0
                      AND contentStep.TenantId = i.TenantId
                      AND contentStep.IsDeleted = 0
                      AND contentStep.WorkflowInstanceId = i.WorkflowInstanceId
               ))
           /* TDC-F05B-CONTENT-BINDING-LINEAGE-END */';

DECLARE @hasLockedMarker bit = IIF(CHARINDEX(@lockedBegin, @normalized) > 0, 1, 0);
DECLARE @hasLineageMarker bit = IIF(CHARINDEX(@lineageBegin, @normalized) > 0, 1, 0);
IF @hasLockedMarker <> @hasLineageMarker
    THROW 51200, 'Tender-document lifecycle trigger contains an incomplete content-binding upgrade.', 1;

IF @hasLockedMarker = 0
BEGIN
    DECLARE @groupedMatches int = (LEN(@normalized) - LEN(REPLACE(@normalized, @lockedOldGrouped, N''))) / LEN(@lockedOldGrouped);
    DECLARE @directMatches int = (LEN(@normalized) - LEN(REPLACE(@normalized, @lockedOldDirect, N''))) / LEN(@lockedOldDirect);
    DECLARE @lineageMatches int = (LEN(@normalized) - LEN(REPLACE(@normalized, @lineageOld, N''))) / LEN(@lineageOld);
    IF @groupedMatches + @directMatches <> 1
       OR @lineageMatches <> 1
    BEGIN
        DECLARE @shapeError nvarchar(2048) = CONCAT(
            N'Tender-document lifecycle trigger does not match one recognized legacy source shape. grouped=',
            @groupedMatches, N'; direct=', @directMatches, N'; lineage=', @lineageMatches, N'.');
        THROW 51200, @shapeError, 1;
    END

    DECLARE @lockedOld nvarchar(max) = IIF(@groupedMatches = 1, @lockedOldGrouped, @lockedOldDirect);
    DECLARE @actualLockedOld nvarchar(max) = SUBSTRING(
        @definition, CHARINDEX(@lockedOld, @normalized), LEN(@lockedOld));
    DECLARE @actualLineageOld nvarchar(max) = SUBSTRING(
        @definition, CHARINDEX(@lineageOld, @normalized), LEN(@lineageOld));
    DECLARE @updated nvarchar(max) = REPLACE(
        REPLACE(@definition, @actualLockedOld, @lockedNew),
        @actualLineageOld,
        @lineageNew);
    DECLARE @updatedNormalized nvarchar(max) = LOWER(@updated);
    IF @updated = @definition
       OR CHARINDEX(@lockedBegin, @updatedNormalized) = 0
       OR CHARINDEX(@lockedEnd, @updatedNormalized) = 0
       OR CHARINDEX(@lineageBegin, @updatedNormalized) = 0
       OR CHARINDEX(@lineageEnd, @updatedNormalized) = 0
       OR CHARINDEX(N'contentstep.workflowinstanceid = i.workflowinstanceid', @updatedNormalized) = 0
        THROW 51200, 'Tender-document lifecycle trigger could not be upgraded safely.', 1;
    DECLARE @executable nvarchar(max) = @updated;
    WHILE LEN(@executable) > 0
      AND UNICODE(LEFT(@executable, 1)) IN (9, 10, 13, 32)
        SET @executable = SUBSTRING(@executable, 2, LEN(@executable));
    DECLARE @header nvarchar(200) = LOWER(LEFT(@executable, 200));
    SET @header = REPLACE(REPLACE(REPLACE(@header, CHAR(9), N' '), CHAR(10), N' '), CHAR(13), N' ');
    WHILE CHARINDEX(N'  ', @header) > 0
        SET @header = REPLACE(@header, N'  ', N' ');
    IF @header LIKE N'create trigger %'
        SET @executable = N'ALTER' + SUBSTRING(@executable, 7, LEN(@executable));
    ELSE IF @header NOT LIKE N'create or alter trigger %'
         AND @header NOT LIKE N'alter trigger %'
        THROW 51200, 'Tender-document lifecycle trigger definition has an unsupported statement header.', 1;
    EXEC sys.sp_executesql @executable;
END
ELSE IF CHARINDEX(@lockedEnd, @normalized) = 0
     OR CHARINDEX(@lineageEnd, @normalized) = 0
     OR CHARINDEX(@lockedBegin, @normalized, CHARINDEX(@lockedBegin, @normalized) + 1) > 0
     OR CHARINDEX(@lockedEnd, @normalized, CHARINDEX(@lockedEnd, @normalized) + 1) > 0
     OR CHARINDEX(@lineageBegin, @normalized, CHARINDEX(@lineageBegin, @normalized) + 1) > 0
     OR CHARINDEX(@lineageEnd, @normalized, CHARINDEX(@lineageEnd, @normalized) + 1) > 0
     OR CHARINDEX(N'd.contentworkflowevidencedocumentid is null', @normalized) = 0
     OR CHARINDEX(N'i.contentworkflowevidencedocumentid is not null', @normalized) = 0
     OR CHARINDEX(N'i.status in (2, 3) and i.contentworkflowevidencedocumentid is null', @normalized) = 0
     OR CHARINDEX(N'contentstep.workflowinstanceid = i.workflowinstanceid', @normalized) = 0
    THROW 51200, 'Tender-document lifecycle trigger content-binding markers are incomplete.', 1;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
IF EXISTS (
    SELECT 1
    FROM ProcurementTenderDocumentTemplateVersions
    WHERE LEN(ContentChecksumSha256) <> 64 OR LEN(LTRIM(RTRIM(ContentReference))) = 0
       OR ContentWorkflowEvidenceDocumentId IS NULL
)
    THROW 51200, 'Cannot restore the former tender-document constraint while unbound Draft or Pending records exist.', 1;
");

        migrationBuilder.DropCheckConstraint(
            name: "CK_ProcurementTenderDocumentTemplateVersions_State",
            table: "ProcurementTenderDocumentTemplateVersions");

        migrationBuilder.AddCheckConstraint(
            name: "CK_ProcurementTenderDocumentTemplateVersions_State",
            table: "ProcurementTenderDocumentTemplateVersions",
            sql: "[Version] >= 1 AND [PolicySetVersion] >= 1 AND [Status] BETWEEN 0 AND 3 " +
                 "AND ([EffectiveToUtc] IS NULL OR [EffectiveToUtc] > [EffectiveFromUtc]) " +
                 "AND LEN([ContentChecksumSha256]) = 64 AND LEN([IntegrityHash]) = 64 " +
                 "AND ISJSON([LifecycleSnapshotJson]) = 1");

        migrationBuilder.Sql(@"
DECLARE @definition nvarchar(max) = OBJECT_DEFINITION(OBJECT_ID(N'[dbo].[TR_ProcurementTenderDocumentTemplateVersions_Lifecycle]'));
IF @definition IS NULL
    THROW 51200, 'Tender-document lifecycle trigger was not found.', 1;
SET @definition = REPLACE(@definition, CHAR(13) + CHAR(10), CHAR(10));
DECLARE @normalized nvarchar(max) = LOWER(@definition);
IF CHARINDEX(N'controlled tender-document template versions cannot be physically deleted.', @normalized) = 0
   OR CHARINDEX(N'tender-document template lifecycle transition is invalid.', @normalized) = 0
   OR CHARINDEX(N'pending, published, and retired tender-document template content and control lineage are immutable.', @normalized) = 0
    THROW 51200, 'Tender-document lifecycle trigger is not a recognized governed source variant.', 1;

DECLARE @lockedOld nvarchar(max) = N'WHERE (d.Status IN (1, 2)
               OR (d.Status = 0 AND i.Status <> 0))
          AND EXISTS (';
DECLARE @lineageOld nvarchar(max) = N'           OR LEN(LTRIM(RTRIM(i.ContentReference))) = 0
           OR i.ContentChecksumSha256 LIKE ''%[^0-9A-Fa-f]%''';
SET @lockedOld = REPLACE(@lockedOld, CHAR(13) + CHAR(10), CHAR(10));
SET @lineageOld = REPLACE(@lineageOld, CHAR(13) + CHAR(10), CHAR(10));
DECLARE @lockedBegin nvarchar(100) = N'/* tdc-f05b-content-binding-lock-begin */';
DECLARE @lockedEnd nvarchar(100) = N'/* tdc-f05b-content-binding-lock-end */';
DECLARE @lineageBegin nvarchar(100) = N'/* tdc-f05b-content-binding-lineage-begin */';
DECLARE @lineageEnd nvarchar(100) = N'/* tdc-f05b-content-binding-lineage-end */';
DECLARE @lockedStart int = CHARINDEX(@lockedBegin, @normalized);
DECLARE @lockedFinish int = CHARINDEX(@lockedEnd, @normalized);
DECLARE @lineageStart int = CHARINDEX(@lineageBegin, @normalized);
DECLARE @lineageFinish int = CHARINDEX(@lineageEnd, @normalized);

IF (@lockedStart = 0 AND @lineageStart > 0)
   OR (@lockedStart > 0 AND @lineageStart = 0)
    THROW 51200, 'Tender-document lifecycle trigger contains an incomplete content-binding upgrade.', 1;

IF @lockedStart > 0
BEGIN
    IF @lockedFinish <= @lockedStart OR @lineageFinish <= @lineageStart
       OR CHARINDEX(@lockedBegin, @normalized, @lockedStart + 1) > 0
       OR CHARINDEX(@lockedEnd, @normalized, @lockedFinish + 1) > 0
       OR CHARINDEX(@lineageBegin, @normalized, @lineageStart + 1) > 0
       OR CHARINDEX(@lineageEnd, @normalized, @lineageFinish + 1) > 0
        THROW 51200, 'Tender-document lifecycle trigger does not match one recognized upgraded source shape.', 1;

    DECLARE @updated nvarchar(max) = STUFF(
        @definition,
        @lineageStart,
        @lineageFinish + LEN(@lineageEnd) - @lineageStart,
        @lineageOld);
    SET @updated = STUFF(
        @updated,
        @lockedStart,
        @lockedFinish + LEN(@lockedEnd) - @lockedStart,
        @lockedOld);
    DECLARE @updatedNormalized nvarchar(max) = LOWER(@updated);
    IF @updated = @definition
       OR CHARINDEX(@lockedBegin, @updatedNormalized) > 0
       OR CHARINDEX(@lineageBegin, @updatedNormalized) > 0
       OR CHARINDEX(LOWER(@lockedOld), @updatedNormalized) = 0
       OR CHARINDEX(N'or len(ltrim(rtrim(i.contentreference))) = 0', @updatedNormalized) = 0
        THROW 51200, 'Tender-document lifecycle trigger could not be restored safely.', 1;
    DECLARE @executable nvarchar(max) = @updated;
    WHILE LEN(@executable) > 0
      AND UNICODE(LEFT(@executable, 1)) IN (9, 10, 13, 32)
        SET @executable = SUBSTRING(@executable, 2, LEN(@executable));
    DECLARE @header nvarchar(200) = LOWER(LEFT(@executable, 200));
    SET @header = REPLACE(REPLACE(REPLACE(@header, CHAR(9), N' '), CHAR(10), N' '), CHAR(13), N' ');
    WHILE CHARINDEX(N'  ', @header) > 0
        SET @header = REPLACE(@header, N'  ', N' ');
    IF @header LIKE N'create trigger %'
        SET @executable = N'ALTER' + SUBSTRING(@executable, 7, LEN(@executable));
    ELSE IF @header NOT LIKE N'create or alter trigger %'
         AND @header NOT LIKE N'alter trigger %'
        THROW 51200, 'Tender-document lifecycle trigger definition has an unsupported statement header.', 1;
    EXEC sys.sp_executesql @executable;
END
ELSE
BEGIN
    DECLARE @lockedMatches int = (LEN(@normalized) - LEN(REPLACE(@normalized, LOWER(@lockedOld), N''))) / LEN(@lockedOld);
    DECLARE @lineageMatches int = (LEN(@normalized) - LEN(REPLACE(@normalized, LOWER(@lineageOld), N''))) / LEN(@lineageOld);
    IF @lockedMatches <> 1 OR @lineageMatches <> 1
        THROW 51200, 'Tender-document lifecycle trigger does not match one recognized legacy source shape.', 1;
END;");
    }
}
