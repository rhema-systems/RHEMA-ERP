using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

public partial class SimplifyPurchaseRequisitionControls : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementRequisitionSourcingReleases_TenantGuard];

            IF EXISTS
            (
                SELECT 1 FROM sys.check_constraints
                 WHERE [name] = 'CK_ProcurementRequisitionSourcingReleases_Lineage'
            )
                ALTER TABLE [dbo].[ProcurementRequisitionSourcingReleases]
                DROP CONSTRAINT [CK_ProcurementRequisitionSourcingReleases_Lineage];

            ALTER TABLE [dbo].[ProcurementRequisitionSourcingReleases] ALTER COLUMN [SourcePlanId] uniqueidentifier NULL;
            ALTER TABLE [dbo].[ProcurementRequisitionSourcingReleases] ALTER COLUMN [SourcePlanItemId] uniqueidentifier NULL;
            ALTER TABLE [dbo].[ProcurementRequisitionSourcingReleases] ALTER COLUMN [SpecificationTemplateId] uniqueidentifier NULL;
            ALTER TABLE [dbo].[ProcurementRequisitionSourcingReleases] ALTER COLUMN [SpecificationTemplateCode] varchar(50) NULL;
            ALTER TABLE [dbo].[ProcurementRequisitionSourcingReleases] ALTER COLUMN [SpecificationTemplateVersion] int NULL;
            ALTER TABLE [dbo].[ProcurementRequisitionSourcingReleases] ALTER COLUMN [BudgetCommitmentId] uniqueidentifier NULL;
            ALTER TABLE [dbo].[ProcurementRequisitionSourcingReleases] ALTER COLUMN [BudgetCommitmentReference] nvarchar(100) NULL;
            ALTER TABLE [dbo].[ProcurementRequisitionSourcingReleases] ALTER COLUMN [AuthorityRouteId] uniqueidentifier NULL;
            ALTER TABLE [dbo].[ProcurementRequisitionSourcingReleases] ALTER COLUMN [AuthorityRouteReference] nvarchar(100) NULL;
            ALTER TABLE [dbo].[ProcurementRequisitionSourcingReleases] ALTER COLUMN [WorkflowInstanceId] uniqueidentifier NULL;
            """);

        migrationBuilder.Sql(
            """
            CREATE TRIGGER [dbo].[TR_ProcurementRequisitionSourcingReleases_TenantGuard]
            ON [dbo].[ProcurementRequisitionSourcingReleases]
            AFTER INSERT
            AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS
                (
                    SELECT 1
                    FROM inserted i
                    LEFT JOIN [dbo].[PurchaseRequisitions] pr ON pr.[Id] = i.[PurchaseRequisitionId]
                    LEFT JOIN [dbo].[ProcurementPlans] pp ON pp.[Id] = i.[SourcePlanId]
                    LEFT JOIN [dbo].[ProcurementPlanItems] pi ON pi.[Id] = i.[SourcePlanItemId]
                    LEFT JOIN [dbo].[ProcurementSpecificationTemplates] st ON st.[Id] = i.[SpecificationTemplateId]
                    LEFT JOIN [dbo].[ProcurementBudgetCommitments] bc ON bc.[Id] = i.[BudgetCommitmentId]
                    LEFT JOIN [dbo].[ProcurementRequisitionAuthorityRoutes] ar ON ar.[Id] = i.[AuthorityRouteId]
                    LEFT JOIN [dbo].[WorkflowInstances] wi ON wi.[Id] = i.[WorkflowInstanceId]
                    WHERE pr.[Id] IS NULL OR pr.[TenantId] <> i.[TenantId] OR pr.[IsDeleted] = 1
                       OR pr.[Status] <> 'Approved' OR pr.[ApprovedAt] IS NULL OR pr.[ApprovedById] IS NULL
                       OR ISJSON(i.[SnapshotJson]) <> 1
                       OR (i.[SourcePlanId] IS NOT NULL AND (pp.[Id] IS NULL OR pp.[TenantId] <> i.[TenantId] OR pp.[IsDeleted] = 1))
                       OR (i.[SourcePlanItemId] IS NOT NULL AND (pi.[Id] IS NULL OR pi.[TenantId] <> i.[TenantId] OR pi.[IsDeleted] = 1
                           OR (i.[SourcePlanId] IS NOT NULL AND pi.[ProcurementPlanId] <> i.[SourcePlanId])))
                       OR (i.[SpecificationTemplateId] IS NOT NULL AND (st.[Id] IS NULL OR st.[TenantId] <> i.[TenantId] OR st.[IsDeleted] = 1))
                       OR (i.[BudgetCommitmentId] IS NOT NULL AND (bc.[Id] IS NULL OR bc.[TenantId] <> i.[TenantId] OR bc.[IsDeleted] = 1
                           OR bc.[PurchaseRequisitionId] <> i.[PurchaseRequisitionId]))
                       OR (i.[AuthorityRouteId] IS NOT NULL AND (ar.[Id] IS NULL OR ar.[TenantId] <> i.[TenantId] OR ar.[IsDeleted] = 1
                           OR ar.[PurchaseRequisitionId] <> i.[PurchaseRequisitionId]))
                       OR (i.[WorkflowInstanceId] IS NOT NULL AND (wi.[Id] IS NULL OR wi.[TenantId] <> i.[TenantId] OR wi.[IsDeleted] = 1
                           OR wi.[EntityId] <> i.[PurchaseRequisitionId] OR wi.[Status] <> 2 OR wi.[CompletedDate] IS NULL))
                )
                BEGIN
                    THROW 51041, 'Sourcing release tenant or approved requisition lineage is invalid.', 1;
                END
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF EXISTS
            (
                SELECT 1 FROM [dbo].[ProcurementRequisitionSourcingReleases]
                 WHERE [SourcePlanId] IS NULL OR [SourcePlanItemId] IS NULL OR [SpecificationTemplateId] IS NULL
                    OR [SpecificationTemplateCode] IS NULL OR [SpecificationTemplateVersion] IS NULL
                    OR [BudgetCommitmentId] IS NULL OR [BudgetCommitmentReference] IS NULL
                    OR [AuthorityRouteId] IS NULL OR [AuthorityRouteReference] IS NULL OR [WorkflowInstanceId] IS NULL
            )
                THROW 51049, 'Cannot restore legacy sourcing-release columns while simplified release rows exist.', 1;

            DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementRequisitionSourcingReleases_TenantGuard];
            ALTER TABLE [dbo].[ProcurementRequisitionSourcingReleases] ALTER COLUMN [SourcePlanId] uniqueidentifier NOT NULL;
            ALTER TABLE [dbo].[ProcurementRequisitionSourcingReleases] ALTER COLUMN [SourcePlanItemId] uniqueidentifier NOT NULL;
            ALTER TABLE [dbo].[ProcurementRequisitionSourcingReleases] ALTER COLUMN [SpecificationTemplateId] uniqueidentifier NOT NULL;
            ALTER TABLE [dbo].[ProcurementRequisitionSourcingReleases] ALTER COLUMN [SpecificationTemplateCode] varchar(50) NOT NULL;
            ALTER TABLE [dbo].[ProcurementRequisitionSourcingReleases] ALTER COLUMN [SpecificationTemplateVersion] int NOT NULL;
            ALTER TABLE [dbo].[ProcurementRequisitionSourcingReleases] ALTER COLUMN [BudgetCommitmentId] uniqueidentifier NOT NULL;
            ALTER TABLE [dbo].[ProcurementRequisitionSourcingReleases] ALTER COLUMN [BudgetCommitmentReference] nvarchar(100) NOT NULL;
            ALTER TABLE [dbo].[ProcurementRequisitionSourcingReleases] ALTER COLUMN [AuthorityRouteId] uniqueidentifier NOT NULL;
            ALTER TABLE [dbo].[ProcurementRequisitionSourcingReleases] ALTER COLUMN [AuthorityRouteReference] nvarchar(100) NOT NULL;
            ALTER TABLE [dbo].[ProcurementRequisitionSourcingReleases] ALTER COLUMN [WorkflowInstanceId] uniqueidentifier NOT NULL;
            ALTER TABLE [dbo].[ProcurementRequisitionSourcingReleases]
              ADD CONSTRAINT [CK_ProcurementRequisitionSourcingReleases_Lineage]
              CHECK ([SourcePlanId] <> '00000000-0000-0000-0000-000000000000' AND [SourcePlanItemId] <> '00000000-0000-0000-0000-000000000000'
                 AND [SpecificationTemplateId] <> '00000000-0000-0000-0000-000000000000' AND [BudgetCommitmentId] <> '00000000-0000-0000-0000-000000000000'
                 AND [AuthorityRouteId] <> '00000000-0000-0000-0000-000000000000' AND [WorkflowInstanceId] <> '00000000-0000-0000-0000-000000000000');
            """);
    }
}
