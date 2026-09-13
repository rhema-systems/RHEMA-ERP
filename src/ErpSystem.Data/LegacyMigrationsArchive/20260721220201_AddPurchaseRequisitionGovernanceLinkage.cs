using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchaseRequisitionGovernanceLinkage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PurchaseRequisitions_TenantId",
                table: "PurchaseRequisitions");

            migrationBuilder.AddColumn<string>(
                name: "ApprovedExceptionName",
                table: "PurchaseRequisitions",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovedExceptionRuleCode",
                table: "PurchaseRequisitions",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedExceptionRuleId",
                table: "PurchaseRequisitions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExceptionApprovalReference",
                table: "PurchaseRequisitions",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExceptionApprovedAtUtc",
                table: "PurchaseRequisitions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ExceptionApprovedById",
                table: "PurchaseRequisitions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExceptionApprovedByName",
                table: "PurchaseRequisitions",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExceptionEvidenceReference",
                table: "PurchaseRequisitions",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ExceptionWorkflowInstanceId",
                table: "PurchaseRequisitions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LinkageLastUpdatedAtUtc",
                table: "PurchaseRequisitions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LinkageLastUpdatedById",
                table: "PurchaseRequisitions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LinkageLastUpdatedByName",
                table: "PurchaseRequisitions",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LinkageRevision",
                table: "PurchaseRequisitions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ProcurementCategory",
                table: "PurchaseRequisitions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "PurchaseRequisitions",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<string>(
                name: "SourcePlanItemDescription",
                table: "PurchaseRequisitions",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourcePlanItemId",
                table: "PurchaseRequisitions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourcePlanNumber",
                table: "PurchaseRequisitions",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourcePlanTitle",
                table: "PurchaseRequisitions",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SpecificationTemplateCode",
                table: "PurchaseRequisitions",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SpecificationTemplateId",
                table: "PurchaseRequisitions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SpecificationTemplateName",
                table: "PurchaseRequisitions",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SpecificationTemplateVersion",
                table: "PurchaseRequisitions",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequisitions_ApprovedExceptionRuleId",
                table: "PurchaseRequisitions",
                column: "ApprovedExceptionRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequisitions_BudgetId",
                table: "PurchaseRequisitions",
                column: "BudgetId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequisitions_ExceptionApprovedById",
                table: "PurchaseRequisitions",
                column: "ExceptionApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequisitions_ExceptionWorkflowInstanceId",
                table: "PurchaseRequisitions",
                column: "ExceptionWorkflowInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequisitions_LinkageLastUpdatedById",
                table: "PurchaseRequisitions",
                column: "LinkageLastUpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequisitions_ProjectId",
                table: "PurchaseRequisitions",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequisitions_SourcePlanId",
                table: "PurchaseRequisitions",
                column: "SourcePlanId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequisitions_SourcePlanItemId",
                table: "PurchaseRequisitions",
                column: "SourcePlanItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequisitions_SpecificationTemplateId",
                table: "PurchaseRequisitions",
                column: "SpecificationTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequisitions_TenantId_ApprovedExceptionRuleId",
                table: "PurchaseRequisitions",
                columns: new[] { "TenantId", "ApprovedExceptionRuleId" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequisitions_TenantId_BudgetId",
                table: "PurchaseRequisitions",
                columns: new[] { "TenantId", "BudgetId" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequisitions_TenantId_ProcurementCategory",
                table: "PurchaseRequisitions",
                columns: new[] { "TenantId", "ProcurementCategory" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequisitions_TenantId_ProjectId",
                table: "PurchaseRequisitions",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequisitions_TenantId_SourcePlanItemId",
                table: "PurchaseRequisitions",
                columns: new[] { "TenantId", "SourcePlanItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequisitions_TenantId_SpecificationTemplateId",
                table: "PurchaseRequisitions",
                columns: new[] { "TenantId", "SpecificationTemplateId" });

            migrationBuilder.Sql(
                "UPDATE [PurchaseRequisitions] SET [RequisitionType] = 1 WHERE [RequisitionType] NOT BETWEEN 1 AND 5;");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PurchaseRequisitions_ExceptionLink",
                table: "PurchaseRequisitions",
                sql: "([ApprovedExceptionRuleId] IS NULL AND [ExceptionWorkflowInstanceId] IS NULL AND [ExceptionApprovalReference] IS NULL AND [ExceptionApprovedAtUtc] IS NULL) OR ([ApprovedExceptionRuleId] IS NOT NULL AND [ExceptionWorkflowInstanceId] IS NOT NULL AND [ExceptionApprovalReference] IS NOT NULL AND [ExceptionApprovedAtUtc] IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PurchaseRequisitions_LinkageRevision",
                table: "PurchaseRequisitions",
                sql: "[LinkageRevision] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PurchaseRequisitions_PlanItemLink",
                table: "PurchaseRequisitions",
                sql: "[SourcePlanItemId] IS NULL OR [SourcePlanId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PurchaseRequisitions_ProcurementCategory",
                table: "PurchaseRequisitions",
                sql: "[ProcurementCategory] IS NULL OR [ProcurementCategory] BETWEEN 0 AND 4");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PurchaseRequisitions_RequisitionType",
                table: "PurchaseRequisitions",
                sql: "[RequisitionType] BETWEEN 1 AND 5");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseRequisitions_ProcurementBudgets_BudgetId",
                table: "PurchaseRequisitions",
                column: "BudgetId",
                principalTable: "ProcurementBudgets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseRequisitions_ProcurementPlanItems_SourcePlanItemId",
                table: "PurchaseRequisitions",
                column: "SourcePlanItemId",
                principalTable: "ProcurementPlanItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseRequisitions_ProcurementPlans_SourcePlanId",
                table: "PurchaseRequisitions",
                column: "SourcePlanId",
                principalTable: "ProcurementPlans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseRequisitions_ProcurementPolicyExceptionRules_ApprovedExceptionRuleId",
                table: "PurchaseRequisitions",
                column: "ApprovedExceptionRuleId",
                principalTable: "ProcurementPolicyExceptionRules",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseRequisitions_ProcurementSpecificationTemplates_SpecificationTemplateId",
                table: "PurchaseRequisitions",
                column: "SpecificationTemplateId",
                principalTable: "ProcurementSpecificationTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseRequisitions_Projects_ProjectId",
                table: "PurchaseRequisitions",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseRequisitions_Users_ExceptionApprovedById",
                table: "PurchaseRequisitions",
                column: "ExceptionApprovedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseRequisitions_Users_LinkageLastUpdatedById",
                table: "PurchaseRequisitions",
                column: "LinkageLastUpdatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseRequisitions_WorkflowInstances_ExceptionWorkflowInstanceId",
                table: "PurchaseRequisitions",
                column: "ExceptionWorkflowInstanceId",
                principalTable: "WorkflowInstances",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_PurchaseRequisitions_LinkageGuard]
                ON [dbo].[PurchaseRequisitions]
                AFTER UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS
                    (
                        SELECT 1
                        FROM inserted AS i
                        INNER JOIN deleted AS d ON d.[Id] = i.[Id]
                        WHERE d.[Status] <> N'Draft'
                          AND EXISTS
                          (
                              SELECT i.[SourcePlanId], i.[SourcePlanItemId], i.[SourcePlanNumber], i.[SourcePlanTitle],
                                     i.[SourcePlanItemDescription], i.[BudgetId], i.[BudgetCode], i.[BudgetAllocated],
                                     i.[BudgetRemaining], i.[BudgetValidated], i.[ProcurementCategory], i.[CostCenter],
                                     i.[ProjectId], i.[ProjectCode], i.[ProjectName], i.[RequisitionType],
                                     i.[SpecificationTemplateId], i.[SpecificationTemplateCode], i.[SpecificationTemplateName],
                                     i.[SpecificationTemplateVersion], i.[ApprovedExceptionRuleId], i.[ApprovedExceptionRuleCode],
                                     i.[ApprovedExceptionName], i.[ExceptionWorkflowInstanceId], i.[ExceptionApprovalReference],
                                     i.[ExceptionEvidenceReference], i.[ExceptionApprovedById], i.[ExceptionApprovedByName],
                                     i.[ExceptionApprovedAtUtc], i.[LinkageRevision], i.[LinkageLastUpdatedAtUtc],
                                     i.[LinkageLastUpdatedById], i.[LinkageLastUpdatedByName]
                              EXCEPT
                              SELECT d.[SourcePlanId], d.[SourcePlanItemId], d.[SourcePlanNumber], d.[SourcePlanTitle],
                                     d.[SourcePlanItemDescription], d.[BudgetId], d.[BudgetCode], d.[BudgetAllocated],
                                     d.[BudgetRemaining], d.[BudgetValidated], d.[ProcurementCategory], d.[CostCenter],
                                     d.[ProjectId], d.[ProjectCode], d.[ProjectName], d.[RequisitionType],
                                     d.[SpecificationTemplateId], d.[SpecificationTemplateCode], d.[SpecificationTemplateName],
                                     d.[SpecificationTemplateVersion], d.[ApprovedExceptionRuleId], d.[ApprovedExceptionRuleCode],
                                     d.[ApprovedExceptionName], d.[ExceptionWorkflowInstanceId], d.[ExceptionApprovalReference],
                                     d.[ExceptionEvidenceReference], d.[ExceptionApprovedById], d.[ExceptionApprovedByName],
                                     d.[ExceptionApprovedAtUtc], d.[LinkageRevision], d.[LinkageLastUpdatedAtUtc],
                                     d.[LinkageLastUpdatedById], d.[LinkageLastUpdatedByName]
                          )
                    )
                    BEGIN
                        THROW 51020, 'Purchase requisition governance linkage is immutable after Draft.', 1;
                    END;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_PurchaseRequisitions_LinkageGuard];");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseRequisitions_ProcurementBudgets_BudgetId",
                table: "PurchaseRequisitions");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseRequisitions_ProcurementPlanItems_SourcePlanItemId",
                table: "PurchaseRequisitions");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseRequisitions_ProcurementPlans_SourcePlanId",
                table: "PurchaseRequisitions");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseRequisitions_ProcurementPolicyExceptionRules_ApprovedExceptionRuleId",
                table: "PurchaseRequisitions");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseRequisitions_ProcurementSpecificationTemplates_SpecificationTemplateId",
                table: "PurchaseRequisitions");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseRequisitions_Projects_ProjectId",
                table: "PurchaseRequisitions");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseRequisitions_Users_ExceptionApprovedById",
                table: "PurchaseRequisitions");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseRequisitions_Users_LinkageLastUpdatedById",
                table: "PurchaseRequisitions");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseRequisitions_WorkflowInstances_ExceptionWorkflowInstanceId",
                table: "PurchaseRequisitions");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseRequisitions_ApprovedExceptionRuleId",
                table: "PurchaseRequisitions");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseRequisitions_BudgetId",
                table: "PurchaseRequisitions");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseRequisitions_ExceptionApprovedById",
                table: "PurchaseRequisitions");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseRequisitions_ExceptionWorkflowInstanceId",
                table: "PurchaseRequisitions");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseRequisitions_LinkageLastUpdatedById",
                table: "PurchaseRequisitions");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseRequisitions_ProjectId",
                table: "PurchaseRequisitions");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseRequisitions_SourcePlanId",
                table: "PurchaseRequisitions");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseRequisitions_SourcePlanItemId",
                table: "PurchaseRequisitions");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseRequisitions_SpecificationTemplateId",
                table: "PurchaseRequisitions");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseRequisitions_TenantId_ApprovedExceptionRuleId",
                table: "PurchaseRequisitions");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseRequisitions_TenantId_BudgetId",
                table: "PurchaseRequisitions");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseRequisitions_TenantId_ProcurementCategory",
                table: "PurchaseRequisitions");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseRequisitions_TenantId_ProjectId",
                table: "PurchaseRequisitions");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseRequisitions_TenantId_SourcePlanItemId",
                table: "PurchaseRequisitions");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseRequisitions_TenantId_SpecificationTemplateId",
                table: "PurchaseRequisitions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PurchaseRequisitions_ExceptionLink",
                table: "PurchaseRequisitions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PurchaseRequisitions_LinkageRevision",
                table: "PurchaseRequisitions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PurchaseRequisitions_PlanItemLink",
                table: "PurchaseRequisitions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PurchaseRequisitions_ProcurementCategory",
                table: "PurchaseRequisitions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PurchaseRequisitions_RequisitionType",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "ApprovedExceptionName",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "ApprovedExceptionRuleCode",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "ApprovedExceptionRuleId",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "ExceptionApprovalReference",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "ExceptionApprovedAtUtc",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "ExceptionApprovedById",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "ExceptionApprovedByName",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "ExceptionEvidenceReference",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "ExceptionWorkflowInstanceId",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "LinkageLastUpdatedAtUtc",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "LinkageLastUpdatedById",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "LinkageLastUpdatedByName",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "LinkageRevision",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "ProcurementCategory",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "SourcePlanItemDescription",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "SourcePlanItemId",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "SourcePlanNumber",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "SourcePlanTitle",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "SpecificationTemplateCode",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "SpecificationTemplateId",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "SpecificationTemplateName",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "SpecificationTemplateVersion",
                table: "PurchaseRequisitions");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequisitions_TenantId",
                table: "PurchaseRequisitions",
                column: "TenantId");
        }
    }
}
