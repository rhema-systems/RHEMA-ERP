using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProcurementSupplierRiskAndConcentration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProcurementSupplierRiskAssessments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssessmentReference = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AssessmentSequence = table.Column<int>(type: "int", nullable: false),
                    BusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssessedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodStartUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodEndUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NextReviewDueAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PolicyDecisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PolicyProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PolicyProfileCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PolicyProfileVersion = table.Column<int>(type: "int", nullable: false),
                    PolicySnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PolicyValueHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ExposureWindowMonths = table.Column<int>(type: "int", nullable: false),
                    MinimumScore = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ConcentrationLimitPercent = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    RiskScore = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    RiskBand = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    EligibilityAction = table.Column<int>(type: "int", nullable: false),
                    DataComplete = table.Column<bool>(type: "bit", nullable: false),
                    MinimumScoreBreached = table.Column<bool>(type: "bit", nullable: false),
                    ConcentrationBreached = table.Column<bool>(type: "bit", nullable: false),
                    SingleSourceDependency = table.Column<bool>(type: "bit", nullable: false),
                    MaximumSpendSharePercent = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    SingleSourceCategoryCount = table.Column<int>(type: "int", nullable: false),
                    DimensionScoresJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SpendExposureJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CategoryExposureJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EligibilitySnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EligibilityDecisionHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FindingsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SourceType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AssessedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssessedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IntegrityHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_ProcurementSupplierRiskAssessments", x => x.Id);
                    table.CheckConstraint("CK_ProcurementSupplierRiskAssessments_State", "[AssessmentSequence] >= 1 AND [PeriodEndUtc] >= [PeriodStartUtc] AND [NextReviewDueAtUtc] > [AssessedAtUtc] AND [ExposureWindowMonths] BETWEEN 1 AND 120 AND [MinimumScore] BETWEEN 0 AND 100 AND [ConcentrationLimitPercent] BETWEEN 0 AND 100 AND ([RiskScore] IS NULL OR [RiskScore] BETWEEN 0 AND 100) AND [MaximumSpendSharePercent] BETWEEN 0 AND 100 AND [SingleSourceCategoryCount] >= 0 AND [EligibilityAction] BETWEEN 0 AND 2 AND [PolicyProfileVersion] >= 1 AND LEN([PolicyValueHash]) = 64 AND LEN([EligibilityDecisionHash]) = 64 AND LEN([IdempotencyKey]) > 0 AND LEN([CorrelationId]) > 0 AND LEN([IntegrityHash]) = 64 AND ISJSON([PolicySnapshotJson]) = 1 AND ISJSON([DimensionScoresJson]) = 1 AND ISJSON([SpendExposureJson]) = 1 AND ISJSON([CategoryExposureJson]) = 1 AND ISJSON([EligibilitySnapshotJson]) = 1 AND ISJSON([FindingsJson]) = 1 AND ISJSON([SnapshotJson]) = 1");
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierRiskAssessments_BusinessPartners_BusinessPartnerId",
                        column: x => x.BusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierRiskAssessments_ProcurementConfigurationDecisions_PolicyDecisionId",
                        column: x => x.PolicyDecisionId,
                        principalTable: "ProcurementConfigurationDecisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierRiskAssessments_ProcurementConfigurationProfiles_PolicyProfileId",
                        column: x => x.PolicyProfileId,
                        principalTable: "ProcurementConfigurationProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierRiskAssessments_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementSupplierRiskAlerts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssessmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AlertType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RuleCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    OpenedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    WorkflowDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EscalatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EscalatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EscalationReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    EscalationEvidenceJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResolvedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResolvedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolutionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ResolutionEvidenceJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreationCorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastOperationCorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastOperation = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IntegrityHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_ProcurementSupplierRiskAlerts", x => x.Id);
                    table.CheckConstraint("CK_ProcurementSupplierRiskAlerts_State", "[AlertType] BETWEEN 0 AND 3 AND [Status] BETWEEN 0 AND 2 AND LEN([RuleCode]) > 0 AND LEN([Severity]) > 0 AND LEN([Message]) > 0 AND LEN([CreationCorrelationId]) > 0 AND LEN([LastOperationCorrelationId]) > 0 AND LEN([IntegrityHash]) = 64 AND ISJSON([SnapshotJson]) = 1 AND ([EscalationEvidenceJson] IS NULL OR ISJSON([EscalationEvidenceJson]) = 1) AND ([ResolutionEvidenceJson] IS NULL OR ISJSON([ResolutionEvidenceJson]) = 1) AND (([Status] IN (1,2) AND [WorkflowDefinitionId] IS NOT NULL AND [WorkflowInstanceId] IS NOT NULL AND [EscalatedById] IS NOT NULL AND [EscalatedAtUtc] IS NOT NULL AND LEN([EscalationReason]) > 0) OR [Status] = 0) AND (([Status] = 2 AND [ResolvedById] IS NOT NULL AND [ResolvedAtUtc] IS NOT NULL AND LEN([ResolutionReason]) > 0) OR [Status] <> 2)");
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierRiskAlerts_BusinessPartners_BusinessPartnerId",
                        column: x => x.BusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierRiskAlerts_ProcurementSupplierRiskAssessments_AssessmentId",
                        column: x => x.AssessmentId,
                        principalTable: "ProcurementSupplierRiskAssessments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierRiskAlerts_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierRiskAlerts_WorkflowDefinitions_WorkflowDefinitionId",
                        column: x => x.WorkflowDefinitionId,
                        principalTable: "WorkflowDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierRiskAlerts_WorkflowInstances_WorkflowInstanceId",
                        column: x => x.WorkflowInstanceId,
                        principalTable: "WorkflowInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierRiskAlerts_AssessmentId",
                table: "ProcurementSupplierRiskAlerts",
                column: "AssessmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierRiskAlerts_BusinessPartnerId",
                table: "ProcurementSupplierRiskAlerts",
                column: "BusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierRiskAlerts_TenantId_AssessmentId_AlertType",
                table: "ProcurementSupplierRiskAlerts",
                columns: new[] { "TenantId", "AssessmentId", "AlertType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierRiskAlerts_TenantId_BusinessPartnerId_Status",
                table: "ProcurementSupplierRiskAlerts",
                columns: new[] { "TenantId", "BusinessPartnerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierRiskAlerts_TenantId_WorkflowInstanceId",
                table: "ProcurementSupplierRiskAlerts",
                columns: new[] { "TenantId", "WorkflowInstanceId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierRiskAlerts_WorkflowDefinitionId",
                table: "ProcurementSupplierRiskAlerts",
                column: "WorkflowDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierRiskAlerts_WorkflowInstanceId",
                table: "ProcurementSupplierRiskAlerts",
                column: "WorkflowInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierRiskAssessments_BusinessPartnerId",
                table: "ProcurementSupplierRiskAssessments",
                column: "BusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierRiskAssessments_PolicyDecisionId",
                table: "ProcurementSupplierRiskAssessments",
                column: "PolicyDecisionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierRiskAssessments_PolicyProfileId",
                table: "ProcurementSupplierRiskAssessments",
                column: "PolicyProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierRiskAssessments_TenantId_AssessmentReference",
                table: "ProcurementSupplierRiskAssessments",
                columns: new[] { "TenantId", "AssessmentReference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierRiskAssessments_TenantId_BusinessPartnerId_AssessedAtUtc",
                table: "ProcurementSupplierRiskAssessments",
                columns: new[] { "TenantId", "BusinessPartnerId", "AssessedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierRiskAssessments_TenantId_BusinessPartnerId_AssessmentSequence",
                table: "ProcurementSupplierRiskAssessments",
                columns: new[] { "TenantId", "BusinessPartnerId", "AssessmentSequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierRiskAssessments_TenantId_IdempotencyKey",
                table: "ProcurementSupplierRiskAssessments",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierRiskAssessments_TenantId_PolicyDecisionId",
                table: "ProcurementSupplierRiskAssessments",
                columns: new[] { "TenantId", "PolicyDecisionId" });

            migrationBuilder.Sql("""
                CREATE TRIGGER [dbo].[TR_ProcurementSupplierRiskAssessments_AppendOnly]
                ON [dbo].[ProcurementSupplierRiskAssessments]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (SELECT 1 FROM deleted)
                        THROW 51300, 'Supplier-risk assessments are append-only.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN BusinessPartners bp ON bp.Id = i.BusinessPartnerId
                        LEFT JOIN ProcurementConfigurationDecisions pd ON pd.Id = i.PolicyDecisionId
                        LEFT JOIN ProcurementConfigurationProfiles pp ON pp.Id = i.PolicyProfileId
                        WHERE i.IsDeleted <> 0
                           OR bp.Id IS NULL OR bp.TenantId <> i.TenantId
                           OR pd.Id IS NULL OR pd.TenantId <> i.TenantId
                           OR pd.DecisionKey <> 'DEC-011'
                           OR pd.ProfileId <> i.PolicyProfileId
                           OR pp.Id IS NULL OR pp.TenantId <> i.TenantId
                           OR pp.ProfileCode <> i.PolicyProfileCode
                           OR pp.Version <> i.PolicyProfileVersion
                    )
                        THROW 51301, 'Supplier-risk assessment supplier and DEC-011 policy lineage must belong to the same tenant.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER [dbo].[TR_ProcurementSupplierRiskAlerts_Lifecycle]
                ON [dbo].[ProcurementSupplierRiskAlerts]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (
                        SELECT 1
                        FROM deleted d
                        LEFT JOIN inserted i ON i.Id = d.Id
                        WHERE i.Id IS NULL
                    )
                        THROW 51310, 'Supplier-risk alerts cannot be physically deleted.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN ProcurementSupplierRiskAssessments a ON a.Id = i.AssessmentId
                        LEFT JOIN BusinessPartners bp ON bp.Id = i.BusinessPartnerId
                        WHERE a.Id IS NULL OR a.TenantId <> i.TenantId
                           OR a.BusinessPartnerId <> i.BusinessPartnerId
                           OR bp.Id IS NULL OR bp.TenantId <> i.TenantId
                    )
                        THROW 51311, 'Supplier-risk alerts must reference a same-tenant assessment and supplier.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN deleted d ON d.Id = i.Id
                        WHERE d.Id IS NULL
                          AND (
                              i.Status <> 0 OR i.IsDeleted <> 0
                              OR i.WorkflowDefinitionId IS NOT NULL
                              OR i.WorkflowInstanceId IS NOT NULL
                              OR i.EscalatedById IS NOT NULL
                              OR i.EscalatedAtUtc IS NOT NULL
                              OR i.EscalationReason IS NOT NULL
                              OR i.EscalationEvidenceJson IS NOT NULL
                              OR i.ResolvedById IS NOT NULL
                              OR i.ResolvedAtUtc IS NOT NULL
                              OR i.ResolutionReason IS NOT NULL
                              OR i.ResolutionEvidenceJson IS NOT NULL
                          )
                    )
                        THROW 51312, 'Supplier-risk alerts must be created as active Open records without workflow or resolution lineage.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN deleted d ON d.Id = i.Id
                        WHERE NOT (
                            (d.Status = 0 AND i.Status = 1)
                            OR (d.Status = 1 AND i.Status = 2)
                        )
                    )
                        THROW 51313, 'Invalid supplier-risk alert lifecycle transition.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN deleted d ON d.Id = i.Id
                        WHERE i.TenantId <> d.TenantId
                           OR i.AssessmentId <> d.AssessmentId
                           OR i.BusinessPartnerId <> d.BusinessPartnerId
                           OR i.AlertType <> d.AlertType
                           OR i.RuleCode <> d.RuleCode
                           OR i.Severity <> d.Severity
                           OR i.Message <> d.Message
                           OR i.OpenedAtUtc <> d.OpenedAtUtc
                           OR i.CreationCorrelationId <> d.CreationCorrelationId
                           OR i.CreatedAt <> d.CreatedAt
                           OR ISNULL(i.CreatedBy, '') <> ISNULL(d.CreatedBy, '')
                           OR ISNULL(i.CreatedById, '00000000-0000-0000-0000-000000000000') <>
                              ISNULL(d.CreatedById, '00000000-0000-0000-0000-000000000000')
                           OR i.IsDeleted <> d.IsDeleted
                           OR i.DeletedAt IS NOT NULL
                           OR i.DeletedBy IS NOT NULL
                    )
                        THROW 51314, 'Supplier-risk alert identity, finding, creation, and tenant lineage are immutable.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN deleted d ON d.Id = i.Id
                        LEFT JOIN WorkflowDefinitions wd ON wd.Id = i.WorkflowDefinitionId
                        LEFT JOIN WorkflowInstances wi ON wi.Id = i.WorkflowInstanceId
                        WHERE d.Status = 0 AND i.Status = 1
                          AND (
                              i.WorkflowDefinitionId IS NULL
                              OR i.WorkflowInstanceId IS NULL
                              OR wd.Id IS NULL OR wd.TenantId <> i.TenantId
                              OR wi.Id IS NULL OR wi.TenantId <> i.TenantId
                              OR wi.WorkflowDefinitionId <> i.WorkflowDefinitionId
                              OR wi.EntityId <> i.Id
                              OR wi.Status NOT IN (0, 1, 2)
                              OR i.EscalatedById IS NULL
                              OR i.EscalatedAtUtc IS NULL
                              OR LEN(ISNULL(i.EscalationReason, '')) = 0
                              OR i.EscalationEvidenceJson IS NULL
                              OR i.ResolvedById IS NOT NULL
                              OR i.ResolvedAtUtc IS NOT NULL
                              OR i.ResolutionReason IS NOT NULL
                              OR i.ResolutionEvidenceJson IS NOT NULL
                          )
                    )
                        THROW 51315, 'Escalation requires a bound same-tenant shared workflow and evidence lineage.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN deleted d ON d.Id = i.Id
                        WHERE d.Status = 1 AND i.Status = 2
                          AND (
                              ISNULL(i.WorkflowDefinitionId, '00000000-0000-0000-0000-000000000000') <>
                                  ISNULL(d.WorkflowDefinitionId, '00000000-0000-0000-0000-000000000000')
                              OR ISNULL(i.WorkflowInstanceId, '00000000-0000-0000-0000-000000000000') <>
                                  ISNULL(d.WorkflowInstanceId, '00000000-0000-0000-0000-000000000000')
                              OR ISNULL(i.EscalatedById, '00000000-0000-0000-0000-000000000000') <>
                                  ISNULL(d.EscalatedById, '00000000-0000-0000-0000-000000000000')
                              OR ISNULL(i.EscalatedAtUtc, '19000101') <> ISNULL(d.EscalatedAtUtc, '19000101')
                              OR ISNULL(i.EscalationReason, '') <> ISNULL(d.EscalationReason, '')
                              OR ISNULL(i.EscalationEvidenceJson, '') <> ISNULL(d.EscalationEvidenceJson, '')
                          )
                    )
                        THROW 51316, 'Supplier-risk escalation workflow and evidence lineage are immutable.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN deleted d ON d.Id = i.Id
                        LEFT JOIN WorkflowInstances wi ON wi.Id = i.WorkflowInstanceId
                        WHERE d.Status = 1 AND i.Status = 2
                          AND (
                              wi.Id IS NULL OR wi.TenantId <> i.TenantId
                              OR wi.WorkflowDefinitionId <> i.WorkflowDefinitionId
                              OR wi.EntityId <> i.Id
                              OR wi.Status <> 2
                              OR i.ResolvedById IS NULL
                              OR i.ResolvedById = i.EscalatedById
                              OR i.ResolvedAtUtc IS NULL
                              OR LEN(ISNULL(i.ResolutionReason, '')) = 0
                              OR i.ResolutionEvidenceJson IS NULL
                          )
                    )
                        THROW 51317, 'Resolution requires Completed bound workflow approval, evidence, and a different resolver.', 1;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementSupplierRiskAlerts_Lifecycle];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementSupplierRiskAssessments_AppendOnly];");

            migrationBuilder.DropTable(
                name: "ProcurementSupplierRiskAlerts");

            migrationBuilder.DropTable(
                name: "ProcurementSupplierRiskAssessments");
        }
    }
}
