using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Repairs the known partial workflow-definition update that soft-deleted the
/// complete Procurement Budget approval graph before replacement rows were saved.
/// The repair is deliberately restricted to the published, active budget workflow
/// with the exact governed three-step template and no active start step.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260822224500_RepairProcurementBudgetApprovalWorkflowGraph")]
public sealed class RepairProcurementBudgetApprovalWorkflowGraph : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ;WITH RepairableDefinitions AS
            (
                SELECT definition.Id
                FROM dbo.WorkflowDefinitions definition
                INNER JOIN dbo.WorkflowEntityTypes entityType
                    ON entityType.Id = definition.EntityTypeId
                   AND entityType.TenantId = definition.TenantId
                   AND entityType.IsDeleted = 0
                WHERE definition.Name = N'TDC Procurement Budget Approval'
                  AND entityType.Code = N'PROCUREMENT_BUDGET'
                  AND definition.IsActive = 1
                  AND definition.IsDeleted = 0
                  AND definition.LifecycleStatus = 1
                  AND NOT EXISTS
                  (
                      SELECT 1
                      FROM dbo.WorkflowSteps activeStart
                      WHERE activeStart.WorkflowDefinitionId = definition.Id
                        AND activeStart.TenantId = definition.TenantId
                        AND activeStart.IsDeleted = 0
                        AND activeStart.IsStartStep = 1
                  )
                  AND 3 =
                  (
                      SELECT COUNT(*)
                      FROM dbo.WorkflowSteps deletedStep
                      WHERE deletedStep.WorkflowDefinitionId = definition.Id
                        AND deletedStep.TenantId = definition.TenantId
                        AND deletedStep.IsDeleted = 1
                        AND deletedStep.Name IN (N'Submitted', N'Approval', N'Completed')
                  )
            )
            UPDATE step
               SET step.IsDeleted = 0,
                   step.DeletedAt = NULL,
                   step.DeletedBy = NULL,
                   step.UpdatedAt = SYSUTCDATETIME(),
                   step.UpdatedBy = N'Migration:RepairProcurementBudgetApprovalWorkflowGraph'
            FROM dbo.WorkflowSteps step
            INNER JOIN RepairableDefinitions repair
                ON repair.Id = step.WorkflowDefinitionId
            WHERE step.IsDeleted = 1
              AND step.Name IN (N'Submitted', N'Approval', N'Completed');

            ;WITH RepairableDefinitions AS
            (
                SELECT definition.Id
                FROM dbo.WorkflowDefinitions definition
                INNER JOIN dbo.WorkflowEntityTypes entityType
                    ON entityType.Id = definition.EntityTypeId
                   AND entityType.TenantId = definition.TenantId
                   AND entityType.IsDeleted = 0
                WHERE definition.Name = N'TDC Procurement Budget Approval'
                  AND entityType.Code = N'PROCUREMENT_BUDGET'
                  AND definition.IsActive = 1
                  AND definition.IsDeleted = 0
                  AND definition.LifecycleStatus = 1
                  AND EXISTS
                  (
                      SELECT 1
                      FROM dbo.WorkflowSteps activeStart
                      WHERE activeStart.WorkflowDefinitionId = definition.Id
                        AND activeStart.TenantId = definition.TenantId
                        AND activeStart.IsDeleted = 0
                        AND activeStart.IsStartStep = 1
                  )
                  AND 2 =
                  (
                      SELECT COUNT(*)
                      FROM dbo.WorkflowTransitions deletedTransition
                      WHERE deletedTransition.WorkflowDefinitionId = definition.Id
                        AND deletedTransition.TenantId = definition.TenantId
                        AND deletedTransition.IsDeleted = 1
                        AND deletedTransition.Name IN (N'Submit for approval', N'Approve')
                  )
            )
            UPDATE transition
               SET transition.IsDeleted = 0,
                   transition.DeletedAt = NULL,
                   transition.DeletedBy = NULL,
                   transition.UpdatedAt = SYSUTCDATETIME(),
                   transition.UpdatedBy = N'Migration:RepairProcurementBudgetApprovalWorkflowGraph'
            FROM dbo.WorkflowTransitions transition
            INNER JOIN RepairableDefinitions repair
                ON repair.Id = transition.WorkflowDefinitionId
            INNER JOIN dbo.WorkflowSteps sourceStep
                ON sourceStep.Id = transition.FromStepId
               AND sourceStep.WorkflowDefinitionId = transition.WorkflowDefinitionId
               AND sourceStep.IsDeleted = 0
            INNER JOIN dbo.WorkflowSteps targetStep
                ON targetStep.Id = transition.ToStepId
               AND targetStep.WorkflowDefinitionId = transition.WorkflowDefinitionId
               AND targetStep.IsDeleted = 0
            WHERE transition.IsDeleted = 1
              AND transition.Name IN (N'Submit for approval', N'Approve');
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // This is a targeted production-data repair. Re-deleting a valid approval
        // graph would reintroduce the outage, so rollback intentionally retains it.
    }
}
