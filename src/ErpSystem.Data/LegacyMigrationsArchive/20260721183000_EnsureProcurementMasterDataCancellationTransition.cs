using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260721183000_EnsureProcurementMasterDataCancellationTransition")]
public partial class EnsureProcurementMasterDataCancellationTransition : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_ProcurementConfigurationProfiles_TenantId_ProfileKey_LifecycleStatus",
            table: "ProcurementConfigurationProfiles");
        migrationBuilder.CreateIndex(
            name: "IX_ProcurementConfigurationProfiles_TenantId_ProfileKey_LifecycleStatus",
            table: "ProcurementConfigurationProfiles",
            columns: new[] { "TenantId", "ProfileKey", "LifecycleStatus" });

        migrationBuilder.DropIndex(
            name: "IX_ProcurementPolicySets_TenantId_PolicyKey_LifecycleStatus",
            table: "ProcurementPolicySets");
        migrationBuilder.CreateIndex(
            name: "IX_ProcurementPolicySets_TenantId_PolicyKey_LifecycleStatus",
            table: "ProcurementPolicySets",
            columns: new[] { "TenantId", "PolicyKey", "LifecycleStatus" });

        migrationBuilder.DropIndex(
            name: "IX_ProcurementMasterDataControlPolicies_TenantId_ResourceType_Status",
            table: "ProcurementMasterDataControlPolicies");
        migrationBuilder.CreateIndex(
            name: "IX_ProcurementMasterDataControlPolicies_TenantId_ResourceType_Status",
            table: "ProcurementMasterDataControlPolicies",
            columns: new[] { "TenantId", "ResourceType", "Status" });

        migrationBuilder.Sql(
            """
            CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementMasterDataChangeRequests_SnapshotGuard]
            ON [dbo].[ProcurementMasterDataChangeRequests]
            AFTER UPDATE
            AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (
                    SELECT 1 FROM deleted d JOIN inserted i ON i.Id = d.Id
                    WHERE d.Status <> 0 AND (
                        i.PolicyId <> d.PolicyId OR i.PolicyVersion <> d.PolicyVersion OR i.ResourceType <> d.ResourceType OR
                        i.TargetKind <> d.TargetKind OR i.TargetId <> d.TargetId OR i.TargetReference <> d.TargetReference OR
                        i.BeforeJson <> d.BeforeJson OR i.BeforeHash <> d.BeforeHash OR
                        i.ProposedChangesJson <> d.ProposedChangesJson OR i.ProposedChangesHash <> d.ProposedChangesHash OR
                        i.Reason <> d.Reason OR i.EffectiveAtUtc <> d.EffectiveAtUtc OR i.MakerUserId <> d.MakerUserId OR
                        ISNULL(i.WorkflowDefinitionId, '00000000-0000-0000-0000-000000000000') <> ISNULL(d.WorkflowDefinitionId, '00000000-0000-0000-0000-000000000000') OR
                        i.CorrelationId <> d.CorrelationId OR i.TenantId <> d.TenantId))
                    THROW 51014, 'Submitted procurement master-data before/proposed snapshots and control lineage are immutable.', 1;

                IF EXISTS (
                    SELECT 1 FROM deleted d JOIN inserted i ON i.Id = d.Id
                    WHERE i.Status <> d.Status AND NOT (
                        (d.Status = 0 AND i.Status IN (1, 5, 6)) OR
                        (d.Status = 1 AND i.Status IN (2, 3, 5, 6)) OR
                        (d.Status = 2 AND i.Status IN (4, 6)) OR
                        (d.Status = 6 AND i.Status = 5)))
                    THROW 51015, 'Invalid procurement master-data change-request lifecycle transition.', 1;
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_ProcurementConfigurationProfiles_TenantId_ProfileKey_LifecycleStatus",
            table: "ProcurementConfigurationProfiles");
        migrationBuilder.CreateIndex(
            name: "IX_ProcurementConfigurationProfiles_TenantId_ProfileKey_LifecycleStatus",
            table: "ProcurementConfigurationProfiles",
            columns: new[] { "TenantId", "ProfileKey", "LifecycleStatus" },
            unique: true,
            filter: "[LifecycleStatus] = 1 AND [IsDeleted] = 0");

        migrationBuilder.DropIndex(
            name: "IX_ProcurementPolicySets_TenantId_PolicyKey_LifecycleStatus",
            table: "ProcurementPolicySets");
        migrationBuilder.CreateIndex(
            name: "IX_ProcurementPolicySets_TenantId_PolicyKey_LifecycleStatus",
            table: "ProcurementPolicySets",
            columns: new[] { "TenantId", "PolicyKey", "LifecycleStatus" },
            unique: true,
            filter: "[LifecycleStatus] = 1 AND [IsDeleted] = 0");

        migrationBuilder.DropIndex(
            name: "IX_ProcurementMasterDataControlPolicies_TenantId_ResourceType_Status",
            table: "ProcurementMasterDataControlPolicies");
        migrationBuilder.CreateIndex(
            name: "IX_ProcurementMasterDataControlPolicies_TenantId_ResourceType_Status",
            table: "ProcurementMasterDataControlPolicies",
            columns: new[] { "TenantId", "ResourceType", "Status" },
            unique: true,
            filter: "[Status] = 1 AND [IsDeleted] = 0");

        // The originating migration now contains the corrected request transition rule.
    }
}
