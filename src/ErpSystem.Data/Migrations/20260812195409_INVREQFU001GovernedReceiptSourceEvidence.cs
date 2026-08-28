using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

public partial class INVREQFU001GovernedReceiptSourceEvidence : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_EmergencyProcurementPlans_TenantId",
            table: "EmergencyProcurementPlans");

        migrationBuilder.CreateIndex(
            name: "IX_EmergencyProcurementPlans_ExceptionalSourcingTenderId",
            table: "EmergencyProcurementPlans",
            column: "ExceptionalSourcingTenderId");
        migrationBuilder.CreateIndex(
            name: "IX_EmergencyProcurementPlans_ExceptionRuleId",
            table: "EmergencyProcurementPlans",
            column: "ExceptionRuleId");
        migrationBuilder.CreateIndex(
            name: "IX_EmergencyProcurementPlans_PurchaseRequisitionId",
            table: "EmergencyProcurementPlans",
            column: "PurchaseRequisitionId");
        migrationBuilder.CreateIndex(
            name: "IX_EmergencyProcurementPlans_WorkflowInstanceId",
            table: "EmergencyProcurementPlans",
            column: "WorkflowInstanceId");

        migrationBuilder.CreateTable(
            name: "ProcurementReceiptSourceEvidence",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PurchaseOrderReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EvidenceKind = table.Column<int>(type: "int", nullable: false),
                ReferenceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                DocumentDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                IsCurrent = table.Column<bool>(type: "bit", nullable: false),
                ClientRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RequestHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                OriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                ContentType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                FileSize = table.Column<long>(type: "bigint", nullable: false),
                ChecksumSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                FileUploadRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CentralDocumentRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CentralDocumentVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SupersededAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                SupersededByEvidenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProcurementReceiptSourceEvidence", x => x.Id);
                table.CheckConstraint("CK_ProcurementReceiptSourceEvidence_Current", "([IsCurrent] = 1 AND [SupersededAtUtc] IS NULL AND [SupersededByEvidenceId] IS NULL) OR ([IsCurrent] = 0 AND [SupersededAtUtc] IS NOT NULL AND [SupersededByEvidenceId] IS NOT NULL)");
                table.CheckConstraint("CK_ProcurementReceiptSourceEvidence_File", "[FileSize] > 0");
                table.CheckConstraint("CK_ProcurementReceiptSourceEvidence_Hashes", "LEN([RequestHash]) = 64 AND LEN([ChecksumSha256]) = 64");
                table.CheckConstraint("CK_ProcurementReceiptSourceEvidence_Kind", "[EvidenceKind] IN (1,2)");
                table.ForeignKey("FK_ProcurementReceiptSourceEvidence_CentralDocumentRecords_CentralDocumentRecordId", x => x.CentralDocumentRecordId, "CentralDocumentRecords", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_ProcurementReceiptSourceEvidence_CentralDocumentVersions_CentralDocumentVersionId", x => x.CentralDocumentVersionId, "CentralDocumentVersions", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_ProcurementReceiptSourceEvidence_FileUploadRecords_FileUploadRecordId", x => x.FileUploadRecordId, "FileUploadRecords", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_ProcurementReceiptSourceEvidence_PurchaseOrderReceipts_PurchaseOrderReceiptId", x => x.PurchaseOrderReceiptId, "PurchaseOrderReceipts", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_ProcurementReceiptSourceEvidence_Tenants_TenantId", x => x.TenantId, "Tenants", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex("IX_ProcurementReceiptSourceEvidence_CentralDocumentRecordId", "ProcurementReceiptSourceEvidence", "CentralDocumentRecordId");
        migrationBuilder.CreateIndex("IX_ProcurementReceiptSourceEvidence_CentralDocumentVersionId", "ProcurementReceiptSourceEvidence", "CentralDocumentVersionId");
        migrationBuilder.CreateIndex("IX_ProcurementReceiptSourceEvidence_FileUploadRecordId", "ProcurementReceiptSourceEvidence", "FileUploadRecordId");
        migrationBuilder.CreateIndex("IX_ProcurementReceiptSourceEvidence_PurchaseOrderReceiptId", "ProcurementReceiptSourceEvidence", "PurchaseOrderReceiptId");
        migrationBuilder.CreateIndex("IX_ProcurementReceiptSourceEvidence_TenantId_ClientRequestId", "ProcurementReceiptSourceEvidence", new[] { "TenantId", "ClientRequestId" }, unique: true);
        migrationBuilder.CreateIndex("IX_ProcurementReceiptSourceEvidence_TenantId_PurchaseOrderReceiptId_EvidenceKind", "ProcurementReceiptSourceEvidence", new[] { "TenantId", "PurchaseOrderReceiptId", "EvidenceKind" }, unique: true, filter: "[IsCurrent] = 1 AND [IsDeleted] = 0");

        migrationBuilder.Sql(
            """
            UPDATE [dbo].[CentralDocumentMetadataTemplates]
               SET [Module]='Procurement',
                   [DocumentType]='PurchaseReceiptSourceEvidence',
                   [SourceLabel]='Purchase receipt source evidence',
                   [RequiredFieldsJson]='["sourceReference","documentFamily","classification","sourceStatus","uploadedBy","checksumSha256"]',
                   [RelationshipsJson]='["PurchaseOrderReceipt","PurchaseOrder","BusinessPartner","VendorInvoice"]',
                   [RetentionRule]='TDC-PROC-RET-7Y',
                   [AccessProfile]='Procurement receipt and AP restricted',
                   [IsActive]=1,
                   [PublishedAt]=COALESCE([PublishedAt],SYSUTCDATETIME()),
                   [UpdatedAt]=SYSUTCDATETIME(),
                   [UpdatedBy]='INV-REQ-FU-001 migration'
             WHERE [TemplateCode]='TDC-PROC-RECEIPT-SOURCE' AND [IsDeleted]=0;

            INSERT INTO [dbo].[CentralDocumentMetadataTemplates]
                ([Id], [Module], [DocumentType], [TemplateCode], [SourceLabel],
                 [RequiredFieldsJson], [RelationshipsJson], [RetentionRule], [AccessProfile],
                 [IsActive], [PublishedAt], [CreatedAt], [CreatedBy], [IsDeleted], [TenantId])
            SELECT NEWID(), 'Procurement', 'PurchaseReceiptSourceEvidence', 'TDC-PROC-RECEIPT-SOURCE',
                   'Purchase receipt source evidence',
                   '["sourceReference","documentFamily","classification","sourceStatus","uploadedBy","checksumSha256"]',
                   '["PurchaseOrderReceipt","PurchaseOrder","BusinessPartner","VendorInvoice"]',
                   'TDC-PROC-RET-7Y', 'Procurement receipt and AP restricted',
                   1, SYSUTCDATETIME(), SYSUTCDATETIME(), 'INV-REQ-FU-001 migration', 0, tenant.Id
            FROM [dbo].[Tenants] tenant
            WHERE tenant.IsDeleted = 0
              AND NOT EXISTS (SELECT 1 FROM [dbo].[CentralDocumentMetadataTemplates] template
                              WHERE template.TenantId = tenant.Id
                                AND template.TemplateCode = 'TDC-PROC-RECEIPT-SOURCE');

            UPDATE [dbo].[CentralDocumentAccessRules]
               SET [IsActive]=1,
                   [UpdatedAt]=SYSUTCDATETIME(),
                   [UpdatedBy]='INV-REQ-FU-001 migration'
             WHERE [AccessProfile]='Procurement receipt and AP restricted'
               AND [Module]='Procurement' AND [IsDeleted]=0;

            INSERT INTO [dbo].[CentralDocumentAccessRules]
                ([Id], [AccessProfile], [Module], [RoleName], [PermissionKey],
                 [CanView], [CanUpload], [CanAnnotate], [CanApprove], [CanArchive], [IsActive],
                 [CreatedAt], [CreatedBy], [IsDeleted], [TenantId])
            SELECT NEWID(), 'Procurement receipt and AP restricted', 'Procurement', NULL, seed.PermissionKey,
                   seed.CanView, seed.CanUpload, seed.CanAnnotate, seed.CanApprove, 0, 1,
                   SYSUTCDATETIME(), 'INV-REQ-FU-001 migration', 0, tenant.Id
            FROM [dbo].[Tenants] tenant
            CROSS APPLY (VALUES
                ('procurement.inventory.read', 1,0,0,0),
                ('procurement.inventory.receive', 1,1,1,0),
                ('Finance.Read', 1,0,0,0),
                ('Finance.AP.Invoices.Create', 1,0,1,0),
                ('Finance.AP.Invoices.Manage', 1,0,1,0),
                ('Finance.AP.Invoices.Write', 1,0,1,0)
            ) seed(PermissionKey, CanView, CanUpload, CanAnnotate, CanApprove)
            WHERE tenant.IsDeleted = 0
              AND NOT EXISTS (SELECT 1 FROM [dbo].[CentralDocumentAccessRules] accessRule
                              WHERE accessRule.TenantId = tenant.Id
                                AND accessRule.AccessProfile = 'Procurement receipt and AP restricted'
                                AND accessRule.Module = 'Procurement'
                                AND accessRule.PermissionKey = seed.PermissionKey
                                AND accessRule.IsDeleted = 0);
            """);

        migrationBuilder.Sql(
            """
            CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementReceiptSourceEvidence_INVREQFU001]
            ON [dbo].[ProcurementReceiptSourceEvidence]
            AFTER INSERT, UPDATE, DELETE
            AS
            BEGIN
                SET NOCOUNT ON;

                IF EXISTS (
                    SELECT 1 FROM inserted i
                    JOIN [dbo].[ProcurementReceiptInspectionCases] c
                      ON c.TenantId=i.TenantId AND c.PurchaseOrderReceiptId=i.PurchaseOrderReceiptId
                     AND c.IsDeleted=0 AND c.Status NOT IN (0,3,10))
                    THROW 51941, 'RCV_SOURCE_EVIDENCE_LOCKED: receipt evidence is immutable after inspection submission.', 1;

                IF EXISTS (
                    SELECT 1 FROM deleted d
                    LEFT JOIN inserted i ON i.Id=d.Id
                    JOIN [dbo].[ProcurementReceiptInspectionCases] c
                      ON c.TenantId=d.TenantId AND c.PurchaseOrderReceiptId=d.PurchaseOrderReceiptId
                     AND c.IsDeleted=0 AND c.Status NOT IN (0,3,10)
                    WHERE i.Id IS NULL)
                    THROW 51941, 'RCV_SOURCE_EVIDENCE_LOCKED: receipt evidence is immutable after inspection submission.', 1;

                IF EXISTS (
                    SELECT 1 FROM deleted d
                    LEFT JOIN inserted i ON i.Id=d.Id
                    WHERE i.Id IS NULL)
                    THROW 51942, 'RCV_SOURCE_EVIDENCE_IMMUTABLE: receipt evidence is append-only and cannot be deleted.', 1;

                IF EXISTS (
                    SELECT 1 FROM inserted i
                    JOIN deleted d ON d.Id=i.Id
                    WHERE NOT (
                        d.IsCurrent=1 AND i.IsCurrent=0 AND i.IsDeleted=0
                        AND d.TenantId=i.TenantId
                        AND d.PurchaseOrderReceiptId=i.PurchaseOrderReceiptId
                        AND d.EvidenceKind=i.EvidenceKind
                        AND d.ReferenceNumber=i.ReferenceNumber
                        AND d.DocumentDate=i.DocumentDate
                        AND d.ClientRequestId=i.ClientRequestId
                        AND d.RequestHash=i.RequestHash
                        AND d.OriginalFileName=i.OriginalFileName
                        AND d.ContentType=i.ContentType
                        AND d.FileSize=i.FileSize
                        AND d.ChecksumSha256=i.ChecksumSha256
                        AND d.FileUploadRecordId=i.FileUploadRecordId
                        AND d.CentralDocumentRecordId=i.CentralDocumentRecordId
                        AND d.CentralDocumentVersionId=i.CentralDocumentVersionId
                        AND i.SupersededAtUtc IS NOT NULL AND i.SupersededByEvidenceId IS NOT NULL))
                    THROW 51942, 'RCV_SOURCE_EVIDENCE_IMMUTABLE: only append-only supersession is permitted.', 1;

                IF EXISTS (
                    SELECT 1 FROM inserted i
                    LEFT JOIN [dbo].[PurchaseOrderReceipts] receipt
                      ON receipt.Id=i.PurchaseOrderReceiptId AND receipt.TenantId=i.TenantId AND receipt.IsDeleted=0
                    LEFT JOIN [dbo].[FileUploadRecords] upload
                      ON upload.Id=i.FileUploadRecordId AND upload.TenantId=i.TenantId AND upload.IsDeleted=0
                    LEFT JOIN [dbo].[CentralDocumentRecords] record
                      ON record.Id=i.CentralDocumentRecordId AND record.TenantId=i.TenantId AND record.IsDeleted=0
                    LEFT JOIN [dbo].[CentralDocumentVersions] version
                      ON version.Id=i.CentralDocumentVersionId AND version.TenantId=i.TenantId AND version.IsDeleted=0
                    WHERE receipt.Id IS NULL OR upload.Id IS NULL OR upload.VirusScanStatus<>2
                       OR upload.Category<>'procurement-receipt-source-evidence' OR upload.FileSize<>i.FileSize
                       OR record.Id IS NULL OR record.SourceModule<>'Procurement'
                       OR record.SourceEntityType<>'ProcurementReceiptSourceEvidence' OR record.SourceRecordId<>i.Id
                       OR record.MetadataTemplateCode<>'TDC-PROC-RECEIPT-SOURCE'
                       OR record.AccessProfile<>'Procurement receipt and AP restricted'
                       OR record.LifecycleStatus<>'Active'
                       OR version.Id IS NULL OR version.DocumentRecordId<>record.Id
                       OR version.FileUploadRecordId<>upload.Id OR version.FileSize<>i.FileSize)
                    THROW 51943, 'RCV_SOURCE_EVIDENCE_DMS_INVALID: tenant, receipt, clean upload and central-DMS lineage must match.', 1;
            END;
            """);

        migrationBuilder.Sql(
            """
            CREATE OR ALTER TRIGGER [dbo].[TR_PurchaseOrderReceipts_INVREQFU001Waybill]
            ON [dbo].[PurchaseOrderReceipts]
            AFTER INSERT, UPDATE
            AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (
                    SELECT 1 FROM inserted receipt
                    WHERE receipt.IsDeleted=0 AND receipt.Status IN ('Accepted','Partially Accepted')
                      AND NOT EXISTS (
                          SELECT 1
                          FROM [dbo].[ProcurementReceiptSourceEvidence] evidence
                          JOIN [dbo].[FileUploadRecords] upload
                            ON upload.Id=evidence.FileUploadRecordId AND upload.TenantId=evidence.TenantId
                           AND upload.IsDeleted=0 AND upload.VirusScanStatus=2
                           AND upload.Category='procurement-receipt-source-evidence'
                          JOIN [dbo].[CentralDocumentRecords] record
                            ON record.Id=evidence.CentralDocumentRecordId AND record.TenantId=evidence.TenantId
                           AND record.IsDeleted=0 AND record.LifecycleStatus='Active'
                           AND record.SourceEntityType='ProcurementReceiptSourceEvidence'
                           AND record.SourceRecordId=evidence.Id
                           AND record.MetadataTemplateCode='TDC-PROC-RECEIPT-SOURCE'
                          JOIN [dbo].[CentralDocumentVersions] version
                            ON version.Id=evidence.CentralDocumentVersionId AND version.TenantId=evidence.TenantId
                           AND version.IsDeleted=0 AND version.DocumentRecordId=record.Id
                           AND version.FileUploadRecordId=upload.Id
                          WHERE evidence.TenantId=receipt.TenantId
                            AND evidence.PurchaseOrderReceiptId=receipt.Id
                            AND evidence.EvidenceKind=1 AND evidence.IsCurrent=1 AND evidence.IsDeleted=0))
                    THROW 51944, 'RCV_WAYBILL_REQUIRED: accepted goods receipt requires current clean governed Waybill evidence.', 1;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_PurchaseOrderReceipts_INVREQFU001Waybill];");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementReceiptSourceEvidence_INVREQFU001];");
        migrationBuilder.DropTable(name: "ProcurementReceiptSourceEvidence");
        migrationBuilder.Sql(
            """
            UPDATE [dbo].[CentralDocumentMetadataTemplates]
               SET [IsActive]=0, [UpdatedAt]=SYSUTCDATETIME(), [UpdatedBy]='INV-REQ-FU-001 rollback'
             WHERE [TemplateCode]='TDC-PROC-RECEIPT-SOURCE' AND [IsDeleted]=0;
            UPDATE [dbo].[CentralDocumentAccessRules]
               SET [IsActive]=0, [UpdatedAt]=SYSUTCDATETIME(), [UpdatedBy]='INV-REQ-FU-001 rollback'
             WHERE [AccessProfile]='Procurement receipt and AP restricted' AND [IsDeleted]=0;
            """);

        // This migration was exercised on shared test databases before the three
        // preceding procurement migrations were merged. Keep rollback compatible
        // with both the original and reconciled index shapes.
        migrationBuilder.Sql(
            """
            DROP INDEX IF EXISTS [IX_EmergencyProcurementPlans_ExceptionalSourcingTenderId]
                ON [dbo].[EmergencyProcurementPlans];
            DROP INDEX IF EXISTS [IX_EmergencyProcurementPlans_ExceptionRuleId]
                ON [dbo].[EmergencyProcurementPlans];
            DROP INDEX IF EXISTS [IX_EmergencyProcurementPlans_PurchaseRequisitionId]
                ON [dbo].[EmergencyProcurementPlans];
            DROP INDEX IF EXISTS [IX_EmergencyProcurementPlans_WorkflowInstanceId]
                ON [dbo].[EmergencyProcurementPlans];

            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE object_id = OBJECT_ID(N'[dbo].[EmergencyProcurementPlans]')
                  AND name = N'IX_EmergencyProcurementPlans_TenantId')
                CREATE INDEX [IX_EmergencyProcurementPlans_TenantId]
                    ON [dbo].[EmergencyProcurementPlans] ([TenantId]);
            """);
    }
}
