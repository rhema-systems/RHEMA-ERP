using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProcurementRequisitionBudgetCommitments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProcurementBudgets_BudgetCode",
                table: "ProcurementBudgets");

            migrationBuilder.DropIndex(
                name: "IX_ProcurementBudgets_TenantId",
                table: "ProcurementBudgets");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ProcurementBudgets",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateTable(
                name: "ProcurementBudgetCommitments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProcurementBudgetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseRequisitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReservationReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ReservationSequence = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ReservedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    BudgetAllocatedSnapshot = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    BudgetUtilizedSnapshot = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    BudgetCommittedBefore = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    BudgetAvailableBefore = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    BudgetCommittedAfter = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    BudgetAvailableAfter = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    IsOverride = table.Column<bool>(type: "bit", nullable: false),
                    OverrideRuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OverrideRuleCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    OverrideWorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OverrideApprovalReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    OverrideEvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OverrideApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReservedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReservedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReservedByName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ReleasedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReleasedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReleasedByName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ReleaseReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
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
                    table.PrimaryKey("PK_ProcurementBudgetCommitments", x => x.Id);
                    table.CheckConstraint("CK_ProcurementBudgetCommitments_Amount", "[ReservedAmount] > 0");
                    table.CheckConstraint("CK_ProcurementBudgetCommitments_Sequence", "[ReservationSequence] > 0");
                    table.CheckConstraint("CK_ProcurementBudgetCommitments_Status", "[Status] IN (1, 2, 3)");
                    table.ForeignKey(
                        name: "FK_ProcurementBudgetCommitments_ProcurementBudgets_ProcurementBudgetId",
                        column: x => x.ProcurementBudgetId,
                        principalTable: "ProcurementBudgets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementBudgetCommitments_ProcurementPolicyExceptionRules_OverrideRuleId",
                        column: x => x.OverrideRuleId,
                        principalTable: "ProcurementPolicyExceptionRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementBudgetCommitments_PurchaseRequisitions_PurchaseRequisitionId",
                        column: x => x.PurchaseRequisitionId,
                        principalTable: "PurchaseRequisitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementBudgetCommitments_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementBudgetCommitments_WorkflowInstances_OverrideWorkflowInstanceId",
                        column: x => x.OverrideWorkflowInstanceId,
                        principalTable: "WorkflowInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBudgets_TenantId_BudgetCode",
                table: "ProcurementBudgets",
                columns: new[] { "TenantId", "BudgetCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBudgetCommitments_OverrideRuleId",
                table: "ProcurementBudgetCommitments",
                column: "OverrideRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBudgetCommitments_OverrideWorkflowInstanceId",
                table: "ProcurementBudgetCommitments",
                column: "OverrideWorkflowInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBudgetCommitments_ProcurementBudgetId",
                table: "ProcurementBudgetCommitments",
                column: "ProcurementBudgetId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBudgetCommitments_PurchaseRequisitionId",
                table: "ProcurementBudgetCommitments",
                column: "PurchaseRequisitionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBudgetCommitments_TenantId_ProcurementBudgetId_Status",
                table: "ProcurementBudgetCommitments",
                columns: new[] { "TenantId", "ProcurementBudgetId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBudgetCommitments_TenantId_PurchaseRequisitionId",
                table: "ProcurementBudgetCommitments",
                columns: new[] { "TenantId", "PurchaseRequisitionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBudgetCommitments_TenantId_ReservationReference",
                table: "ProcurementBudgetCommitments",
                columns: new[] { "TenantId", "ReservationReference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBudgetCommitments_TenantId_Status_ReservedAtUtc",
                table: "ProcurementBudgetCommitments",
                columns: new[] { "TenantId", "Status", "ReservedAtUtc" });

            migrationBuilder.Sql(
                """
                CREATE TRIGGER [dbo].[TR_ProcurementBudgetCommitments_NoDelete]
                ON [dbo].[ProcurementBudgetCommitments]
                INSTEAD OF DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    THROW 51000, 'Purchase-requisition budget commitments are retained as auditable records and cannot be deleted.', 1;
                END;
                """);

            migrationBuilder.Sql(
                """
                CREATE TRIGGER [dbo].[TR_ProcurementBudgetCommitments_TenantAndEvidenceGuard]
                ON [dbo].[ProcurementBudgetCommitments]
                AFTER INSERT, UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS
                    (
                        SELECT 1
                        FROM inserted AS i
                        INNER JOIN [dbo].[ProcurementBudgets] AS b ON b.[Id] = i.[ProcurementBudgetId]
                        INNER JOIN [dbo].[PurchaseRequisitions] AS pr ON pr.[Id] = i.[PurchaseRequisitionId]
                        LEFT JOIN [dbo].[ProcurementPolicyExceptionRules] AS er ON er.[Id] = i.[OverrideRuleId]
                        LEFT JOIN [dbo].[WorkflowInstances] AS wi ON wi.[Id] = i.[OverrideWorkflowInstanceId]
                        WHERE b.[TenantId] <> i.[TenantId]
                           OR pr.[TenantId] <> i.[TenantId]
                           OR b.[IsDeleted] = 1
                           OR pr.[IsDeleted] = 1
                           OR (i.[OverrideRuleId] IS NOT NULL AND (er.[TenantId] <> i.[TenantId] OR er.[IsDeleted] = 1))
                           OR (i.[OverrideWorkflowInstanceId] IS NOT NULL AND (wi.[TenantId] <> i.[TenantId] OR wi.[IsDeleted] = 1))
                           OR (i.[Status] = 1 AND (i.[ReleasedAtUtc] IS NOT NULL OR i.[ReleasedById] IS NOT NULL OR i.[ReleasedByName] IS NOT NULL OR i.[ReleaseReason] IS NOT NULL))
                           OR (i.[Status] = 2 AND (i.[ReleasedAtUtc] IS NULL OR i.[ReleasedById] IS NULL OR NULLIF(LTRIM(RTRIM(i.[ReleasedByName])), N'') IS NULL OR NULLIF(LTRIM(RTRIM(i.[ReleaseReason])), N'') IS NULL))
                           OR i.[BudgetAvailableBefore] <> i.[BudgetAllocatedSnapshot] - i.[BudgetUtilizedSnapshot] - i.[BudgetCommittedBefore]
                           OR i.[BudgetAvailableAfter] <> i.[BudgetAllocatedSnapshot] - i.[BudgetUtilizedSnapshot] - i.[BudgetCommittedAfter]
                           OR (i.[Status] = 1 AND (i.[BudgetCommittedAfter] <> i.[BudgetCommittedBefore] + i.[ReservedAmount] OR i.[BudgetAvailableAfter] <> i.[BudgetAvailableBefore] - i.[ReservedAmount]))
                           OR (i.[IsOverride] = 1 AND (i.[OverrideRuleId] IS NULL OR NULLIF(LTRIM(RTRIM(i.[OverrideRuleCode])), N'') IS NULL OR i.[OverrideWorkflowInstanceId] IS NULL OR NULLIF(LTRIM(RTRIM(i.[OverrideApprovalReference])), N'') IS NULL OR NULLIF(LTRIM(RTRIM(i.[OverrideEvidenceReference])), N'') IS NULL OR i.[OverrideApprovedAtUtc] IS NULL))
                           OR (i.[IsOverride] = 0 AND (i.[OverrideRuleId] IS NOT NULL OR i.[OverrideRuleCode] IS NOT NULL OR i.[OverrideWorkflowInstanceId] IS NOT NULL OR i.[OverrideApprovalReference] IS NOT NULL OR i.[OverrideEvidenceReference] IS NOT NULL OR i.[OverrideApprovedAtUtc] IS NOT NULL))
                    )
                    BEGIN
                        THROW 51021, 'Budget commitment tenant, lifecycle, snapshot, or override evidence is invalid.', 1;
                    END;
                END;
                """);

            migrationBuilder.Sql(
                """
                CREATE TRIGGER [dbo].[TR_ProcurementBudgetCommitments_LifecycleGuard]
                ON [dbo].[ProcurementBudgetCommitments]
                AFTER UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS
                    (
                        SELECT 1
                        FROM inserted AS i
                        INNER JOIN deleted AS d ON d.[Id] = i.[Id]
                        WHERE i.[TenantId] <> d.[TenantId]
                           OR i.[PurchaseRequisitionId] <> d.[PurchaseRequisitionId]
                           OR i.[ReservationReference] <> d.[ReservationReference]
                           OR i.[CreatedAt] <> d.[CreatedAt]
                           OR ISNULL(i.[CreatedById], '00000000-0000-0000-0000-000000000000') <> ISNULL(d.[CreatedById], '00000000-0000-0000-0000-000000000000')
                           OR ISNULL(i.[CreatedBy], N'') <> ISNULL(d.[CreatedBy], N'')
                           OR i.[IsDeleted] <> d.[IsDeleted]
                           OR NOT ((d.[Status] = 1 AND i.[Status] IN (2, 3) AND i.[ReservationSequence] = d.[ReservationSequence])
                                OR (d.[Status] = 2 AND i.[Status] = 1 AND i.[ReservationSequence] = d.[ReservationSequence] + 1))
                    )
                    BEGIN
                        THROW 51022, 'Budget commitment identity, retention, or lifecycle transition is immutable.', 1;
                    END;
                END;
                """);

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
                          AND
                          (
                              EXISTS
                              (
                                  SELECT i.[SourcePlanId], i.[SourcePlanItemId], i.[SourcePlanNumber], i.[SourcePlanTitle],
                                         i.[SourcePlanItemDescription], i.[BudgetId], i.[BudgetCode], i.[ProcurementCategory],
                                         i.[CostCenter], i.[ProjectId], i.[ProjectCode], i.[ProjectName], i.[RequisitionType],
                                         i.[SpecificationTemplateId], i.[SpecificationTemplateCode], i.[SpecificationTemplateName],
                                         i.[SpecificationTemplateVersion], i.[ApprovedExceptionRuleId], i.[ApprovedExceptionRuleCode],
                                         i.[ApprovedExceptionName], i.[ExceptionWorkflowInstanceId], i.[ExceptionApprovalReference],
                                         i.[ExceptionEvidenceReference], i.[ExceptionApprovedById], i.[ExceptionApprovedByName],
                                         i.[ExceptionApprovedAtUtc], i.[LinkageRevision], i.[LinkageLastUpdatedAtUtc],
                                         i.[LinkageLastUpdatedById], i.[LinkageLastUpdatedByName]
                                  EXCEPT
                                  SELECT d.[SourcePlanId], d.[SourcePlanItemId], d.[SourcePlanNumber], d.[SourcePlanTitle],
                                         d.[SourcePlanItemDescription], d.[BudgetId], d.[BudgetCode], d.[ProcurementCategory],
                                         d.[CostCenter], d.[ProjectId], d.[ProjectCode], d.[ProjectName], d.[RequisitionType],
                                         d.[SpecificationTemplateId], d.[SpecificationTemplateCode], d.[SpecificationTemplateName],
                                         d.[SpecificationTemplateVersion], d.[ApprovedExceptionRuleId], d.[ApprovedExceptionRuleCode],
                                         d.[ApprovedExceptionName], d.[ExceptionWorkflowInstanceId], d.[ExceptionApprovalReference],
                                         d.[ExceptionEvidenceReference], d.[ExceptionApprovedById], d.[ExceptionApprovedByName],
                                         d.[ExceptionApprovedAtUtc], d.[LinkageRevision], d.[LinkageLastUpdatedAtUtc],
                                         d.[LinkageLastUpdatedById], d.[LinkageLastUpdatedByName]
                              )
                              OR
                              (
                                  EXISTS
                                  (
                                      SELECT i.[BudgetAllocated], i.[BudgetRemaining], i.[BudgetValidated]
                                      EXCEPT
                                      SELECT d.[BudgetAllocated], d.[BudgetRemaining], d.[BudgetValidated]
                                  )
                                  AND NOT (d.[BudgetValidated] = 1 AND i.[BudgetValidated] = 0 AND REPLACE(i.[Status], N' ', N'') IN (N'Draft', N'Rejected', N'Cancelled', N'Canceled'))
                              )
                          )
                    )
                    BEGIN
                        THROW 51020, 'Purchase requisition governance linkage is immutable after Draft except for a controlled budget release.', 1;
                    END;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementBudgetCommitments_LifecycleGuard];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementBudgetCommitments_TenantAndEvidenceGuard];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementBudgetCommitments_NoDelete];");

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

            migrationBuilder.DropTable(
                name: "ProcurementBudgetCommitments");

            migrationBuilder.DropIndex(
                name: "IX_ProcurementBudgets_TenantId_BudgetCode",
                table: "ProcurementBudgets");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ProcurementBudgets");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBudgets_BudgetCode",
                table: "ProcurementBudgets",
                column: "BudgetCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementBudgets_TenantId",
                table: "ProcurementBudgets",
                column: "TenantId");
        }
    }
}
