using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260912220000_VendorPaymentDirectEvidence")]
public sealed class VendorPaymentDirectEvidence : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "VendorPaymentEvidenceLinks",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                VendorPaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RequirementKey = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                ClientRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RequestHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                FileUploadRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CentralDocumentRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CentralDocumentVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                ContentType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                FileSize = table.Column<long>(type: "bigint", nullable: false),
                ChecksumSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_VendorPaymentEvidenceLinks", item => item.Id);
                table.ForeignKey("FK_VendorPaymentEvidenceLinks_Tenants_TenantId", item => item.TenantId,
                    "Tenants", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_VendorPaymentEvidenceLinks_VendorPayment_VendorPaymentId", item => item.VendorPaymentId,
                    "VendorPayment", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_VendorPaymentEvidenceLinks_FileUploadRecords_FileUploadRecordId", item => item.FileUploadRecordId,
                    "FileUploadRecords", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_VendorPaymentEvidenceLinks_CentralDocumentRecords_CentralDocumentRecordId", item => item.CentralDocumentRecordId,
                    "CentralDocumentRecords", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_VendorPaymentEvidenceLinks_CentralDocumentVersions_CentralDocumentVersionId", item => item.CentralDocumentVersionId,
                    "CentralDocumentVersions", "Id", onDelete: ReferentialAction.Restrict);
            });
        foreach (var column in new[] { "VendorPaymentId", "FileUploadRecordId", "CentralDocumentRecordId", "CentralDocumentVersionId" })
            migrationBuilder.CreateIndex($"IX_VendorPaymentEvidenceLinks_{column}", "VendorPaymentEvidenceLinks", column);
        migrationBuilder.CreateIndex("UX_VendorPaymentEvidenceLinks_Tenant_Request", "VendorPaymentEvidenceLinks",
            new[] { "TenantId", "ClientRequestId" }, unique: true);
        migrationBuilder.CreateIndex("UX_VendorPaymentEvidenceLinks_Tenant_Payment_Requirement_Hash", "VendorPaymentEvidenceLinks",
            new[] { "TenantId", "VendorPaymentId", "RequirementKey", "ChecksumSha256" }, unique: true);

        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER dbo.TR_VendorPaymentEvidenceLinks_SourceGuard
            ON dbo.VendorPaymentEvidenceLinks AFTER INSERT, UPDATE, DELETE AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (SELECT 1 FROM deleted)
                    THROW 51900, 'Payment attachment history is immutable; attach a new document instead.', 1;
                IF EXISTS (SELECT 1 FROM inserted e WHERE e.IsDeleted=1 OR e.FileSize<=0
                    OR e.CreatedById IS NULL OR e.CreatedById='00000000-0000-0000-0000-000000000000'
                    OR e.ClientRequestId='00000000-0000-0000-0000-000000000000'
                    OR LEN(e.RequestHash)<>64 OR e.RequestHash COLLATE Latin1_General_100_BIN2 LIKE '%[^0-9A-Fa-f]%'
                    OR LEN(e.ChecksumSha256)<>64 OR e.ChecksumSha256 COLLATE Latin1_General_100_BIN2 LIKE '%[^0-9A-Fa-f]%'
                    OR NULLIF(LTRIM(RTRIM(e.RequirementKey)),'') IS NULL
                    OR (e.ExpiryDate IS NOT NULL AND e.ExpiryDate<=SYSUTCDATETIME())
                    OR NOT EXISTS (SELECT 1 FROM dbo.VendorPayment p WHERE p.Id=e.VendorPaymentId
                        AND p.TenantId=e.TenantId AND p.IsDeleted=0 AND p.Status=1 AND p.PaymentBatchId IS NULL
                        AND p.WorkflowInstanceId IS NULL AND p.JournalEntryId IS NULL
                        AND dbo.WorkflowApprovalRequiredAtSubmission(p.TenantId,N'VendorPayment',p.Id)=0))
                    THROW 51901, 'A payment attachment must belong to a current direct draft without an active approval process.', 1;
                IF EXISTS (SELECT 1 FROM inserted e WHERE NOT EXISTS (
                    SELECT 1 FROM dbo.CentralDocumentRecords r
                    JOIN dbo.CentralDocumentVersions v ON v.DocumentRecordId=r.Id AND v.TenantId=r.TenantId
                    JOIN dbo.FileUploadRecords f ON f.Id=v.FileUploadRecordId AND f.TenantId=v.TenantId
                    WHERE r.Id=e.CentralDocumentRecordId AND v.Id=e.CentralDocumentVersionId
                        AND f.Id=e.FileUploadRecordId AND r.TenantId=e.TenantId
                        AND r.IsDeleted=0 AND v.IsDeleted=0 AND f.IsDeleted=0 AND f.StorageDeletedAtUtc IS NULL
                        AND r.SourceModule=N'Finance' AND r.SourceEntityType=N'VendorPayment' AND r.SourceRecordId=e.VendorPaymentId
                        AND r.LifecycleStatus=N'Active' AND r.VersionStatus=N'Submitted' AND v.Status=N'Submitted'
                        AND r.CurrentVersion=v.VersionNumber AND (r.ExpiryDate IS NULL OR r.ExpiryDate>SYSUTCDATETIME())
                        AND f.Category=N'document-management' AND v.RepositoryPath=f.FilePath
                        AND f.VirusScanStatus=2 AND f.ScannedAtUtc IS NOT NULL
                        AND f.UploadedByUserId=e.CreatedById AND v.CreatedByUserId=e.CreatedById
                        AND f.FileSize=e.FileSize AND v.FileSize=e.FileSize
                        AND f.ContentType=e.ContentType AND v.ContentType=e.ContentType
                        AND f.OriginalFileName=e.FileName AND v.FileName=e.FileName))
                    THROW 51902, 'Payment evidence requires its exact current same-tenant source, version and clean stored file.', 1;
            END
            """);

        // Replace only the prior blanket document prohibition. MD/signature checks and the
        // immutable captured policy remain owned by the existing payment trigger.
        PatchBlanketEvidenceGuard(migrationBuilder, remove: true);
        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER dbo.TR_VendorPayment_DirectEvidence
            ON dbo.VendorPayment AFTER INSERT, UPDATE AS
            BEGIN
                SET NOCOUNT ON;
                -- Revalidate entry to readiness and first posting, not later reversals/history.
                IF EXISTS (SELECT 1 FROM inserted p LEFT JOIN deleted old ON old.Id=p.Id
                    CROSS APPLY OPENJSON(CASE WHEN p.ApprovalRequired=0 AND ISJSON(p.ApprovalControlSnapshotJson)=1
                        THEN p.ApprovalControlSnapshotJson ELSE N'{}' END,'$.evidenceRequirements') requirement
                    WHERE p.ApprovalRequired=0
                      AND (old.Id IS NULL OR old.ApprovalRequired=1 OR (old.JournalEntryId IS NULL AND p.JournalEntryId IS NOT NULL))
                      AND NULLIF(LTRIM(RTRIM(JSON_VALUE(requirement.value,'$.requirementKey'))),'') IS NOT NULL
                      AND (SELECT COUNT(DISTINCT e.ChecksumSha256) FROM dbo.VendorPaymentEvidenceLinks e
                        JOIN dbo.CentralDocumentRecords r ON r.Id=e.CentralDocumentRecordId AND r.TenantId=e.TenantId
                        JOIN dbo.CentralDocumentVersions v ON v.Id=e.CentralDocumentVersionId AND v.TenantId=e.TenantId
                            AND v.DocumentRecordId=r.Id AND v.FileUploadRecordId=e.FileUploadRecordId
                        JOIN dbo.FileUploadRecords f ON f.Id=e.FileUploadRecordId AND f.TenantId=e.TenantId
                        WHERE e.VendorPaymentId=p.Id AND e.TenantId=p.TenantId AND e.IsDeleted=0
                          AND e.RequirementKey=LTRIM(RTRIM(JSON_VALUE(requirement.value,'$.requirementKey')))
                          AND (e.ExpiryDate IS NULL OR e.ExpiryDate>SYSUTCDATETIME())
                          AND r.SourceModule=N'Finance' AND r.SourceEntityType=N'VendorPayment' AND r.SourceRecordId=p.Id
                          AND r.IsDeleted=0 AND v.IsDeleted=0 AND f.IsDeleted=0 AND f.StorageDeletedAtUtc IS NULL
                          AND r.LifecycleStatus=N'Active' AND r.VersionStatus=N'Submitted' AND v.Status=N'Submitted'
                          AND r.CurrentVersion=v.VersionNumber AND (r.ExpiryDate IS NULL OR r.ExpiryDate>SYSUTCDATETIME())
                          AND f.Category=N'document-management' AND v.RepositoryPath=f.FilePath
                          AND f.VirusScanStatus=2 AND f.ScannedAtUtc IS NOT NULL
                          AND f.UploadedByUserId=e.CreatedById AND v.CreatedByUserId=e.CreatedById
                          AND f.FileSize=e.FileSize AND v.FileSize=e.FileSize
                          AND f.ContentType=e.ContentType AND v.ContentType=e.ContentType
                          AND f.OriginalFileName=e.FileName AND v.FileName=e.FileName)
                        < CASE WHEN TRY_CONVERT(int,JSON_VALUE(requirement.value,'$.minimumDocuments'))>0
                            THEN TRY_CONVERT(int,JSON_VALUE(requirement.value,'$.minimumDocuments')) ELSE 1 END)
                    THROW 51903, 'Attach the required current clean payment documents before completing or posting the payment.', 1;
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF EXISTS (SELECT 1 FROM dbo.VendorPaymentEvidenceLinks)
                THROW 51904, 'Payment document history must be retained; this rollback is unsafe.', 1;
            DROP TRIGGER IF EXISTS dbo.TR_VendorPayment_DirectEvidence;
            DROP TRIGGER IF EXISTS dbo.TR_VendorPaymentEvidenceLinks_SourceGuard;
            """);
        PatchBlanketEvidenceGuard(migrationBuilder, remove: false);
        migrationBuilder.DropTable("VendorPaymentEvidenceLinks");
    }

    private static void PatchBlanketEvidenceGuard(MigrationBuilder builder, bool remove)
    {
        const string clause = "OR EXISTS (SELECT 1 FROM OPENJSON(i.ApprovalControlSnapshotJson,'$.evidenceRequirements') e\n                    WHERE NULLIF(LTRIM(RTRIM(JSON_VALUE(e.value,'$.requirementKey'))),'') IS NOT NULL)";
        const string marker = "/* Direct payment attachments are enforced by TR_VendorPayment_DirectEvidence. */";
        var before = remove ? clause : marker;
        var after = remove ? marker : clause;
        builder.Sql($"""
            DECLARE @definition nvarchar(max)=REPLACE(OBJECT_DEFINITION(OBJECT_ID(N'dbo.TR_VendorPayment_OptionalApproval')),CHAR(13),N'');
            DECLARE @before nvarchar(max)=N'{before.Replace("'", "''")}';
            DECLARE @after nvarchar(max)=N'{after.Replace("'", "''")}';
            -- EF emits Windows CRLF inside multiline SQL literals. Normalize the literals
            -- as well as OBJECT_DEFINITION before comparing the exact reviewed clause.
            SET @before=REPLACE(@before,CHAR(13),N'');
            SET @after=REPLACE(@after,CHAR(13),N'');
            IF @definition IS NULL OR (LEN(@definition)-LEN(REPLACE(@definition,@before,N'')))/LEN(@before)<>1
                THROW 51905, 'The reviewed payment evidence guard differs; no automatic replacement is allowed.', 1;
            SET @definition=REPLACE(@definition,@before,@after);
            DECLARE @trigger int=CHARINDEX(N'TRIGGER',@definition);
            IF @trigger=0 THROW 51905, 'The payment trigger declaration is unavailable.', 1;
            SET @definition=N'ALTER '+SUBSTRING(@definition,@trigger,LEN(@definition));
            EXEC sys.sp_executesql @definition;
            """);
    }
}
