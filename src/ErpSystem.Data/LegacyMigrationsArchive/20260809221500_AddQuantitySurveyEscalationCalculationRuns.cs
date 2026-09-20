using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260809221500_AddQuantitySurveyEscalationCalculationRuns")]
public partial class AddQuantitySurveyEscalationCalculationRuns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "QuantitySurveyEscalationCalculationRuns",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RunReference = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                ClientRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RequestHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                FormulaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FormulaKeySnapshot = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FormulaVersionSnapshot = table.Column<int>(type: "int", nullable: false),
                FormulaCodeSnapshot = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ContractNumberSnapshot = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                BaseIndexPeriod = table.Column<DateTime>(type: "datetime2", nullable: false),
                CurrentIndexPeriod = table.Column<DateTime>(type: "datetime2", nullable: false),
                CalculationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                ImpactTargetType = table.Column<int>(type: "int", nullable: false),
                PaymentCertificateId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                FinalAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ImpactTargetReferenceSnapshot = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                ImpactTargetStatusSnapshot = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                ImpactTargetSnapshotHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                CurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                BaseRate = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                RevisedRate = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                AdjustmentFactor = table.Column<decimal>(type: "decimal(18,12)", nullable: false),
                CalculatedFluctuationAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                ReviewerAdjustmentAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                ReviewerAdjustmentReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                ReviewedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                ApprovedImpactAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                ImpactApplicationStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                AuthorityRoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AuthorityRoleNameSnapshot = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                ConfigurationProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ConfigurationDecisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ApprovalWorkflowDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                SnapshotHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                ApprovalStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                PreparedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PreparedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                SubmittedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                RejectionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                AuditAction = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                ChangeReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                ActorRoles = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                table.PrimaryKey("PK_QuantitySurveyEscalationCalculationRuns", x => x.Id);
                table.CheckConstraint("CK_QsEscalationCalculationRuns_TargetType", "[ImpactTargetType] IN (0,1)");
                table.CheckConstraint("CK_QsEscalationCalculationRuns_Target", "([ImpactTargetType] = 0 AND [PaymentCertificateId] IS NOT NULL AND [FinalAccountId] IS NULL) OR ([ImpactTargetType] = 1 AND [PaymentCertificateId] IS NULL AND [FinalAccountId] IS NOT NULL)");
                table.CheckConstraint("CK_QsEscalationCalculationRuns_Period", "DAY([BaseIndexPeriod]) = 1 AND DAY([CurrentIndexPeriod]) = 1 AND [CurrentIndexPeriod] >= [BaseIndexPeriod]");
                table.CheckConstraint("CK_QsEscalationCalculationRuns_Amounts", "[BaseRate] > 0 AND [RevisedRate] >= 0 AND [AdjustmentFactor] > 0 AND [ApprovedImpactAmount] = [CalculatedFluctuationAmount] + [ReviewerAdjustmentAmount]");
                table.CheckConstraint("CK_QsEscalationCalculationRuns_Status", "[Status] IN ('Draft','PendingApproval','ApprovedPendingApplication','Rejected')");
                table.CheckConstraint("CK_QsEscalationCalculationRuns_Approval", "[ApprovalStatus] IN ('Draft','Pending','Approved','Rejected')");
                table.CheckConstraint("CK_QsEscalationCalculationRuns_Application", "[ImpactApplicationStatus] IN ('Projected','PendingApplication','Applied','ApplicationFailed')");
                table.CheckConstraint("CK_QsEscalationCalculationRuns_Lifecycle", "([Status] = 'Draft' AND [ApprovalStatus] = 'Draft') OR ([Status] = 'PendingApproval' AND [ApprovalStatus] = 'Pending' AND [SubmittedById] IS NOT NULL AND [SubmittedAt] IS NOT NULL) OR ([Status] = 'ApprovedPendingApplication' AND [ApprovalStatus] = 'Approved' AND [ApprovedById] IS NOT NULL AND [ApprovedAt] IS NOT NULL AND [ImpactApplicationStatus] IN ('PendingApplication','Applied','ApplicationFailed')) OR ([Status] = 'Rejected' AND [ApprovalStatus] = 'Rejected' AND [RejectionReason] IS NOT NULL)");
                table.ForeignKey("FK_QsEscalationRuns_AspNetRoles_AuthorityRoleId", x => x.AuthorityRoleId, "AspNetRoles", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QsEscalationRuns_WorkflowDefinitions_ApprovalWorkflowDefinitionId", x => x.ApprovalWorkflowDefinitionId, "WorkflowDefinitions", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QsEscalationRuns_QsDecisions_ConfigurationDecisionId", x => x.ConfigurationDecisionId, "QuantitySurveyConfigurationDecisions", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QsEscalationRuns_QsProfiles_ConfigurationProfileId", x => x.ConfigurationProfileId, "QuantitySurveyConfigurationProfiles", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QsEscalationRuns_Contracts_ContractId", x => x.ContractId, "Contracts", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QsEscalationRuns_ProjectFinalAccounts_FinalAccountId", x => x.FinalAccountId, "ProjectFinalAccounts", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QsEscalationRuns_QsFormulas_FormulaId", x => x.FormulaId, "QuantitySurveyEscalationFormulas", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QsEscalationRuns_ProjectCertificates_PaymentCertificateId", x => x.PaymentCertificateId, "ProjectPaymentCertificates", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QsEscalationRuns_Projects_ProjectId", x => x.ProjectId, "Projects", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QsEscalationRuns_Tenants_TenantId", x => x.TenantId, "Tenants", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "QuantitySurveyEscalationCalculationLines",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CalculationRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Sequence = table.Column<int>(type: "int", nullable: false),
                Component = table.Column<int>(type: "int", nullable: false),
                Coefficient = table.Column<decimal>(type: "decimal(9,4)", nullable: false),
                IndexFamilyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                IndexFamilyCodeSnapshot = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                BaseIndexValueId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CurrentIndexValueId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                BaseIndexValue = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                CurrentIndexValue = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                IndexRatio = table.Column<decimal>(type: "decimal(18,12)", nullable: false),
                WeightedContribution = table.Column<decimal>(type: "decimal(18,12)", nullable: false),
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
                table.PrimaryKey("PK_QuantitySurveyEscalationCalculationLines", x => x.Id);
                table.CheckConstraint("CK_QsEscalationCalculationLines_Sequence", "[Sequence] BETWEEN 1 AND 4");
                table.CheckConstraint("CK_QsEscalationCalculationLines_Component", "[Component] IN (0,1,2,3)");
                table.CheckConstraint("CK_QsEscalationCalculationLines_Coefficient", "[Coefficient] >= 0 AND [Coefficient] <= 100");
                table.CheckConstraint("CK_QsEscalationCalculationLines_Indices", "[BaseIndexValue] > 0 AND [CurrentIndexValue] > 0 AND [IndexRatio] > 0 AND [WeightedContribution] >= 0");
                table.ForeignKey("FK_QsEscalationLines_QsRuns_CalculationRunId", x => x.CalculationRunId, "QuantitySurveyEscalationCalculationRuns", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QsEscalationLines_QsIndexFamilies_IndexFamilyId", x => x.IndexFamilyId, "QuantitySurveyPriceIndexFamilies", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QsEscalationLines_QsBaseIndexValues_BaseIndexValueId", x => x.BaseIndexValueId, "QuantitySurveyPriceIndexValues", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QsEscalationLines_QsCurrentIndexValues_CurrentIndexValueId", x => x.CurrentIndexValueId, "QuantitySurveyPriceIndexValues", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QsEscalationLines_Tenants_TenantId", x => x.TenantId, "Tenants", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "QuantitySurveyEscalationCalculationRevisions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CalculationRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ActorName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                ActorRoles = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                BeforeJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                AfterJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                table.PrimaryKey("PK_QuantitySurveyEscalationCalculationRevisions", x => x.Id);
                table.ForeignKey("FK_QsEscalationRevisions_QsRuns_CalculationRunId", x => x.CalculationRunId, "QuantitySurveyEscalationCalculationRuns", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QsEscalationRevisions_Tenants_TenantId", x => x.TenantId, "Tenants", "Id", onDelete: ReferentialAction.Restrict);
            });

        CreateIndexes(migrationBuilder);
        CreateGuards(migrationBuilder);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [TR_QsEscalationCalculationRevisions_Guard];");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [TR_QsEscalationCalculationLines_Guard];");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [TR_QsEscalationCalculationRuns_Guard];");
        migrationBuilder.DropTable(name: "QuantitySurveyEscalationCalculationRevisions");
        migrationBuilder.DropTable(name: "QuantitySurveyEscalationCalculationLines");
        migrationBuilder.DropTable(name: "QuantitySurveyEscalationCalculationRuns");
    }

    private static void CreateIndexes(MigrationBuilder migrationBuilder)
    {
        foreach (var column in new[] { "AuthorityRoleId", "ApprovalWorkflowDefinitionId", "ConfigurationDecisionId", "ConfigurationProfileId", "ContractId", "FinalAccountId", "FormulaId", "PaymentCertificateId", "ProjectId" })
            migrationBuilder.CreateIndex(name: $"IX_QuantitySurveyEscalationCalculationRuns_{column}", table: "QuantitySurveyEscalationCalculationRuns", column: column);
        migrationBuilder.CreateIndex(name: "IX_QsEscalationRuns_Tenant_ClientRequest", table: "QuantitySurveyEscalationCalculationRuns", columns: new[] { "TenantId", "ClientRequestId" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_QsEscalationRuns_Tenant_Reference", table: "QuantitySurveyEscalationCalculationRuns", columns: new[] { "TenantId", "RunReference" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_QsEscalationRuns_Tenant_Project_Status_Prepared", table: "QuantitySurveyEscalationCalculationRuns", columns: new[] { "TenantId", "ProjectId", "Status", "PreparedAt" });
        migrationBuilder.CreateIndex(name: "IX_QsEscalationRuns_Tenant_Contract_Status", table: "QuantitySurveyEscalationCalculationRuns", columns: new[] { "TenantId", "ContractId", "Status" });
        migrationBuilder.CreateIndex(name: "IX_QsEscalationRuns_Tenant_Formula_Status", table: "QuantitySurveyEscalationCalculationRuns", columns: new[] { "TenantId", "FormulaId", "Status" });

        foreach (var column in new[] { "CalculationRunId", "IndexFamilyId", "BaseIndexValueId", "CurrentIndexValueId" })
            migrationBuilder.CreateIndex(name: $"IX_QuantitySurveyEscalationCalculationLines_{column}", table: "QuantitySurveyEscalationCalculationLines", column: column);
        migrationBuilder.CreateIndex(name: "IX_QsEscalationLines_Tenant_Run_Sequence", table: "QuantitySurveyEscalationCalculationLines", columns: new[] { "TenantId", "CalculationRunId", "Sequence" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_QsEscalationLines_Tenant_Run_Component", table: "QuantitySurveyEscalationCalculationLines", columns: new[] { "TenantId", "CalculationRunId", "Component" }, unique: true);

        migrationBuilder.CreateIndex(name: "IX_QuantitySurveyEscalationCalculationRevisions_CalculationRunId", table: "QuantitySurveyEscalationCalculationRevisions", column: "CalculationRunId");
        migrationBuilder.CreateIndex(name: "IX_QsEscalationRevisions_Tenant_Run_Created", table: "QuantitySurveyEscalationCalculationRevisions", columns: new[] { "TenantId", "CalculationRunId", "CreatedAt" });
        migrationBuilder.CreateIndex(name: "IX_QsEscalationRevisions_Tenant_Correlation", table: "QuantitySurveyEscalationCalculationRevisions", columns: new[] { "TenantId", "CorrelationId" });
    }

    private static void CreateGuards(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TRIGGER [TR_QsEscalationCalculationRuns_Guard]
            ON [QuantitySurveyEscalationCalculationRuns]
            AFTER INSERT, UPDATE, DELETE
            AS
            BEGIN
                SET NOCOUNT ON;

                IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.[Id] = d.[Id] WHERE i.[Id] IS NULL)
                    THROW 51060, 'Escalation calculation runs cannot be deleted; retain their governed history.', 1;

                IF EXISTS (
                    SELECT 1 FROM inserted i
                    LEFT JOIN deleted d ON d.[Id] = i.[Id]
                    WHERE (d.[Id] IS NULL AND (i.[Status] <> 'Draft' OR i.[ApprovalStatus] <> 'Draft' OR i.[IsDeleted] = 1))
                       OR (d.[Id] IS NOT NULL AND NOT (
                              (d.[Status] = i.[Status] AND d.[Status] IN ('PendingApproval','ApprovedPendingApplication'))
                           OR (d.[Status] = 'Draft' AND i.[Status] IN ('PendingApproval','Rejected'))
                           OR (d.[Status] = 'Rejected' AND i.[Status] = 'PendingApproval')
                           OR (d.[Status] = 'PendingApproval' AND i.[Status] IN ('ApprovedPendingApplication','Rejected')))))
                    THROW 51060, 'Invalid escalation calculation lifecycle transition.', 1;

                IF EXISTS (
                    SELECT 1 FROM inserted i
                    JOIN deleted d ON d.[Id] = i.[Id]
                    WHERE EXISTS (
                        SELECT d.[TenantId],d.[RunReference],d.[ClientRequestId],d.[RequestHash],d.[FormulaId],d.[FormulaKeySnapshot],d.[FormulaVersionSnapshot],d.[FormulaCodeSnapshot],
                               d.[ProjectId],d.[ContractId],d.[ContractNumberSnapshot],d.[BaseIndexPeriod],d.[CurrentIndexPeriod],d.[CalculationDate],d.[ImpactTargetType],
                               d.[PaymentCertificateId],d.[FinalAccountId],d.[ImpactTargetReferenceSnapshot],d.[ImpactTargetStatusSnapshot],d.[ImpactTargetSnapshotHash],d.[CurrencyCode],
                               d.[BaseRate],d.[RevisedRate],d.[AdjustmentFactor],d.[CalculatedFluctuationAmount],d.[AuthorityRoleId],d.[AuthorityRoleNameSnapshot],
                               d.[ConfigurationProfileId],d.[ConfigurationDecisionId],d.[ApprovalWorkflowDefinitionId],d.[PreparedById],d.[PreparedAt],d.[CreatedAt],d.[CreatedById],d.[IsDeleted]
                        EXCEPT
                        SELECT i.[TenantId],i.[RunReference],i.[ClientRequestId],i.[RequestHash],i.[FormulaId],i.[FormulaKeySnapshot],i.[FormulaVersionSnapshot],i.[FormulaCodeSnapshot],
                               i.[ProjectId],i.[ContractId],i.[ContractNumberSnapshot],i.[BaseIndexPeriod],i.[CurrentIndexPeriod],i.[CalculationDate],i.[ImpactTargetType],
                               i.[PaymentCertificateId],i.[FinalAccountId],i.[ImpactTargetReferenceSnapshot],i.[ImpactTargetStatusSnapshot],i.[ImpactTargetSnapshotHash],i.[CurrencyCode],
                               i.[BaseRate],i.[RevisedRate],i.[AdjustmentFactor],i.[CalculatedFluctuationAmount],i.[AuthorityRoleId],i.[AuthorityRoleNameSnapshot],
                               i.[ConfigurationProfileId],i.[ConfigurationDecisionId],i.[ApprovalWorkflowDefinitionId],i.[PreparedById],i.[PreparedAt],i.[CreatedAt],i.[CreatedById],i.[IsDeleted]))
                    THROW 51061, 'Escalation calculation source, target, formula, policy, authority and preparer lineage is immutable.', 1;

                IF EXISTS (
                    SELECT 1 FROM inserted i
                    WHERE (i.[ReviewedById] IS NOT NULL AND i.[ReviewedById] = i.[PreparedById])
                       OR (i.[ApprovedById] IS NOT NULL AND i.[ApprovedById] = i.[PreparedById])
                       OR NOT EXISTS (
                            SELECT 1 FROM [QuantitySurveyEscalationFormulas] f
                            WHERE f.[Id] = i.[FormulaId] AND f.[TenantId] = i.[TenantId]
                              AND f.[FormulaKey] = i.[FormulaKeySnapshot] AND f.[Version] = i.[FormulaVersionSnapshot]
                              AND f.[Code] = i.[FormulaCodeSnapshot] AND f.[ProjectId] = i.[ProjectId]
                              AND f.[ContractId] = i.[ContractId] AND f.[AuthorityRoleId] = i.[AuthorityRoleId]
                              AND f.[ConfigurationProfileId] = i.[ConfigurationProfileId]
                              AND f.[ConfigurationDecisionId] = i.[ConfigurationDecisionId]
                              AND f.[ApprovalWorkflowDefinitionId] = i.[ApprovalWorkflowDefinitionId]
                              AND f.[Status] = 'Approved' AND f.[ApprovalStatus] = 'Approved'
                              AND f.[EffectiveFrom] <= i.[CurrentIndexPeriod]
                              AND (f.[EffectiveTo] IS NULL OR f.[EffectiveTo] >= i.[CurrentIndexPeriod])
                              AND f.[IsDeleted] = 0)
                       OR NOT EXISTS (SELECT 1 FROM [Projects] p WHERE p.[Id] = i.[ProjectId] AND p.[TenantId] = i.[TenantId] AND p.[IsDeleted] = 0)
                       OR NOT EXISTS (SELECT 1 FROM [Contracts] c WHERE c.[Id] = i.[ContractId] AND c.[TenantId] = i.[TenantId] AND c.[ContractNumber] = i.[ContractNumberSnapshot] AND c.[Currency] = i.[CurrencyCode] AND c.[IsDeleted] = 0)
                       OR NOT EXISTS (SELECT 1 FROM [AspNetRoles] r WHERE r.[Id] = i.[AuthorityRoleId] AND r.[Name] = i.[AuthorityRoleNameSnapshot])
                       OR NOT EXISTS (SELECT 1 FROM [QuantitySurveyConfigurationProfiles] p WHERE p.[Id] = i.[ConfigurationProfileId] AND p.[TenantId] = i.[TenantId] AND p.[LifecycleStatus] = 1 AND p.[EffectiveFrom] <= i.[CalculationDate] AND (p.[EffectiveTo] IS NULL OR p.[EffectiveTo] >= i.[CalculationDate]) AND p.[IsDeleted] = 0)
                       OR NOT EXISTS (SELECT 1 FROM [QuantitySurveyConfigurationDecisions] d WHERE d.[Id] = i.[ConfigurationDecisionId] AND d.[ProfileId] = i.[ConfigurationProfileId] AND d.[TenantId] = i.[TenantId] AND d.[DecisionKey] = 'QS-DEC-006' AND d.[Status] = 2 AND d.[ApprovalStatus] = 1 AND d.[EvidenceStatus] = 2 AND (d.[EffectiveFrom] IS NULL OR d.[EffectiveFrom] <= i.[CalculationDate]) AND (d.[EffectiveTo] IS NULL OR d.[EffectiveTo] >= i.[CalculationDate]) AND TRY_CONVERT(uniqueidentifier, JSON_VALUE(d.[ValueJson], '$.approvalWorkflowDefinitionId')) = i.[ApprovalWorkflowDefinitionId] AND d.[IsDeleted] = 0)
                       OR NOT EXISTS (SELECT 1 FROM [WorkflowDefinitions] w JOIN [WorkflowEntityTypes] wet ON wet.[Id] = w.[EntityTypeId] WHERE w.[Id] = i.[ApprovalWorkflowDefinitionId] AND w.[TenantId] = i.[TenantId] AND w.[LifecycleStatus] = 1 AND w.[IsActive] = 1 AND w.[IsDeleted] = 0 AND wet.[TenantId] = i.[TenantId] AND wet.[Code] = 'QS_ESCALATION' AND wet.[IsActive] = 1 AND wet.[IsDeleted] = 0)
                       OR (i.[ImpactTargetType] = 0 AND NOT EXISTS (SELECT 1 FROM [ProjectPaymentCertificates] t WHERE t.[Id] = i.[PaymentCertificateId] AND t.[TenantId] = i.[TenantId] AND t.[ProjectId] = i.[ProjectId] AND t.[ContractId] = i.[ContractId] AND t.[Status] = i.[ImpactTargetStatusSnapshot] AND t.[Status] IN ('Draft','Issued') AND t.[Currency] = i.[CurrencyCode] AND t.[IsDeleted] = 0))
                       OR (i.[ImpactTargetType] = 1 AND NOT EXISTS (SELECT 1 FROM [ProjectFinalAccounts] t WHERE t.[Id] = i.[FinalAccountId] AND t.[TenantId] = i.[TenantId] AND t.[ProjectId] = i.[ProjectId] AND t.[ContractId] = i.[ContractId] AND t.[Status] = i.[ImpactTargetStatusSnapshot] AND t.[Status] IN ('Draft','UnderReview') AND t.[Currency] = i.[CurrencyCode] AND t.[IsDeleted] = 0)))
                    THROW 51061, 'Escalation calculation tenant, policy, workflow, formula or impact-target lineage is invalid.', 1;
            END
            """);

        migrationBuilder.Sql("""
            CREATE TRIGGER [TR_QsEscalationCalculationLines_Guard]
            ON [QuantitySurveyEscalationCalculationLines]
            AFTER INSERT, UPDATE, DELETE
            AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (SELECT 1 FROM deleted)
                    THROW 51062, 'Escalation calculation component snapshots are immutable.', 1;
                IF EXISTS (
                    SELECT 1 FROM inserted i
                    JOIN [QuantitySurveyEscalationCalculationRuns] r ON r.[Id] = i.[CalculationRunId]
                    WHERE r.[TenantId] <> i.[TenantId] OR r.[Status] <> 'Draft'
                       OR NOT EXISTS (SELECT 1 FROM [QuantitySurveyPriceIndexFamilies] f WHERE f.[Id] = i.[IndexFamilyId] AND f.[TenantId] = i.[TenantId] AND f.[Code] = i.[IndexFamilyCodeSnapshot] AND f.[IsActive] = 1 AND f.[IsDeleted] = 0)
                       OR NOT EXISTS (SELECT 1 FROM [QuantitySurveyPriceIndexValues] v WHERE v.[Id] = i.[BaseIndexValueId] AND v.[IndexFamilyId] = i.[IndexFamilyId] AND v.[TenantId] = i.[TenantId] AND v.[IndexPeriod] = r.[BaseIndexPeriod] AND v.[IndexValue] = i.[BaseIndexValue] AND v.[Status] = 'Approved' AND v.[IsCurrent] = 1 AND v.[IsDeleted] = 0)
                       OR NOT EXISTS (SELECT 1 FROM [QuantitySurveyPriceIndexValues] v WHERE v.[Id] = i.[CurrentIndexValueId] AND v.[IndexFamilyId] = i.[IndexFamilyId] AND v.[TenantId] = i.[TenantId] AND v.[IndexPeriod] = r.[CurrentIndexPeriod] AND v.[IndexValue] = i.[CurrentIndexValue] AND v.[Status] = 'Approved' AND v.[IsCurrent] = 1 AND v.[IsDeleted] = 0))
                    THROW 51062, 'Escalation calculation component tenant or approved-index lineage is invalid.', 1;
            END
            """);

        migrationBuilder.Sql("""
            CREATE TRIGGER [TR_QsEscalationCalculationRevisions_Guard]
            ON [QuantitySurveyEscalationCalculationRevisions]
            AFTER INSERT, UPDATE, DELETE
            AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (SELECT 1 FROM deleted)
                    THROW 51063, 'Escalation calculation revision history is append-only.', 1;
                IF EXISTS (
                    SELECT 1 FROM inserted i
                    LEFT JOIN [QuantitySurveyEscalationCalculationRuns] r ON r.[Id] = i.[CalculationRunId] AND r.[TenantId] = i.[TenantId]
                    WHERE r.[Id] IS NULL OR i.[IsDeleted] = 1)
                    THROW 51063, 'Escalation calculation revision tenant lineage is invalid.', 1;
            END
            """);
    }
}
