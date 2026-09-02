using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Reconciles the CIV-CFG-014 catalogue entry into existing editable Civil profiles.
/// Published profiles remain immutable; the runtime seeder creates an unapproved successor
/// draft when a later catalogue extension is discovered on an immutable family.
/// </summary>
public partial class ReconcileCivilEngineeringConfigurationDecisionCatalogue : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            SET NOCOUNT ON;
            SET QUOTED_IDENTIFIER ON;
            DECLARE @Now datetime2 = SYSUTCDATETIME();
            DECLARE @Changed TABLE (ProfileId uniqueidentifier NOT NULL PRIMARY KEY, TenantId uniqueidentifier NOT NULL);

            UPDATE decision
               SET decision.IsDeleted = 0,
                   decision.DeletedAt = NULL,
                   decision.DeletedBy = NULL,
                   decision.SchemaVersion = 1,
                   decision.OwnerGroup = 'Civil Engineering + QS + Finance + Procurement',
                   decision.Status = 0,
                   decision.ApprovalStatus = 0,
                   decision.EvidenceStatus = 0,
                   decision.ValueJson = '{}',
                   decision.DecisionDate = NULL,
                   decision.EffectiveFrom = NULL,
                   decision.EffectiveTo = NULL,
                   decision.ApprovedById = NULL,
                   decision.ApprovedAt = NULL,
                   decision.ApprovalWorkflowInstanceId = NULL,
                   decision.ApprovalReference = NULL,
                   decision.SourceDecisionId = NULL,
                   decision.SourceLineage = 'CIV-CFG-014 tenant catalogue reconciliation; unapproved draft',
                   decision.Notes = NULL,
                   decision.UpdatedAt = @Now,
                   decision.UpdatedBy = 'System'
            OUTPUT inserted.ProfileId, inserted.TenantId INTO @Changed(ProfileId, TenantId)
            FROM dbo.CivilEngineeringConfigurationDecisions decision
            JOIN dbo.CivilEngineeringConfigurationProfiles profile
              ON profile.Id = decision.ProfileId
             AND profile.TenantId = decision.TenantId
             AND profile.IsDeleted = 0
             AND profile.ProfileCode = 'TDC-CIVIL-ENGINEERING'
             AND profile.LifecycleStatus = 0
            WHERE decision.ConfigurationKey = 'CIV-CFG-014'
              AND decision.IsDeleted = 1;

            INSERT dbo.CivilEngineeringConfigurationDecisions
                (Id, ProfileId, ConfigurationKey, SchemaVersion, OwnerGroup, Status, ApprovalStatus,
                 EvidenceStatus, ValueJson, SourceLineage, CreatedAt, CreatedBy, IsDeleted, TenantId)
            OUTPUT inserted.ProfileId, inserted.TenantId INTO @Changed(ProfileId, TenantId)
            SELECT NEWID(), profile.Id, 'CIV-CFG-014', 1,
                   'Civil Engineering + QS + Finance + Procurement', 0, 0, 0, '{}',
                   'CIV-CFG-014 tenant catalogue reconciliation; unapproved draft',
                   @Now, 'System', 0, profile.TenantId
            FROM dbo.CivilEngineeringConfigurationProfiles profile
            WHERE profile.ProfileCode = 'TDC-CIVIL-ENGINEERING'
              AND profile.LifecycleStatus = 0
              AND profile.IsDeleted = 0
              AND NOT EXISTS (
                  SELECT 1
                  FROM dbo.CivilEngineeringConfigurationDecisions existing
                  WHERE existing.TenantId = profile.TenantId
                    AND existing.ProfileId = profile.Id
                    AND existing.ConfigurationKey = 'CIV-CFG-014');

            INSERT dbo.CivilEngineeringConfigurationRevisions
                (Id, ProfileId, Action, Result, CorrelationId, ActorUserId, ActorName, ActorRoles,
                 Reason, AfterJson, CreatedAt, CreatedBy, IsDeleted, TenantId)
            SELECT NEWID(), changed.ProfileId, 'SeedDraft', 'Succeeded',
                   CONCAT('civil-catalogue-014-', CONVERT(varchar(36), changed.TenantId)),
                   '00000000-0000-0000-0000-000000000000', 'System', 'System',
                   'Reconciled newly registered CIV-CFG-014 into the existing editable Civil configuration draft.',
                   '{"configurationKey":"CIV-CFG-014","lifecycleStatus":"draft","approvalStatus":"pending","evidenceStatus":"missing"}',
                   @Now, 'System', 0, changed.TenantId
            FROM @Changed changed
            WHERE NOT EXISTS (
                SELECT 1
                FROM dbo.CivilEngineeringConfigurationRevisions revision
                WHERE revision.TenantId = changed.TenantId
                  AND revision.ProfileId = changed.ProfileId
                  AND revision.CorrelationId = CONCAT('civil-catalogue-014-', CONVERT(varchar(36), changed.TenantId)));
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF EXISTS (
                SELECT 1
                FROM dbo.CivilEngineeringConfigurationDecisions decision
                WHERE decision.ConfigurationKey = 'CIV-CFG-014'
                  AND decision.SourceLineage = 'CIV-CFG-014 tenant catalogue reconciliation; unapproved draft'
                  AND (decision.Status <> 0 OR decision.ApprovalStatus <> 0 OR decision.EvidenceStatus <> 0 OR decision.ValueJson <> '{}'
                       OR EXISTS (SELECT 1 FROM dbo.CivilEngineeringConfigurationEvidenceLinks evidence WHERE evidence.DecisionId = decision.Id AND evidence.IsDeleted = 0)))
                THROW 52161, 'CIV-CFG-014 contains configured or evidenced data and cannot be removed by migration rollback.', 1;

            UPDATE decision
               SET decision.IsDeleted = 1,
                   decision.DeletedAt = SYSUTCDATETIME(),
                   decision.DeletedBy = 'Migration rollback'
            FROM dbo.CivilEngineeringConfigurationDecisions decision
            WHERE decision.ConfigurationKey = 'CIV-CFG-014'
              AND decision.SourceLineage = 'CIV-CFG-014 tenant catalogue reconciliation; unapproved draft'
              AND decision.Status = 0
              AND decision.ApprovalStatus = 0
              AND decision.EvidenceStatus = 0
              AND decision.ValueJson = '{}';
            """);
    }
}
