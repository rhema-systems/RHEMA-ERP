using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260730171500_TDC0409WorksCloseoutControl")]
public partial class TDC0409WorksCloseoutControl : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ProcurementWorksCloseoutActions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Sequence = table.Column<int>(type: "int", nullable: false),
                ActionType = table.Column<int>(type: "int", nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                ConfigurationProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ConfigurationProfileVersion = table.Column<int>(type: "int", nullable: false),
                PolicySetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PolicyVersion = table.Column<int>(type: "int", nullable: false),
                AuthorityRuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AuthorityName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                WorkflowDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ProjectHandoverItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ProjectDefectLiabilityCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ProjectFinalAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ProjectPaymentCertificateId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                PerformanceBondRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                EffectiveAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                DefectsLiabilityEndsAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                RequiresIndependentFinanceApproval = table.Column<bool>(type: "bit", nullable: false),
                AmountAutoPosted = table.Column<bool>(type: "bit", nullable: false),
                SubmittedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SubmittedByName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                DecidedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                DecidedByName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                DecisionComment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                SourceSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                SourceSnapshotHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                ReadinessSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
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
                table.PrimaryKey("PK_ProcurementWorksCloseoutActions", x => x.Id);
                table.CheckConstraint("CK_ProcurementWorksCloseoutActions_Sequence", "[Sequence] >= 1");
                table.CheckConstraint("CK_ProcurementWorksCloseoutActions_Type",
                    "[ActionType] >= 0 AND [ActionType] <= 10");
                table.CheckConstraint("CK_ProcurementWorksCloseoutActions_Status",
                    "[Status] >= 0 AND [Status] <= 3");
                table.CheckConstraint("CK_ProcurementWorksCloseoutActions_Hashes",
                    "LEN([SourceSnapshotHash]) = 64 AND LEN([IntegrityHash]) = 64 AND ISJSON([SourceSnapshotJson]) = 1 AND ISJSON([ReadinessSnapshotJson]) = 1");
                table.CheckConstraint("CK_ProcurementWorksCloseoutActions_NoAutoPost", "[AmountAutoPosted] = 0");
                table.CheckConstraint("CK_ProcurementWorksCloseoutActions_FinanceBoundary",
                    "([ActionType] IN (5,8,9) AND [RequiresIndependentFinanceApproval] = 1) OR ([ActionType] NOT IN (5,8,9) AND [RequiresIndependentFinanceApproval] = 0)");
                table.CheckConstraint("CK_ProcurementWorksCloseoutActions_Currency",
                    "([Amount] IS NULL AND [Currency] IS NULL) OR ([Amount] IS NOT NULL AND [Amount] >= 0 AND LEN([Currency]) = 3)");
                table.ForeignKey("FK_ProcurementWorksCloseoutActions_Contracts_ContractId",
                    x => x.ContractId, "Contracts", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_ProcurementWorksCloseoutActions_Projects_ProjectId",
                    x => x.ProjectId, "Projects", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_ProcurementWorksCloseoutActions_WorkflowDefinitions_WorkflowDefinitionId",
                    x => x.WorkflowDefinitionId, "WorkflowDefinitions", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_ProcurementWorksCloseoutActions_WorkflowInstances_WorkflowInstanceId",
                    x => x.WorkflowInstanceId, "WorkflowInstances", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_ProcurementWorksCloseoutActions_ProjectHandoverItems_ProjectHandoverItemId",
                    x => x.ProjectHandoverItemId, "ProjectHandoverItems", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_ProcurementWorksCloseoutActions_ProjectDefectLiabilityCases_ProjectDefectLiabilityCaseId",
                    x => x.ProjectDefectLiabilityCaseId, "ProjectDefectLiabilityCases", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_ProcurementWorksCloseoutActions_ProjectFinalAccounts_ProjectFinalAccountId",
                    x => x.ProjectFinalAccountId, "ProjectFinalAccounts", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_ProcurementWorksCloseoutActions_ProjectPaymentCertificates_ProjectPaymentCertificateId",
                    x => x.ProjectPaymentCertificateId, "ProjectPaymentCertificates", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_ProcurementWorksCloseoutActions_PerformanceBondRequests_PerformanceBondRequestId",
                    x => x.PerformanceBondRequestId, "PerformanceBondRequests", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_ProcurementWorksCloseoutActions_Tenants_TenantId",
                    x => x.TenantId, "Tenants", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "ProcurementWorksCloseoutEvidence",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ActionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RequirementKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                RequirementLabel = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                ReferenceKind = table.Column<int>(type: "int", nullable: false),
                WorkflowEvidenceDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                FileUploadRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                EvidenceReference = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                EvidenceHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
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
                table.PrimaryKey("PK_ProcurementWorksCloseoutEvidence", x => x.Id);
                table.CheckConstraint("CK_ProcurementWorksCloseoutEvidence_Reference",
                    "([ReferenceKind] = 0 AND [WorkflowEvidenceDocumentId] IS NOT NULL AND [FileUploadRecordId] IS NULL) OR ([ReferenceKind] = 1 AND [WorkflowEvidenceDocumentId] IS NULL AND [FileUploadRecordId] IS NOT NULL)");
                table.CheckConstraint("CK_ProcurementWorksCloseoutEvidence_Hash", "LEN([EvidenceHash]) = 64");
                table.ForeignKey("FK_ProcurementWorksCloseoutEvidence_ProcurementWorksCloseoutActions_ActionId",
                    x => x.ActionId, "ProcurementWorksCloseoutActions", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_ProcurementWorksCloseoutEvidence_WorkflowEvidenceDocuments_WorkflowEvidenceDocumentId",
                    x => x.WorkflowEvidenceDocumentId, "WorkflowEvidenceDocuments", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_ProcurementWorksCloseoutEvidence_FileUploadRecords_FileUploadRecordId",
                    x => x.FileUploadRecordId, "FileUploadRecords", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_ProcurementWorksCloseoutEvidence_Tenants_TenantId",
                    x => x.TenantId, "Tenants", "Id", onDelete: ReferentialAction.Restrict);
            });

        CreateIndexes(migrationBuilder);
        CreateGuards(migrationBuilder);
    }

    private static void CreateIndexes(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex("IX_ProcurementWorksCloseoutActions_TenantId_ContractId_Sequence",
            "ProcurementWorksCloseoutActions", new[] { "TenantId", "ContractId", "Sequence" }, unique: true);
        migrationBuilder.CreateIndex("IX_ProcurementWorksCloseoutActions_TenantId_IdempotencyKey",
            "ProcurementWorksCloseoutActions", new[] { "TenantId", "IdempotencyKey" }, unique: true);
        migrationBuilder.CreateIndex("IX_ProcurementWorksCloseoutActions_TenantId_ContractId_ActionType_Status",
            "ProcurementWorksCloseoutActions", new[] { "TenantId", "ContractId", "ActionType", "Status" });
        migrationBuilder.CreateIndex("IX_ProcurementWorksCloseoutActions_ContractId",
            "ProcurementWorksCloseoutActions", "ContractId");
        migrationBuilder.CreateIndex("IX_ProcurementWorksCloseoutActions_ProjectId",
            "ProcurementWorksCloseoutActions", "ProjectId");
        migrationBuilder.CreateIndex("IX_ProcurementWorksCloseoutActions_WorkflowDefinitionId",
            "ProcurementWorksCloseoutActions", "WorkflowDefinitionId");
        migrationBuilder.CreateIndex("IX_ProcurementWorksCloseoutActions_WorkflowInstanceId",
            "ProcurementWorksCloseoutActions", "WorkflowInstanceId");
        migrationBuilder.CreateIndex("IX_ProcurementWorksCloseoutActions_ProjectHandoverItemId",
            "ProcurementWorksCloseoutActions", "ProjectHandoverItemId");
        migrationBuilder.CreateIndex("IX_ProcurementWorksCloseoutActions_ProjectDefectLiabilityCaseId",
            "ProcurementWorksCloseoutActions", "ProjectDefectLiabilityCaseId");
        migrationBuilder.CreateIndex("IX_ProcurementWorksCloseoutActions_ProjectFinalAccountId",
            "ProcurementWorksCloseoutActions", "ProjectFinalAccountId");
        migrationBuilder.CreateIndex("IX_ProcurementWorksCloseoutActions_ProjectPaymentCertificateId",
            "ProcurementWorksCloseoutActions", "ProjectPaymentCertificateId");
        migrationBuilder.CreateIndex("IX_ProcurementWorksCloseoutActions_PerformanceBondRequestId",
            "ProcurementWorksCloseoutActions", "PerformanceBondRequestId");
        migrationBuilder.CreateIndex("IX_ProcurementWorksCloseoutActions_TenantId",
            "ProcurementWorksCloseoutActions", "TenantId");
        migrationBuilder.CreateIndex("IX_ProcurementWorksCloseoutEvidence_TenantId_ActionId_RequirementKey",
            "ProcurementWorksCloseoutEvidence", new[] { "TenantId", "ActionId", "RequirementKey" }, unique: true);
        migrationBuilder.CreateIndex("IX_ProcurementWorksCloseoutEvidence_ActionId",
            "ProcurementWorksCloseoutEvidence", "ActionId");
        migrationBuilder.CreateIndex("IX_ProcurementWorksCloseoutEvidence_WorkflowEvidenceDocumentId",
            "ProcurementWorksCloseoutEvidence", "WorkflowEvidenceDocumentId");
        migrationBuilder.CreateIndex("IX_ProcurementWorksCloseoutEvidence_FileUploadRecordId",
            "ProcurementWorksCloseoutEvidence", "FileUploadRecordId");
        migrationBuilder.CreateIndex("IX_ProcurementWorksCloseoutEvidence_TenantId",
            "ProcurementWorksCloseoutEvidence", "TenantId");
    }

    private static void CreateGuards(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE OR ALTER TRIGGER [TR_ProcurementWorksCloseoutActions_TDC0409Protected]
            ON [ProcurementWorksCloseoutActions]
            AFTER INSERT, UPDATE, DELETE
            AS
            BEGIN
                SET NOCOUNT ON;

                IF EXISTS (SELECT 1 FROM deleted) AND NOT EXISTS (SELECT 1 FROM inserted)
                    THROW 54091, 'TDC-0409 Works closeout actions are immutable and cannot be deleted.', 1;

                IF EXISTS
                (
                    SELECT 1
                    FROM inserted i
                    LEFT JOIN [Contracts] c ON c.[Id] = i.[ContractId] AND c.[TenantId] = i.[TenantId] AND c.[IsDeleted] = 0
                    LEFT JOIN [Projects] p ON p.[Id] = i.[ProjectId] AND p.[TenantId] = i.[TenantId] AND p.[IsDeleted] = 0
                    WHERE c.[Id] IS NULL OR UPPER(LTRIM(RTRIM(c.[ContractType]))) <> 'WORKS'
                       OR p.[Id] IS NULL OR p.[ContractId] <> i.[ContractId]
                       OR i.[AmountAutoPosted] <> 0
                )
                    THROW 54092, 'TDC-0409 requires a same-tenant Works contract/project and forbids automatic Finance posting.', 1;

                IF EXISTS
                (
                    SELECT 1
                    FROM inserted i
                    JOIN deleted d ON d.[Id] = i.[Id]
                    WHERE i.[TenantId] <> d.[TenantId]
                       OR i.[ContractId] <> d.[ContractId]
                       OR i.[ProjectId] <> d.[ProjectId]
                       OR i.[Sequence] <> d.[Sequence]
                       OR i.[ActionType] <> d.[ActionType]
                       OR i.[ConfigurationProfileId] <> d.[ConfigurationProfileId]
                       OR i.[ConfigurationProfileVersion] <> d.[ConfigurationProfileVersion]
                       OR i.[PolicySetId] <> d.[PolicySetId]
                       OR i.[PolicyVersion] <> d.[PolicyVersion]
                       OR i.[AuthorityRuleId] <> d.[AuthorityRuleId]
                       OR i.[WorkflowDefinitionId] <> d.[WorkflowDefinitionId]
                       OR i.[SubmittedById] <> d.[SubmittedById]
                       OR i.[SubmittedAtUtc] <> d.[SubmittedAtUtc]
                       OR i.[IdempotencyKey] <> d.[IdempotencyKey]
                       OR i.[CreatedAt] <> d.[CreatedAt]
                       OR i.[IsDeleted] <> 0
                )
                    THROW 54093, 'TDC-0409 immutable action identity or control lineage cannot be changed.', 1;

                IF EXISTS
                (
                    SELECT 1
                    FROM inserted i
                    JOIN deleted d ON d.[Id] = i.[Id]
                    WHERE NOT
                    (
                        (d.[Status] = 0 AND i.[Status] IN (0,1,2,3))
                        OR (d.[Status] = 3 AND i.[Status] IN (1,3))
                    )
                )
                    THROW 54094, 'TDC-0409 Works closeout status transition is not allowed.', 1;

                IF EXISTS
                (
                    SELECT 1
                    FROM inserted i
                    JOIN deleted d ON d.[Id] = i.[Id]
                    JOIN [Contracts] c ON c.[Id] = i.[ContractId] AND c.[TenantId] = i.[TenantId]
                    LEFT JOIN [WorkflowInstances] wi ON wi.[Id] = i.[WorkflowInstanceId]
                    WHERE d.[Status] <> 1 AND i.[Status] = 1
                      AND
                      (
                          TRY_CONVERT(uniqueidentifier,
                              SESSION_CONTEXT(N'TDC0409_WORKS_CLOSEOUT_ACTION_ID')) IS NULL
                          OR TRY_CONVERT(uniqueidentifier,
                              SESSION_CONTEXT(N'TDC0409_WORKS_CLOSEOUT_ACTION_ID')) <> i.[Id]
                          OR wi.[Id] IS NULL
                          OR wi.[TenantId] <> i.[TenantId]
                          OR wi.[EntityId] <> i.[Id]
                          OR wi.[WorkflowDefinitionId] <> i.[WorkflowDefinitionId]
                          OR wi.[Status] <> 2
                          OR i.[DecidedById] IS NULL
                          OR i.[DecidedById] = i.[SubmittedById]
                          OR i.[DecidedById] = c.[CreatedById]
                      )
                )
                    THROW 54095, 'TDC-0409 approval requires the exact completed workflow, an independent actor, and the protected transaction context.', 1;
            END
            """);

        migrationBuilder.Sql(
            """
            CREATE OR ALTER TRIGGER [TR_ProcurementWorksCloseoutEvidence_TDC0409Immutable]
            ON [ProcurementWorksCloseoutEvidence]
            AFTER INSERT, UPDATE, DELETE
            AS
            BEGIN
                SET NOCOUNT ON;

                IF EXISTS (SELECT 1 FROM deleted)
                    THROW 54096, 'TDC-0409 Works closeout evidence is append-only.', 1;

                IF EXISTS
                (
                    SELECT 1
                    FROM inserted i
                    LEFT JOIN [ProcurementWorksCloseoutActions] a
                      ON a.[Id] = i.[ActionId] AND a.[TenantId] = i.[TenantId] AND a.[IsDeleted] = 0
                    LEFT JOIN [WorkflowEvidenceDocuments] wed
                      ON wed.[Id] = i.[WorkflowEvidenceDocumentId] AND wed.[TenantId] = i.[TenantId] AND wed.[IsDeleted] = 0
                    LEFT JOIN [FileUploadRecords] fur
                      ON fur.[Id] = i.[FileUploadRecordId] AND fur.[TenantId] = i.[TenantId] AND fur.[IsDeleted] = 0
                    WHERE a.[Id] IS NULL
                       OR (i.[ReferenceKind] = 0 AND wed.[Id] IS NULL)
                       OR (i.[ReferenceKind] = 1 AND fur.[Id] IS NULL)
                )
                    THROW 54097, 'TDC-0409 evidence must reference a current same-tenant controlled record.', 1;
            END
            """);

        migrationBuilder.Sql(
            """
            CREATE OR ALTER TRIGGER [TR_Contracts_TDC0409CloseoutGuard]
            ON [Contracts]
            AFTER UPDATE
            AS
            BEGIN
                SET NOCOUNT ON;

                IF EXISTS
                (
                    SELECT 1
                    FROM inserted i
                    JOIN deleted d ON d.[Id] = i.[Id]
                    WHERE UPPER(LTRIM(RTRIM(i.[ContractType]))) = 'WORKS'
                      AND UPPER(ISNULL(i.[Status], '')) <> UPPER(ISNULL(d.[Status], ''))
                      AND UPPER(ISNULL(d.[Status], '')) IN ('COMPLETED', 'TERMINATED')
                )
                    THROW 54098, 'TDC-0409 terminal Works contracts cannot be reopened by direct mutation.', 1;

                IF EXISTS
                (
                    SELECT 1
                    FROM inserted i
                    JOIN deleted d ON d.[Id] = i.[Id]
                    LEFT JOIN [ProcurementWorksCloseoutActions] a
                      ON a.[Id] = TRY_CONVERT(uniqueidentifier,
                          SESSION_CONTEXT(N'TDC0409_WORKS_CLOSEOUT_ACTION_ID'))
                     AND a.[TenantId] = i.[TenantId]
                     AND a.[ContractId] = i.[Id]
                     AND a.[Status] = 1
                     AND a.[IsDeleted] = 0
                    WHERE UPPER(LTRIM(RTRIM(i.[ContractType]))) = 'WORKS'
                      AND UPPER(ISNULL(i.[Status], '')) <> UPPER(ISNULL(d.[Status], ''))
                      AND UPPER(ISNULL(i.[Status], '')) IN ('COMPLETED', 'TERMINATED')
                      AND
                      (
                          a.[Id] IS NULL
                          OR (UPPER(i.[Status]) = 'COMPLETED' AND a.[ActionType] <> 10)
                          OR (UPPER(i.[Status]) = 'TERMINATED' AND a.[ActionType] <> 8)
                      )
                )
                    THROW 54099, 'TDC-0409 terminal Works status requires the matching approved closeout action in the same transaction.', 1;
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [TR_Contracts_TDC0409CloseoutGuard];");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [TR_ProcurementWorksCloseoutEvidence_TDC0409Immutable];");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [TR_ProcurementWorksCloseoutActions_TDC0409Protected];");
        migrationBuilder.DropTable(name: "ProcurementWorksCloseoutEvidence");
        migrationBuilder.DropTable(name: "ProcurementWorksCloseoutActions");
    }
}
