using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProcurementMethodSelectionControls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MethodOverrideApprovalActorsJson",
                table: "ProcurementSourcingCases",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "MethodOverrideApprovedAtUtc",
                table: "ProcurementSourcingCases",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MethodOverrideReason",
                table: "ProcurementSourcingCases",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MethodOverrideWorkflowInstanceId",
                table: "ProcurementSourcingCases",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MethodSelectionBasis",
                table: "ProcurementSourcingCases",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RecommendedMethod",
                table: "ProcurementSourcingCases",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSourcingCases_MethodOverrideWorkflowInstanceId",
                table: "ProcurementSourcingCases",
                column: "MethodOverrideWorkflowInstanceId");

            migrationBuilder.Sql("""
                DISABLE TRIGGER [dbo].[TR_ProcurementSourcingCases_Lifecycle] ON [dbo].[ProcurementSourcingCases];
                BEGIN TRY
                    UPDATE [dbo].[ProcurementSourcingCases]
                    SET [RecommendedMethod] = [SelectedMethod],
                        [MethodSelectionBasis] = 0,
                        [MethodOverrideWorkflowInstanceId] = NULL,
                        [MethodOverrideReason] = NULL,
                        [MethodOverrideApprovalActorsJson] = NULL,
                        [MethodOverrideApprovedAtUtc] = NULL
                    WHERE [RecommendedMethod] <> [SelectedMethod]
                       OR [MethodSelectionBasis] <> 0
                       OR [MethodOverrideWorkflowInstanceId] IS NOT NULL
                       OR [MethodOverrideReason] IS NOT NULL
                       OR [MethodOverrideApprovalActorsJson] IS NOT NULL
                       OR [MethodOverrideApprovedAtUtc] IS NOT NULL;
                    ENABLE TRIGGER [dbo].[TR_ProcurementSourcingCases_Lifecycle] ON [dbo].[ProcurementSourcingCases];
                END TRY
                BEGIN CATCH
                    ENABLE TRIGGER [dbo].[TR_ProcurementSourcingCases_Lifecycle] ON [dbo].[ProcurementSourcingCases];
                    THROW;
                END CATCH;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProcurementSourcingCases_MethodSelection",
                table: "ProcurementSourcingCases",
                sql: "[RecommendedMethod] BETWEEN 0 AND 8 AND [SelectedMethod] BETWEEN 0 AND 8 AND [MethodSelectionBasis] IN (0, 1) AND (([MethodSelectionBasis] = 0 AND [SelectedMethod] = [RecommendedMethod] AND [MethodOverrideWorkflowInstanceId] IS NULL AND [MethodOverrideReason] IS NULL AND [MethodOverrideApprovalActorsJson] IS NULL AND [MethodOverrideApprovedAtUtc] IS NULL) OR ([MethodSelectionBasis] = 1 AND [SelectedMethod] <> [RecommendedMethod] AND [ApprovedExceptionRuleId] IS NOT NULL AND [MethodOverrideWorkflowInstanceId] IS NOT NULL AND LEN(LTRIM(RTRIM([MethodOverrideReason]))) >= 5 AND ISJSON([MethodOverrideApprovalActorsJson]) = 1 AND [MethodOverrideApprovalActorsJson] <> '[]' AND [MethodOverrideApprovedAtUtc] IS NOT NULL AND [ExceptionApprovalReference] IS NOT NULL AND [ExceptionEvidenceReference] IS NOT NULL))");

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementSourcingCases_WorkflowInstances_MethodOverrideWorkflowInstanceId",
                table: "ProcurementSourcingCases",
                column: "MethodOverrideWorkflowInstanceId",
                principalTable: "WorkflowInstances",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(MethodSelectionGuardTriggerSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementSourcingCases_MethodSelection];");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementSourcingCases_WorkflowInstances_MethodOverrideWorkflowInstanceId",
                table: "ProcurementSourcingCases");

            migrationBuilder.DropIndex(
                name: "IX_ProcurementSourcingCases_MethodOverrideWorkflowInstanceId",
                table: "ProcurementSourcingCases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProcurementSourcingCases_MethodSelection",
                table: "ProcurementSourcingCases");

            migrationBuilder.DropColumn(
                name: "MethodOverrideApprovalActorsJson",
                table: "ProcurementSourcingCases");

            migrationBuilder.DropColumn(
                name: "MethodOverrideApprovedAtUtc",
                table: "ProcurementSourcingCases");

            migrationBuilder.DropColumn(
                name: "MethodOverrideReason",
                table: "ProcurementSourcingCases");

            migrationBuilder.DropColumn(
                name: "MethodOverrideWorkflowInstanceId",
                table: "ProcurementSourcingCases");

            migrationBuilder.DropColumn(
                name: "MethodSelectionBasis",
                table: "ProcurementSourcingCases");

            migrationBuilder.DropColumn(
                name: "RecommendedMethod",
                table: "ProcurementSourcingCases");

        }

        private const string MethodSelectionGuardTriggerSql = """
            CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementSourcingCases_MethodSelection]
            ON [dbo].[ProcurementSourcingCases]
            AFTER INSERT, UPDATE
            AS
            BEGIN
                SET NOCOUNT ON;

                IF EXISTS
                (
                    SELECT 1
                    FROM inserted i
                    LEFT JOIN [dbo].[PurchaseRequisitions] pr ON pr.[Id] = i.[PurchaseRequisitionId]
                    LEFT JOIN [dbo].[ProcurementPolicySets] ps ON ps.[Id] = i.[PolicySetId]
                    LEFT JOIN [dbo].[ProcurementPolicyExceptionRules] er ON er.[Id] = i.[ApprovedExceptionRuleId]
                    LEFT JOIN [dbo].[WorkflowInstances] wi ON wi.[Id] = i.[MethodOverrideWorkflowInstanceId]
                    LEFT JOIN [dbo].[WorkflowEntityTypes] wet ON wet.[Id] = wi.[EntityTypeId]
                    LEFT JOIN [dbo].[WorkflowDefinitions] wd ON wd.[Id] = wi.[WorkflowDefinitionId]
                    WHERE
                        (i.[MethodSelectionBasis] = 0 AND
                            (i.[SelectedMethod] <> i.[RecommendedMethod]
                             OR i.[MethodOverrideWorkflowInstanceId] IS NOT NULL
                             OR i.[MethodOverrideReason] IS NOT NULL
                             OR i.[MethodOverrideApprovalActorsJson] IS NOT NULL
                             OR i.[MethodOverrideApprovedAtUtc] IS NOT NULL))
                        OR
                        (i.[MethodSelectionBasis] = 1 AND
                            (i.[SelectedMethod] = i.[RecommendedMethod]
                             OR er.[Id] IS NULL OR er.[TenantId] <> i.[TenantId] OR er.[IsDeleted] = 1
                             OR (er.[PolicySetId] <> i.[PolicySetId] AND ISNULL(ps.[BasePolicySetId], '00000000-0000-0000-0000-000000000000') <> er.[PolicySetId])
                             OR er.[IsEnabled] = 0 OR er.[Disposition] <> 1
                             OR er.[EffectiveFrom] > i.[CreatedAt] OR (er.[EffectiveTo] IS NOT NULL AND er.[EffectiveTo] < i.[CreatedAt])
                             OR (er.[Category] IS NOT NULL AND er.[Category] <> i.[Category])
                             OR (er.[Method] IS NOT NULL AND er.[Method] <> i.[SelectedMethod])
                             OR pr.[Id] IS NULL OR pr.[TenantId] <> i.[TenantId] OR pr.[IsDeleted] = 1
                             OR pr.[ApprovedExceptionRuleId] <> i.[ApprovedExceptionRuleId]
                             OR pr.[ExceptionWorkflowInstanceId] <> i.[MethodOverrideWorkflowInstanceId]
                             OR ISNULL(pr.[ExceptionApprovalReference], '') <> ISNULL(i.[ExceptionApprovalReference], '')
                             OR ISNULL(pr.[ExceptionEvidenceReference], '') <> ISNULL(i.[ExceptionEvidenceReference], '')
                             OR wi.[Id] IS NULL OR wi.[TenantId] <> i.[TenantId] OR wi.[IsDeleted] = 1
                             OR wi.[EntityId] <> i.[PurchaseRequisitionId] OR wi.[Status] <> 2 OR wi.[CompletedDate] IS NULL
                             OR wet.[Id] IS NULL OR wet.[TenantId] <> i.[TenantId] OR wet.[IsDeleted] = 1
                             OR UPPER(wet.[Code]) NOT IN ('PROCUREMENT_EXCEPTION', 'PROCUREMENTEXCEPTION')
                             OR er.[WorkflowDefinitionId] IS NULL OR wi.[WorkflowDefinitionId] <> er.[WorkflowDefinitionId]
                             OR wd.[Id] IS NULL OR wd.[TenantId] <> i.[TenantId] OR wd.[IsDeleted] = 1
                             OR wd.[IsActive] = 0 OR wd.[LifecycleStatus] <> 1
                             OR i.[MethodOverrideApprovedAtUtc] <> wi.[CompletedDate]
                             OR pr.[ExceptionApprovedAtUtc] IS NULL OR pr.[ExceptionApprovedAtUtc] <> wi.[CompletedDate]
                             OR (er.[MaximumDurationDays] IS NOT NULL AND DATEADD(DAY, er.[MaximumDurationDays], wi.[CompletedDate]) < i.[CreatedAt])
                             OR NOT EXISTS
                                (SELECT 1 FROM OPENJSON(i.[MethodOverrideApprovalActorsJson]) j
                                 WHERE TRY_CONVERT(uniqueidentifier, j.[value]) IS NOT NULL)
                             OR EXISTS
                                (SELECT 1 FROM OPENJSON(i.[MethodOverrideApprovalActorsJson]) j
                                 WHERE TRY_CONVERT(uniqueidentifier, j.[value]) IS NULL)
                             OR EXISTS
                                (SELECT 1 FROM OPENJSON(i.[MethodOverrideApprovalActorsJson]) j
                                 WHERE TRY_CONVERT(uniqueidentifier, j.[value]) IN (pr.[RequestedById], i.[CreatedById]))
                             OR EXISTS
                                (SELECT 1 FROM OPENJSON(i.[MethodOverrideApprovalActorsJson]) j
                                 WHERE NOT EXISTS
                                    (SELECT 1
                                     FROM [dbo].[WorkflowStepInstances] wsi
                                     INNER JOIN [dbo].[WorkflowApprovals] wa ON wa.[StepInstanceId] = wsi.[Id]
                                     WHERE wsi.[WorkflowInstanceId] = wi.[Id] AND wsi.[TenantId] = i.[TenantId]
                                       AND wsi.[IsDeleted] = 0 AND wa.[TenantId] = i.[TenantId] AND wa.[IsDeleted] = 0
                                       AND wa.[Status] = 1 AND wa.[ProcessedById] = TRY_CONVERT(uniqueidentifier, j.[value])))
                             OR EXISTS
                                (SELECT 1
                                 FROM [dbo].[WorkflowStepInstances] wsi
                                 INNER JOIN [dbo].[WorkflowApprovals] wa ON wa.[StepInstanceId] = wsi.[Id]
                                 WHERE wsi.[WorkflowInstanceId] = wi.[Id] AND wsi.[TenantId] = i.[TenantId]
                                   AND wsi.[IsDeleted] = 0 AND wa.[TenantId] = i.[TenantId] AND wa.[IsDeleted] = 0
                                   AND wa.[Status] = 1 AND wa.[ProcessedById] IS NOT NULL
                                   AND NOT EXISTS
                                      (SELECT 1 FROM OPENJSON(i.[MethodOverrideApprovalActorsJson]) j
                                       WHERE TRY_CONVERT(uniqueidentifier, j.[value]) = wa.[ProcessedById]))
                             OR NOT EXISTS
                                (SELECT 1
                                 FROM [dbo].[WorkflowStepInstances] wsi
                                 INNER JOIN [dbo].[WorkflowApprovals] wa ON wa.[StepInstanceId] = wsi.[Id]
                                 WHERE wsi.[WorkflowInstanceId] = wi.[Id] AND wsi.[TenantId] = i.[TenantId]
                                   AND wsi.[IsDeleted] = 0 AND wa.[TenantId] = i.[TenantId] AND wa.[IsDeleted] = 0
                                   AND wa.[Status] = 1 AND wa.[ProcessedById] IS NOT NULL
                                   AND UPPER(ISNULL(wa.[ApproverRole], '')) = UPPER(er.[ApproverRole]))))
                )
                    THROW 51073, 'Procurement sourcing method recommendation or approved override lineage is invalid.', 1;

                IF EXISTS
                (
                    SELECT 1
                    FROM inserted i
                    INNER JOIN deleted d ON d.[Id] = i.[Id]
                    WHERE d.[RecommendedMethod] <> i.[RecommendedMethod]
                       OR d.[MethodSelectionBasis] <> i.[MethodSelectionBasis]
                       OR ISNULL(d.[MethodOverrideWorkflowInstanceId], '00000000-0000-0000-0000-000000000000') <> ISNULL(i.[MethodOverrideWorkflowInstanceId], '00000000-0000-0000-0000-000000000000')
                       OR ISNULL(d.[MethodOverrideReason], '') <> ISNULL(i.[MethodOverrideReason], '')
                       OR ISNULL(d.[MethodOverrideApprovalActorsJson], '') <> ISNULL(i.[MethodOverrideApprovalActorsJson], '')
                       OR ISNULL(d.[MethodOverrideApprovedAtUtc], '19000101') <> ISNULL(i.[MethodOverrideApprovedAtUtc], '19000101')
                )
                    THROW 51074, 'Procurement sourcing method decision lineage is immutable.', 1;
            END
            """;
    }
}
