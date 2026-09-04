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

DECLARE @lockedOld nvarchar(max) = N'WHERE (d.Status IN (1, 2)
               OR (d.Status = 0 AND i.Status <> 0))
          AND EXISTS (';
DECLARE @lockedNew nvarchar(max) = N'WHERE ((d.Status IN (1, 2)
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
          AND EXISTS (';

DECLARE @lineageOld nvarchar(max) = N'           OR LEN(LTRIM(RTRIM(i.ContentReference))) = 0
           OR i.ContentChecksumSha256 LIKE ''%[^0-9A-Fa-f]%''';
DECLARE @lineageNew nvarchar(max) = N'           OR (i.ContentWorkflowEvidenceDocumentId IS NULL AND
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
               ))';

DECLARE @lockedMatches int = (LEN(@definition) - LEN(REPLACE(@definition, @lockedOld, N''))) / LEN(@lockedOld);
DECLARE @lineageMatches int = (LEN(@definition) - LEN(REPLACE(@definition, @lineageOld, N''))) / LEN(@lineageOld);
IF @lockedMatches <> 1 OR @lineageMatches <> 1
    THROW 51200, 'Tender-document lifecycle trigger does not match the single expected source shape.', 1;

DECLARE @updated nvarchar(max) = REPLACE(REPLACE(@definition, @lockedOld, @lockedNew), @lineageOld, @lineageNew);
IF @updated = @definition OR CHARINDEX(@lockedNew, @updated) = 0 OR CHARINDEX(@lineageNew, @updated) = 0
    THROW 51200, 'Tender-document lifecycle trigger could not be upgraded safely.', 1;
EXEC sys.sp_executesql @updated;");
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

DECLARE @lockedNew nvarchar(max) = N'WHERE ((d.Status IN (1, 2)
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
          AND EXISTS (';
DECLARE @lockedOld nvarchar(max) = N'WHERE (d.Status IN (1, 2)
               OR (d.Status = 0 AND i.Status <> 0))
          AND EXISTS (';
DECLARE @lineageNew nvarchar(max) = N'           OR (i.ContentWorkflowEvidenceDocumentId IS NULL AND
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
               ))';
DECLARE @lineageOld nvarchar(max) = N'           OR LEN(LTRIM(RTRIM(i.ContentReference))) = 0
           OR i.ContentChecksumSha256 LIKE ''%[^0-9A-Fa-f]%''';

DECLARE @lockedMatches int = (LEN(@definition) - LEN(REPLACE(@definition, @lockedNew, N''))) / LEN(@lockedNew);
DECLARE @lineageMatches int = (LEN(@definition) - LEN(REPLACE(@definition, @lineageNew, N''))) / LEN(@lineageNew);
IF @lockedMatches <> 1 OR @lineageMatches <> 1
    THROW 51200, 'Tender-document lifecycle trigger does not match the single expected upgraded shape.', 1;

DECLARE @updated nvarchar(max) = REPLACE(REPLACE(@definition, @lockedNew, @lockedOld), @lineageNew, @lineageOld);
IF @updated = @definition OR CHARINDEX(@lockedOld, @updated) = 0 OR CHARINDEX(@lineageOld, @updated) = 0
    THROW 51200, 'Tender-document lifecycle trigger could not be restored safely.', 1;
EXEC sys.sp_executesql @updated;");
    }
}
