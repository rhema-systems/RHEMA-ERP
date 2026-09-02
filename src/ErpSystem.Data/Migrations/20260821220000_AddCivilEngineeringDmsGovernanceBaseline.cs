using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Repairs the initially published Civil Engineering template so it is backed by
/// the existing central DMS access and retention governance records.
/// </summary>
public partial class AddCivilEngineeringDmsGovernanceBaseline : Migration
{
    private const string TemplateCode = "TDC-CIV-ENGINEERING-FILE";
    private const string AccessProfile = "TDC-CIVIL-ENGINEERING-RESTRICTED";
    private const string RetentionPolicyCode = "TDC-CIVIL-ENGINEERING-RETENTION-7Y";
    private const string LegacyAccessProfile = "Civil Engineering restricted";
    private const string LegacyRetentionRule =
        "Minimum seven years or effective CIV-CFG-004 retention, whichever is longer";
    private const string MigrationActor = "CIV-0601 DMS governance migration";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql($"""
            UPDATE template
            SET template.AccessProfile = '{AccessProfile}',
                template.RetentionRule = '{RetentionPolicyCode}',
                template.UpdatedAt = SYSUTCDATETIME(),
                template.UpdatedBy = '{MigrationActor}'
            FROM CentralDocumentMetadataTemplates template
            WHERE template.TemplateCode = '{TemplateCode}'
              AND template.IsDeleted = 0
              AND template.AccessProfile = '{LegacyAccessProfile}'
              AND template.RetentionRule = '{LegacyRetentionRule}';
            """);

        migrationBuilder.Sql($"""
            INSERT INTO CentralDocumentAccessRules
                (Id, AccessProfile, Module, RoleName, PermissionKey,
                 CanView, CanUpload, CanAnnotate, CanApprove, CanArchive,
                 IsActive, CreatedAt, CreatedBy, IsDeleted, TenantId)
            SELECT NEWID(), '{AccessProfile}', 'Civil Engineering', NULL,
                   'civil-engineering.documents.manage',
                   1, 1, 1, 0, 0, 1, SYSUTCDATETIME(), '{MigrationActor}', 0, template.TenantId
            FROM (
                SELECT DISTINCT template.TenantId
                FROM CentralDocumentMetadataTemplates template
                WHERE template.TemplateCode = '{TemplateCode}'
                  AND template.IsDeleted = 0
                  AND template.AccessProfile = '{AccessProfile}'
            ) template
            WHERE NOT EXISTS (
                SELECT 1
                FROM CentralDocumentAccessRules accessRule
                WHERE accessRule.TenantId = template.TenantId
                  AND accessRule.AccessProfile = '{AccessProfile}'
                  AND accessRule.Module = 'Civil Engineering'
                  AND accessRule.PermissionKey = 'civil-engineering.documents.manage'
                  AND accessRule.IsDeleted = 0);
            """);

        migrationBuilder.Sql($"""
            INSERT INTO CentralDocumentAccessRules
                (Id, AccessProfile, Module, RoleName, PermissionKey,
                 CanView, CanUpload, CanAnnotate, CanApprove, CanArchive,
                 IsActive, CreatedAt, CreatedBy, IsDeleted, TenantId)
            SELECT NEWID(), '{AccessProfile}', 'Civil Engineering', NULL,
                   'civil-engineering.transactions.approve',
                   1, 0, 1, 1, 0, 1, SYSUTCDATETIME(), '{MigrationActor}', 0, template.TenantId
            FROM (
                SELECT DISTINCT template.TenantId
                FROM CentralDocumentMetadataTemplates template
                WHERE template.TemplateCode = '{TemplateCode}'
                  AND template.IsDeleted = 0
                  AND template.AccessProfile = '{AccessProfile}'
            ) template
            WHERE NOT EXISTS (
                SELECT 1
                FROM CentralDocumentAccessRules accessRule
                WHERE accessRule.TenantId = template.TenantId
                  AND accessRule.AccessProfile = '{AccessProfile}'
                  AND accessRule.Module = 'Civil Engineering'
                  AND accessRule.PermissionKey = 'civil-engineering.transactions.approve'
                  AND accessRule.IsDeleted = 0);
            """);

        migrationBuilder.Sql($"""
            INSERT INTO CentralDocumentRetentionPolicies
                (Id, PolicyCode, Name, Module, DocumentType, RetentionDays,
                 RequiresLegalHoldReview, AllowArchive, AllowDestruction,
                 IsActive, Notes, CreatedAt, CreatedBy, IsDeleted, TenantId)
            SELECT NEWID(), '{RetentionPolicyCode}', 'Civil Engineering engineering-file retention',
                   'Civil Engineering', 'Engineering file', 2555,
                   1, 1, 0, 1,
                   'Minimum seven-year retention; legal-hold review is required before disposal.',
                   SYSUTCDATETIME(), '{MigrationActor}', 0, template.TenantId
            FROM (
                SELECT DISTINCT template.TenantId
                FROM CentralDocumentMetadataTemplates template
                WHERE template.TemplateCode = '{TemplateCode}'
                  AND template.IsDeleted = 0
                  AND template.RetentionRule = '{RetentionPolicyCode}'
            ) template
            WHERE NOT EXISTS (
                SELECT 1
                FROM CentralDocumentRetentionPolicies policy
                WHERE policy.TenantId = template.TenantId
                  AND policy.PolicyCode = '{RetentionPolicyCode}'
                  AND policy.IsDeleted = 0);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql($"""
            DELETE accessRule
            FROM CentralDocumentAccessRules accessRule
            WHERE accessRule.CreatedBy = '{MigrationActor}'
              AND accessRule.AccessProfile = '{AccessProfile}'
              AND accessRule.Module = 'Civil Engineering'
              AND accessRule.PermissionKey IN
                  ('civil-engineering.documents.manage', 'civil-engineering.transactions.approve');

            DELETE policy
            FROM CentralDocumentRetentionPolicies policy
            WHERE policy.CreatedBy = '{MigrationActor}'
              AND policy.PolicyCode = '{RetentionPolicyCode}';

            UPDATE template
            SET template.AccessProfile = '{LegacyAccessProfile}',
                template.RetentionRule = '{LegacyRetentionRule}',
                template.UpdatedAt = SYSUTCDATETIME(),
                template.UpdatedBy = '{MigrationActor}'
            FROM CentralDocumentMetadataTemplates template
            WHERE template.TemplateCode = '{TemplateCode}'
              AND template.IsDeleted = 0
              AND template.AccessProfile = '{AccessProfile}'
              AND template.RetentionRule = '{RetentionPolicyCode}'
              AND template.UpdatedBy = '{MigrationActor}';
            """);
    }
}
