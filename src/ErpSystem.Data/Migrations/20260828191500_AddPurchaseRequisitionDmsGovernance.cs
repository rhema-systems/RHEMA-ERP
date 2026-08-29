using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260828191500_AddPurchaseRequisitionDmsGovernance")]
public partial class AddPurchaseRequisitionDmsGovernance : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            INSERT INTO [dbo].[CentralDocumentMetadataTemplates]
                ([Id], [Module], [DocumentType], [TemplateCode], [SourceLabel],
                 [RequiredFieldsJson], [RelationshipsJson], [RetentionRule], [AccessProfile],
                 [IsActive], [PublishedAt], [CreatedAt], [CreatedBy], [IsDeleted], [TenantId])
            SELECT NEWID(), 'Procurement', 'PurchaseRequisitionEvidence', 'TDC-PROC-REQUISITION',
                   'Purchase requisition supporting documents',
                   '["sourceReference","documentFamily","classification","sourceStatus","uploadedBy","checksumSha256"]',
                   '["PurchaseRequisition"]', 'TDC-PROC-RET-7Y', 'Procurement requisition restricted',
                   1, SYSUTCDATETIME(), SYSUTCDATETIME(), 'FR-PR-001 migration', 0, tenant.Id
            FROM [dbo].[Tenants] tenant
            WHERE tenant.IsDeleted = 0
              AND NOT EXISTS (
                  SELECT 1 FROM [dbo].[CentralDocumentMetadataTemplates] template
                  WHERE template.TenantId = tenant.Id
                    AND template.TemplateCode = 'TDC-PROC-REQUISITION');

            INSERT INTO [dbo].[CentralDocumentRetentionPolicies]
                ([Id], [PolicyCode], [Name], [Module], [DocumentType], [RetentionDays],
                 [RequiresLegalHoldReview], [AllowArchive], [AllowDestruction], [IsActive], [Notes],
                 [CreatedAt], [CreatedBy], [IsDeleted], [TenantId])
            SELECT NEWID(), 'TDC-PROC-RET-7Y', 'Procurement controlled-document retention',
                   'Procurement', NULL, 2555, 1, 1, 0, 1,
                   'Seven-year minimum retention with mandatory legal-hold review and no direct destruction.',
                   SYSUTCDATETIME(), 'FR-PR-001 migration', 0, tenant.Id
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
            SELECT NEWID(), 'Procurement requisition restricted', 'Procurement', NULL,
                   seed.PermissionKey, 1, seed.CanUpload, seed.CanAnnotate, 0, seed.CanArchive, 1,
                   SYSUTCDATETIME(), 'FR-PR-001 migration', 0, tenant.Id
            FROM [dbo].[Tenants] tenant
            CROSS APPLY (VALUES
                ('procurement.records.read',       0,0,0),
                ('procurement.requisition.create', 1,1,1)
            ) seed(PermissionKey, CanUpload, CanAnnotate, CanArchive)
            WHERE tenant.IsDeleted = 0
              AND NOT EXISTS (
                  SELECT 1 FROM [dbo].[CentralDocumentAccessRules] accessRule
                  WHERE accessRule.TenantId = tenant.Id
                    AND accessRule.AccessProfile = 'Procurement requisition restricted'
                    AND accessRule.Module = 'Procurement'
                    AND accessRule.PermissionKey = seed.PermissionKey
                    AND accessRule.IsDeleted = 0);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DELETE accessRule
            FROM [dbo].[CentralDocumentAccessRules] accessRule
            WHERE accessRule.CreatedBy = 'FR-PR-001 migration'
              AND accessRule.AccessProfile = 'Procurement requisition restricted'
              AND NOT EXISTS (
                  SELECT 1 FROM [dbo].[CentralDocumentRecords] record
                  WHERE record.TenantId = accessRule.TenantId
                    AND record.AccessProfile = accessRule.AccessProfile
                    AND record.IsDeleted = 0);

            DELETE template
            FROM [dbo].[CentralDocumentMetadataTemplates] template
            WHERE template.CreatedBy = 'FR-PR-001 migration'
              AND template.TemplateCode = 'TDC-PROC-REQUISITION'
              AND NOT EXISTS (
                  SELECT 1 FROM [dbo].[CentralDocumentRecords] record
                  WHERE record.TenantId = template.TenantId
                    AND record.MetadataTemplateCode = template.TemplateCode
                    AND record.IsDeleted = 0);
            """);
    }
}
