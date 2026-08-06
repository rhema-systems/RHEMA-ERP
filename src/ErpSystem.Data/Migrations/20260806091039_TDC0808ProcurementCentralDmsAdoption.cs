using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class TDC0808ProcurementCentralDmsAdoption : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TenderDocuments_TenantId",
                table: "TenderDocuments");

            migrationBuilder.DropIndex(
                name: "IX_TenderBidDocuments_TenantId",
                table: "TenderBidDocuments");

            migrationBuilder.DropIndex(
                name: "IX_TenderAwardVerificationItemDocuments_TenantId",
                table: "TenderAwardVerificationItemDocuments");

            migrationBuilder.AddColumn<Guid>(
                name: "CentralDocumentRecordId",
                table: "TenderDocuments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CentralDocumentVersionId",
                table: "TenderDocuments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FileUploadRecordId",
                table: "TenderDocuments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CentralDocumentRecordId",
                table: "TenderBidDocuments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CentralDocumentVersionId",
                table: "TenderBidDocuments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FileUploadRecordId",
                table: "TenderBidDocuments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CentralDocumentRecordId",
                table: "TenderAwardVerificationItemDocuments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CentralDocumentVersionId",
                table: "TenderAwardVerificationItemDocuments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FileUploadRecordId",
                table: "TenderAwardVerificationItemDocuments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenderDocuments_CentralDocumentRecordId",
                table: "TenderDocuments",
                column: "CentralDocumentRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderDocuments_CentralDocumentVersionId",
                table: "TenderDocuments",
                column: "CentralDocumentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderDocuments_FileUploadRecordId",
                table: "TenderDocuments",
                column: "FileUploadRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderDocuments_TenantId_CentralDocumentRecordId",
                table: "TenderDocuments",
                columns: new[] { "TenantId", "CentralDocumentRecordId" },
                unique: true,
                filter: "[CentralDocumentRecordId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TenderDocuments_TenantId_CentralDocumentVersionId",
                table: "TenderDocuments",
                columns: new[] { "TenantId", "CentralDocumentVersionId" },
                unique: true,
                filter: "[CentralDocumentVersionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TenderDocuments_TenantId_FileUploadRecordId",
                table: "TenderDocuments",
                columns: new[] { "TenantId", "FileUploadRecordId" },
                filter: "[FileUploadRecordId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TenderBidDocuments_CentralDocumentRecordId",
                table: "TenderBidDocuments",
                column: "CentralDocumentRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderBidDocuments_CentralDocumentVersionId",
                table: "TenderBidDocuments",
                column: "CentralDocumentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderBidDocuments_FileUploadRecordId",
                table: "TenderBidDocuments",
                column: "FileUploadRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderBidDocuments_TenantId_CentralDocumentRecordId",
                table: "TenderBidDocuments",
                columns: new[] { "TenantId", "CentralDocumentRecordId" },
                unique: true,
                filter: "[CentralDocumentRecordId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TenderBidDocuments_TenantId_CentralDocumentVersionId",
                table: "TenderBidDocuments",
                columns: new[] { "TenantId", "CentralDocumentVersionId" },
                unique: true,
                filter: "[CentralDocumentVersionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TenderBidDocuments_TenantId_FileUploadRecordId",
                table: "TenderBidDocuments",
                columns: new[] { "TenantId", "FileUploadRecordId" },
                filter: "[FileUploadRecordId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TenderAwardVerificationItemDocuments_CentralDocumentRecordId",
                table: "TenderAwardVerificationItemDocuments",
                column: "CentralDocumentRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderAwardVerificationItemDocuments_CentralDocumentVersionId",
                table: "TenderAwardVerificationItemDocuments",
                column: "CentralDocumentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderAwardVerificationItemDocuments_FileUploadRecordId",
                table: "TenderAwardVerificationItemDocuments",
                column: "FileUploadRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderAwardVerificationItemDocuments_TenantId_CentralDocumentRecordId",
                table: "TenderAwardVerificationItemDocuments",
                columns: new[] { "TenantId", "CentralDocumentRecordId" },
                unique: true,
                filter: "[CentralDocumentRecordId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TenderAwardVerificationItemDocuments_TenantId_CentralDocumentVersionId",
                table: "TenderAwardVerificationItemDocuments",
                columns: new[] { "TenantId", "CentralDocumentVersionId" },
                unique: true,
                filter: "[CentralDocumentVersionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TenderAwardVerificationItemDocuments_TenantId_FileUploadRecordId",
                table: "TenderAwardVerificationItemDocuments",
                columns: new[] { "TenantId", "FileUploadRecordId" },
                filter: "[FileUploadRecordId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_TenderAwardVerificationItemDocuments_CentralDocumentRecords_CentralDocumentRecordId",
                table: "TenderAwardVerificationItemDocuments",
                column: "CentralDocumentRecordId",
                principalTable: "CentralDocumentRecords",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderAwardVerificationItemDocuments_CentralDocumentVersions_CentralDocumentVersionId",
                table: "TenderAwardVerificationItemDocuments",
                column: "CentralDocumentVersionId",
                principalTable: "CentralDocumentVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderAwardVerificationItemDocuments_FileUploadRecords_FileUploadRecordId",
                table: "TenderAwardVerificationItemDocuments",
                column: "FileUploadRecordId",
                principalTable: "FileUploadRecords",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderBidDocuments_CentralDocumentRecords_CentralDocumentRecordId",
                table: "TenderBidDocuments",
                column: "CentralDocumentRecordId",
                principalTable: "CentralDocumentRecords",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderBidDocuments_CentralDocumentVersions_CentralDocumentVersionId",
                table: "TenderBidDocuments",
                column: "CentralDocumentVersionId",
                principalTable: "CentralDocumentVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderBidDocuments_FileUploadRecords_FileUploadRecordId",
                table: "TenderBidDocuments",
                column: "FileUploadRecordId",
                principalTable: "FileUploadRecords",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderDocuments_CentralDocumentRecords_CentralDocumentRecordId",
                table: "TenderDocuments",
                column: "CentralDocumentRecordId",
                principalTable: "CentralDocumentRecords",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderDocuments_CentralDocumentVersions_CentralDocumentVersionId",
                table: "TenderDocuments",
                column: "CentralDocumentVersionId",
                principalTable: "CentralDocumentVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TenderDocuments_FileUploadRecords_FileUploadRecordId",
                table: "TenderDocuments",
                column: "FileUploadRecordId",
                principalTable: "FileUploadRecords",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(
                """
                INSERT INTO [dbo].[CentralDocumentMetadataTemplates]
                    ([Id], [Module], [DocumentType], [TemplateCode], [SourceLabel],
                     [RequiredFieldsJson], [RelationshipsJson], [RetentionRule], [AccessProfile],
                     [IsActive], [PublishedAt], [CreatedAt], [CreatedBy], [IsDeleted], [TenantId])
                SELECT NEWID(), 'Procurement', seed.DocumentType, seed.TemplateCode, seed.SourceLabel,
                       '["sourceReference","documentFamily","classification","sourceStatus","uploadedBy","checksumSha256"]',
                       seed.RelationshipsJson, 'TDC-PROC-RET-7Y', seed.AccessProfile,
                       1, SYSUTCDATETIME(), SYSUTCDATETIME(), 'TDC-0808 migration', 0, tenant.Id
                FROM [dbo].[Tenants] tenant
                CROSS APPLY (VALUES
                    ('TDC-PROC-TENDER',     'TenderDocument',              'Procurement tender evidence',     '["Tender","TenderBid"]',                                      'Procurement tender restricted'),
                    ('TDC-PROC-EVALUATION', 'TenderEvaluationEvidence',    'Procurement evaluation evidence', '["TenderEvaluation","TenderBid","Tender"]',                   'Procurement evaluation restricted'),
                    ('TDC-PROC-APPROVAL',   'ProcurementApprovalEvidence', 'Procurement approval evidence',   '["WorkflowInstance","TenderAwardVerificationItemResult"]',     'Procurement approval restricted'),
                    ('PROC-SUP-EVD',        'SupplierEvidence',            'Supplier registration evidence',  '["BusinessPartnerRegistration","BusinessPartner"]',            'Procurement supplier restricted'),
                    ('PROC-CON-EVD',        'ContractEvidence',            'Procurement contract evidence',   '["Contract","TenderAward","BusinessPartner"]',                'Procurement contract restricted'),
                    ('TDC-PROC-GRN-EVD',    'GoodsReceiptEvidence',        'Goods receipt evidence',          '["GoodsReceiptNote","PurchaseOrder","Inspection"]',           'Procurement receipt restricted'),
                    ('TDC-PROC-MRN-EVD',    'MaterialReceiptEvidence',     'Material receipt evidence',       '["ProcurementReceiptDocument","PurchaseOrder","Inspection"]', 'Procurement receipt restricted'),
                    ('TDC-PROC-INVOICE',    'VendorInvoiceEvidence',       'Vendor invoice evidence',         '["VendorInvoice","PurchaseOrder","GoodsReceiptNote"]',        'Procurement invoice restricted'),
                    ('TDC-PROC-DISPOSAL',   'InventoryDisposalEvidence',   'Inventory disposal evidence',     '["InventoryDisposalCase","InventoryItem","Warehouse"]',       'Inventory disposal restricted')
                ) seed(TemplateCode, DocumentType, SourceLabel, RelationshipsJson, AccessProfile)
                WHERE tenant.IsDeleted = 0
                  AND NOT EXISTS (
                      SELECT 1 FROM [dbo].[CentralDocumentMetadataTemplates] template
                      WHERE template.TenantId = tenant.Id
                        AND template.TemplateCode = seed.TemplateCode);

                INSERT INTO [dbo].[CentralDocumentRetentionPolicies]
                    ([Id], [PolicyCode], [Name], [Module], [DocumentType], [RetentionDays],
                     [RequiresLegalHoldReview], [AllowArchive], [AllowDestruction], [IsActive], [Notes],
                     [CreatedAt], [CreatedBy], [IsDeleted], [TenantId])
                SELECT NEWID(), 'TDC-PROC-RET-7Y', 'Procurement controlled-document retention',
                       'Procurement', NULL, 2555, 1, 1, 0, 1,
                       'Seven-year minimum retention with mandatory legal-hold review and no direct destruction.',
                       SYSUTCDATETIME(), 'TDC-0808 migration', 0, tenant.Id
                FROM [dbo].[Tenants] tenant
                WHERE tenant.IsDeleted = 0
                  AND NOT EXISTS (
                      SELECT 1 FROM [dbo].[CentralDocumentRetentionPolicies] policy
                      WHERE policy.TenantId = tenant.Id
                        AND policy.PolicyCode = 'TDC-PROC-RET-7Y');

                INSERT INTO [dbo].[CentralDocumentAccessRules]
                    ([Id], [AccessProfile], [Module], [RoleName], [PermissionKey],
                     [CanView], [CanUpload], [CanAnnotate], [CanApprove], [CanArchive], [IsActive],
                     [CreatedAt], [CreatedBy], [IsDeleted], [TenantId])
                SELECT NEWID(), seed.AccessProfile, 'Procurement', NULL, seed.PermissionKey,
                       seed.CanView, seed.CanUpload, seed.CanAnnotate, seed.CanApprove, seed.CanArchive, 1,
                       SYSUTCDATETIME(), 'TDC-0808 migration', 0, tenant.Id
                FROM [dbo].[Tenants] tenant
                CROSS APPLY (VALUES
                    ('Procurement tender restricted',     'procurement.records.read',                 1,0,0,0,0),
                    ('Procurement tender restricted',     'procurement.sourcing.manage',              1,1,1,0,0),
                    ('Procurement tender restricted',     'procurement.tender.administer',            1,1,1,1,1),
                    ('Procurement evaluation restricted', 'procurement.records.read',                 1,0,0,0,0),
                    ('Procurement evaluation restricted', 'procurement.tender.evaluate',              1,1,1,1,0),
                    ('Procurement evaluation restricted', 'procurement.tender.administer',            1,1,1,1,1),
                    ('Procurement approval restricted',   'procurement.records.read',                 1,0,0,0,0),
                    ('Procurement approval restricted',   'procurement.sourcing.approve',             1,1,1,1,1),
                    ('Procurement approval restricted',   'procurement.tender.approve',               1,1,1,1,1),
                    ('Procurement approval restricted',   'procurement.purchase-order.approve',       1,1,1,1,1),
                    ('Procurement approval restricted',   'procurement.contract.approve',             1,1,1,1,1),
                    ('Procurement supplier restricted',   'procurement.records.read',                 1,0,0,0,0),
                    ('Procurement supplier restricted',   'procurement.supplier.manage',              1,1,1,0,0),
                    ('Procurement supplier restricted',   'procurement.supplier.review',              1,1,1,1,1),
                    ('Procurement contract restricted',   'procurement.records.read',                 1,0,0,0,0),
                    ('Procurement contract restricted',   'procurement.contract.manage',              1,1,1,0,0),
                    ('Procurement contract restricted',   'procurement.contract.approve',             1,0,0,1,1),
                    ('Procurement receipt restricted',    'procurement.inventory.read',               1,0,0,0,0),
                    ('Procurement receipt restricted',    'procurement.inventory.receive',            1,1,1,1,0),
                    ('Procurement invoice restricted',    'Finance.Read',                             1,0,0,0,0),
                    ('Procurement invoice restricted',    'Finance.AP.Invoices.Create',               1,1,1,0,0),
                    ('Procurement invoice restricted',    'Finance.AP.Invoices.Manage',               1,1,1,1,1),
                    ('Procurement invoice restricted',    'Finance.AP.Invoices.Write',                1,1,1,0,0),
                    ('Inventory disposal restricted',     'procurement.inventory.read',               1,0,0,0,0),
                    ('Inventory disposal restricted',     'procurement.inventory.disposal.request',   1,1,1,0,0),
                    ('Inventory disposal restricted',     'procurement.inventory.disposal.approve',   1,1,1,1,1)
                ) seed(AccessProfile, PermissionKey, CanView, CanUpload, CanAnnotate, CanApprove, CanArchive)
                WHERE tenant.IsDeleted = 0
                  AND NOT EXISTS (
                      SELECT 1 FROM [dbo].[CentralDocumentAccessRules] accessRule
                      WHERE accessRule.TenantId = tenant.Id
                        AND accessRule.AccessProfile = seed.AccessProfile
                        AND accessRule.Module = 'Procurement'
                        AND accessRule.PermissionKey = seed.PermissionKey
                        AND accessRule.IsDeleted = 0);
                """);

            CreateOrAlterTrigger(migrationBuilder,
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_TenderDocuments_TDC0808DmsLineage]
                ON [dbo].[TenderDocuments]
                AFTER INSERT, UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        WHERE (i.FileUploadRecordId IS NOT NULL OR i.CentralDocumentRecordId IS NOT NULL OR i.CentralDocumentVersionId IS NOT NULL)
                          AND (i.FileUploadRecordId IS NULL OR i.CentralDocumentRecordId IS NULL OR i.CentralDocumentVersionId IS NULL))
                        THROW 51880, 'PROC_DMS_LINEAGE_INCOMPLETE: tender document DMS identifiers must be supplied together.', 1;
                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        LEFT JOIN FileUploadRecords upload ON upload.Id = i.FileUploadRecordId AND upload.TenantId = i.TenantId AND upload.IsDeleted = 0
                        LEFT JOIN CentralDocumentRecords record ON record.Id = i.CentralDocumentRecordId AND record.TenantId = i.TenantId AND record.IsDeleted = 0
                        LEFT JOIN CentralDocumentVersions version ON version.Id = i.CentralDocumentVersionId AND version.TenantId = i.TenantId AND version.IsDeleted = 0
                        WHERE i.CentralDocumentRecordId IS NOT NULL
                          AND (upload.Id IS NULL OR upload.VirusScanStatus <> 2 OR record.Id IS NULL OR version.Id IS NULL
                               OR version.DocumentRecordId <> record.Id OR version.FileUploadRecordId <> upload.Id
                               OR record.SourceModule <> 'Procurement' OR record.SourceRecordId <> i.TenderId
                               OR record.MetadataTemplateCode <> 'TDC-PROC-TENDER'))
                        THROW 51881, 'PROC_DMS_LINEAGE_INVALID: tender document DMS lineage must match tenant, source, record, version, and clean-upload registration.', 1;
                END;
                """);

            CreateOrAlterTrigger(migrationBuilder,
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_TenderBidDocuments_TDC0808DmsLineage]
                ON [dbo].[TenderBidDocuments]
                AFTER INSERT, UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        WHERE (i.FileUploadRecordId IS NOT NULL OR i.CentralDocumentRecordId IS NOT NULL OR i.CentralDocumentVersionId IS NOT NULL)
                          AND (i.FileUploadRecordId IS NULL OR i.CentralDocumentRecordId IS NULL OR i.CentralDocumentVersionId IS NULL))
                        THROW 51882, 'PROC_DMS_LINEAGE_INCOMPLETE: tender-bid document DMS identifiers must be supplied together.', 1;
                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        LEFT JOIN FileUploadRecords upload ON upload.Id = i.FileUploadRecordId AND upload.TenantId = i.TenantId AND upload.IsDeleted = 0
                        LEFT JOIN CentralDocumentRecords record ON record.Id = i.CentralDocumentRecordId AND record.TenantId = i.TenantId AND record.IsDeleted = 0
                        LEFT JOIN CentralDocumentVersions version ON version.Id = i.CentralDocumentVersionId AND version.TenantId = i.TenantId AND version.IsDeleted = 0
                        WHERE i.CentralDocumentRecordId IS NOT NULL
                          AND (upload.Id IS NULL OR upload.VirusScanStatus <> 2 OR record.Id IS NULL OR version.Id IS NULL
                               OR version.DocumentRecordId <> record.Id OR version.FileUploadRecordId <> upload.Id
                               OR record.SourceModule <> 'Procurement' OR record.SourceRecordId <> i.TenderBidId
                               OR record.MetadataTemplateCode <> 'TDC-PROC-TENDER'))
                        THROW 51883, 'PROC_DMS_LINEAGE_INVALID: tender-bid document DMS lineage must match tenant, source, record, version, and clean-upload registration.', 1;
                END;
                """);

            CreateOrAlterTrigger(migrationBuilder,
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_TenderAwardVerificationItemDocuments_TDC0808DmsLineage]
                ON [dbo].[TenderAwardVerificationItemDocuments]
                AFTER INSERT, UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        WHERE (i.FileUploadRecordId IS NOT NULL OR i.CentralDocumentRecordId IS NOT NULL OR i.CentralDocumentVersionId IS NOT NULL)
                          AND (i.FileUploadRecordId IS NULL OR i.CentralDocumentRecordId IS NULL OR i.CentralDocumentVersionId IS NULL))
                        THROW 51884, 'PROC_DMS_LINEAGE_INCOMPLETE: award-verification document DMS identifiers must be supplied together.', 1;
                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        LEFT JOIN FileUploadRecords upload ON upload.Id = i.FileUploadRecordId AND upload.TenantId = i.TenantId AND upload.IsDeleted = 0
                        LEFT JOIN CentralDocumentRecords record ON record.Id = i.CentralDocumentRecordId AND record.TenantId = i.TenantId AND record.IsDeleted = 0
                        LEFT JOIN CentralDocumentVersions version ON version.Id = i.CentralDocumentVersionId AND version.TenantId = i.TenantId AND version.IsDeleted = 0
                        WHERE i.CentralDocumentRecordId IS NOT NULL
                          AND (upload.Id IS NULL OR upload.VirusScanStatus <> 2 OR record.Id IS NULL OR version.Id IS NULL
                               OR version.DocumentRecordId <> record.Id OR version.FileUploadRecordId <> upload.Id
                               OR record.SourceModule <> 'Procurement' OR record.SourceRecordId <> i.ItemResultId
                               OR record.MetadataTemplateCode <> 'TDC-PROC-APPROVAL'))
                        THROW 51885, 'PROC_DMS_LINEAGE_INVALID: award-verification document DMS lineage must match tenant, source, record, version, and clean-upload registration.', 1;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS [dbo].[TR_TenderAwardVerificationItemDocuments_TDC0808DmsLineage];
                DROP TRIGGER IF EXISTS [dbo].[TR_TenderBidDocuments_TDC0808DmsLineage];
                DROP TRIGGER IF EXISTS [dbo].[TR_TenderDocuments_TDC0808DmsLineage];

                DELETE accessRule
                FROM [dbo].[CentralDocumentAccessRules] accessRule
                WHERE accessRule.CreatedBy = 'TDC-0808 migration'
                  AND NOT EXISTS (
                      SELECT 1 FROM [dbo].[CentralDocumentRecords] record
                      WHERE record.TenantId = accessRule.TenantId
                        AND record.AccessProfile = accessRule.AccessProfile
                        AND record.IsDeleted = 0);

                DELETE policy
                FROM [dbo].[CentralDocumentRetentionPolicies] policy
                WHERE policy.PolicyCode = 'TDC-PROC-RET-7Y'
                  AND policy.CreatedBy = 'TDC-0808 migration'
                  AND NOT EXISTS (
                      SELECT 1 FROM [dbo].[CentralDocumentRecords] record
                      WHERE record.TenantId = policy.TenantId
                        AND record.SourceModule = 'Procurement'
                        AND record.IsDeleted = 0);

                DELETE template
                FROM [dbo].[CentralDocumentMetadataTemplates] template
                WHERE template.CreatedBy = 'TDC-0808 migration'
                  AND NOT EXISTS (
                      SELECT 1 FROM [dbo].[CentralDocumentRecords] record
                      WHERE record.TenantId = template.TenantId
                        AND record.MetadataTemplateCode = template.TemplateCode
                        AND record.IsDeleted = 0);
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_TenderAwardVerificationItemDocuments_CentralDocumentRecords_CentralDocumentRecordId",
                table: "TenderAwardVerificationItemDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderAwardVerificationItemDocuments_CentralDocumentVersions_CentralDocumentVersionId",
                table: "TenderAwardVerificationItemDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderAwardVerificationItemDocuments_FileUploadRecords_FileUploadRecordId",
                table: "TenderAwardVerificationItemDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderBidDocuments_CentralDocumentRecords_CentralDocumentRecordId",
                table: "TenderBidDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderBidDocuments_CentralDocumentVersions_CentralDocumentVersionId",
                table: "TenderBidDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderBidDocuments_FileUploadRecords_FileUploadRecordId",
                table: "TenderBidDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderDocuments_CentralDocumentRecords_CentralDocumentRecordId",
                table: "TenderDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderDocuments_CentralDocumentVersions_CentralDocumentVersionId",
                table: "TenderDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_TenderDocuments_FileUploadRecords_FileUploadRecordId",
                table: "TenderDocuments");

            migrationBuilder.DropIndex(
                name: "IX_TenderDocuments_CentralDocumentRecordId",
                table: "TenderDocuments");

            migrationBuilder.DropIndex(
                name: "IX_TenderDocuments_CentralDocumentVersionId",
                table: "TenderDocuments");

            migrationBuilder.DropIndex(
                name: "IX_TenderDocuments_FileUploadRecordId",
                table: "TenderDocuments");

            migrationBuilder.DropIndex(
                name: "IX_TenderDocuments_TenantId_CentralDocumentRecordId",
                table: "TenderDocuments");

            migrationBuilder.DropIndex(
                name: "IX_TenderDocuments_TenantId_CentralDocumentVersionId",
                table: "TenderDocuments");

            migrationBuilder.DropIndex(
                name: "IX_TenderDocuments_TenantId_FileUploadRecordId",
                table: "TenderDocuments");

            migrationBuilder.DropIndex(
                name: "IX_TenderBidDocuments_CentralDocumentRecordId",
                table: "TenderBidDocuments");

            migrationBuilder.DropIndex(
                name: "IX_TenderBidDocuments_CentralDocumentVersionId",
                table: "TenderBidDocuments");

            migrationBuilder.DropIndex(
                name: "IX_TenderBidDocuments_FileUploadRecordId",
                table: "TenderBidDocuments");

            migrationBuilder.DropIndex(
                name: "IX_TenderBidDocuments_TenantId_CentralDocumentRecordId",
                table: "TenderBidDocuments");

            migrationBuilder.DropIndex(
                name: "IX_TenderBidDocuments_TenantId_CentralDocumentVersionId",
                table: "TenderBidDocuments");

            migrationBuilder.DropIndex(
                name: "IX_TenderBidDocuments_TenantId_FileUploadRecordId",
                table: "TenderBidDocuments");

            migrationBuilder.DropIndex(
                name: "IX_TenderAwardVerificationItemDocuments_CentralDocumentRecordId",
                table: "TenderAwardVerificationItemDocuments");

            migrationBuilder.DropIndex(
                name: "IX_TenderAwardVerificationItemDocuments_CentralDocumentVersionId",
                table: "TenderAwardVerificationItemDocuments");

            migrationBuilder.DropIndex(
                name: "IX_TenderAwardVerificationItemDocuments_FileUploadRecordId",
                table: "TenderAwardVerificationItemDocuments");

            migrationBuilder.DropIndex(
                name: "IX_TenderAwardVerificationItemDocuments_TenantId_CentralDocumentRecordId",
                table: "TenderAwardVerificationItemDocuments");

            migrationBuilder.DropIndex(
                name: "IX_TenderAwardVerificationItemDocuments_TenantId_CentralDocumentVersionId",
                table: "TenderAwardVerificationItemDocuments");

            migrationBuilder.DropIndex(
                name: "IX_TenderAwardVerificationItemDocuments_TenantId_FileUploadRecordId",
                table: "TenderAwardVerificationItemDocuments");

            migrationBuilder.DropColumn(
                name: "CentralDocumentRecordId",
                table: "TenderDocuments");

            migrationBuilder.DropColumn(
                name: "CentralDocumentVersionId",
                table: "TenderDocuments");

            migrationBuilder.DropColumn(
                name: "FileUploadRecordId",
                table: "TenderDocuments");

            migrationBuilder.DropColumn(
                name: "CentralDocumentRecordId",
                table: "TenderBidDocuments");

            migrationBuilder.DropColumn(
                name: "CentralDocumentVersionId",
                table: "TenderBidDocuments");

            migrationBuilder.DropColumn(
                name: "FileUploadRecordId",
                table: "TenderBidDocuments");

            migrationBuilder.DropColumn(
                name: "CentralDocumentRecordId",
                table: "TenderAwardVerificationItemDocuments");

            migrationBuilder.DropColumn(
                name: "CentralDocumentVersionId",
                table: "TenderAwardVerificationItemDocuments");

            migrationBuilder.DropColumn(
                name: "FileUploadRecordId",
                table: "TenderAwardVerificationItemDocuments");

            migrationBuilder.CreateIndex(
                name: "IX_TenderDocuments_TenantId",
                table: "TenderDocuments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderBidDocuments_TenantId",
                table: "TenderBidDocuments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderAwardVerificationItemDocuments_TenantId",
                table: "TenderAwardVerificationItemDocuments",
                column: "TenantId");
        }

        private static void CreateOrAlterTrigger(MigrationBuilder migrationBuilder, string triggerSql)
        {
            // CREATE OR ALTER TRIGGER must be the first statement in its SQL
            // batch. Dynamic execution keeps EF idempotent scripts valid when
            // they wrap each migration command in a history guard.
            migrationBuilder.Sql($"EXEC(N'{triggerSql.Replace("'", "''", StringComparison.Ordinal)}')");
        }
    }
}
