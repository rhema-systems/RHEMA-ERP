using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddQuantitySurveyFinalAccountLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProjectFinalAccounts_TenantId",
                table: "ProjectFinalAccounts");

            migrationBuilder.AddColumn<decimal>(
                name: "AdvanceRecoveryAmount",
                table: "ProjectFinalAccounts",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ApprovalStatus",
                table: "ProjectFinalAccounts",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovalWorkflowDefinitionId",
                table: "ProjectFinalAccounts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "ProjectFinalAccounts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovedBoqSnapshotHash",
                table: "ProjectFinalAccounts",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ApprovedBoqValue",
                table: "ProjectFinalAccounts",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedBoqVersionId",
                table: "ProjectFinalAccounts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedById",
                table: "ProjectFinalAccounts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ApprovedClaimAmount",
                table: "ProjectFinalAccounts",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ApprovedEscalationAmount",
                table: "ProjectFinalAccounts",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "ClientRequestId",
                table: "ProjectFinalAccounts",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedAt",
                table: "ProjectFinalAccounts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ClosedById",
                table: "ProjectFinalAccounts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClosureReason",
                table: "ProjectFinalAccounts",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConfigurationProfileId",
                table: "ProjectFinalAccounts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ContractControlsDecisionId",
                table: "ProjectFinalAccounts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CorrelationId",
                table: "ProjectFinalAccounts",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastMutationClientRequestId",
                table: "ProjectFinalAccounts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastMutationRequestHash",
                table: "ProjectFinalAccounts",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MaterialDeductionAmount",
                table: "ProjectFinalAccounts",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OtherDeductionAmount",
                table: "ProjectFinalAccounts",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PaidToDateAmount",
                table: "ProjectFinalAccounts",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "PolicyHash",
                table: "ProjectFinalAccounts",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PreparedAt",
                table: "ProjectFinalAccounts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PreparedById",
                table: "ProjectFinalAccounts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReconciliationHash",
                table: "ProjectFinalAccounts",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReconciliationJson",
                table: "ProjectFinalAccounts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "ProjectFinalAccounts",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestHash",
                table: "ProjectFinalAccounts",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ProjectFinalAccounts",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedAt",
                table: "ProjectFinalAccounts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SubmittedById",
                table: "ProjectFinalAccounts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WorkflowInstanceId",
                table: "ProjectFinalAccounts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProjectFinalAccountRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectFinalAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ActorRoles = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_ProjectFinalAccountRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectFinalAccountRevisions_ProjectFinalAccounts_ProjectFinalAccountId",
                        column: x => x.ProjectFinalAccountId,
                        principalTable: "ProjectFinalAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectFinalAccountRevisions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectFinalAccounts_ApprovedBoqVersionId",
                table: "ProjectFinalAccounts",
                column: "ApprovedBoqVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectFinalAccounts_TenantId_ApprovalWorkflowDefinitionId",
                table: "ProjectFinalAccounts",
                columns: new[] { "TenantId", "ApprovalWorkflowDefinitionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectFinalAccounts_TenantId_ApprovedBoqVersionId",
                table: "ProjectFinalAccounts",
                columns: new[] { "TenantId", "ApprovedBoqVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectFinalAccounts_TenantId_ClientRequestId",
                table: "ProjectFinalAccounts",
                columns: new[] { "TenantId", "ClientRequestId" },
                unique: true,
                filter: "[ClientRequestId] <> '00000000-0000-0000-0000-000000000000' AND [IsDeleted] = 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProjectFinalAccounts_QS0506Amounts",
                table: "ProjectFinalAccounts",
                sql: "[OriginalContractValue] >= 0 AND [ApprovedBoqValue] >= 0 AND [ApprovedVariationAmount] >= 0 AND [ApprovedClaimAmount] >= 0 AND [ApprovedEscalationAmount] >= 0 AND [CertifiedToDate] >= 0 AND [RetentionHeldAmount] >= 0 AND [RetentionReleasedAmount] >= 0 AND [RetentionReleasedAmount] <= [RetentionHeldAmount] AND [AdvanceRecoveryAmount] >= 0 AND [MaterialDeductionAmount] >= 0 AND [OtherDeductionAmount] >= 0 AND [PaidToDateAmount] >= 0 AND [FinalAccountValue] >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectFinalAccountRevisions_ProjectFinalAccountId_CreatedAt",
                table: "ProjectFinalAccountRevisions",
                columns: new[] { "ProjectFinalAccountId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectFinalAccountRevisions_TenantId_Action_CreatedAt",
                table: "ProjectFinalAccountRevisions",
                columns: new[] { "TenantId", "Action", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectFinalAccountRevisions_TenantId_ClientRequestId",
                table: "ProjectFinalAccountRevisions",
                columns: new[] { "TenantId", "ClientRequestId" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProjectFinalAccountRevisions_QS0506Request",
                table: "ProjectFinalAccountRevisions",
                sql: "[ClientRequestId] <> '00000000-0000-0000-0000-000000000000' AND LEN([RequestHash]) = 64");

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectFinalAccounts_ProjectBoqVersions_ApprovedBoqVersionId",
                table: "ProjectFinalAccounts",
                column: "ApprovedBoqVersionId",
                principalTable: "ProjectBoqVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [dbo].[TR_ProjectFinalAccounts_QS0506Guard]
                ON [dbo].[ProjectFinalAccounts]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (
                        SELECT 1
                        FROM deleted d
                        LEFT JOIN inserted i ON i.[Id] = d.[Id]
                        WHERE d.[ConfigurationProfileId] IS NOT NULL AND i.[Id] IS NULL)
                        THROW 51861, 'Governed QS final accounts cannot be deleted.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN deleted d ON d.[Id] = i.[Id]
                        WHERE d.[ConfigurationProfileId] IS NOT NULL
                          AND (
                              i.[TenantId] <> d.[TenantId]
                              OR i.[ProjectId] <> d.[ProjectId]
                              OR i.[IsDeleted] <> d.[IsDeleted]
                              OR (d.[Status] NOT IN ('Draft', 'Rejected') AND (
                                  ISNULL(CONVERT(nvarchar(36), i.[ContractId]), '') <> ISNULL(CONVERT(nvarchar(36), d.[ContractId]), '')
                                  OR ISNULL(CONVERT(nvarchar(36), i.[ApprovedBoqVersionId]), '') <> ISNULL(CONVERT(nvarchar(36), d.[ApprovedBoqVersionId]), '')
                                  OR ISNULL(i.[ApprovedBoqSnapshotHash], '') <> ISNULL(d.[ApprovedBoqSnapshotHash], '')
                                  OR ISNULL(CONVERT(nvarchar(36), i.[ConfigurationProfileId]), '') <> ISNULL(CONVERT(nvarchar(36), d.[ConfigurationProfileId]), '')
                                  OR ISNULL(CONVERT(nvarchar(36), i.[ContractControlsDecisionId]), '') <> ISNULL(CONVERT(nvarchar(36), d.[ContractControlsDecisionId]), '')
                                  OR ISNULL(CONVERT(nvarchar(36), i.[ApprovalWorkflowDefinitionId]), '') <> ISNULL(CONVERT(nvarchar(36), d.[ApprovalWorkflowDefinitionId]), '')
                                  OR ISNULL(i.[PolicyHash], '') <> ISNULL(d.[PolicyHash], '')
                                  OR ISNULL(i.[RequestHash], '') <> ISNULL(d.[RequestHash], '')
                                  OR ISNULL(i.[ReconciliationHash], '') <> ISNULL(d.[ReconciliationHash], '')
                                  OR ISNULL(CONVERT(nvarchar(36), i.[PreparedById]), '') <> ISNULL(CONVERT(nvarchar(36), d.[PreparedById]), '')
                              ))
                              OR (d.[Status] = 'PendingApproval' AND (
                                  i.[OriginalContractValue] <> d.[OriginalContractValue]
                                  OR i.[ApprovedBoqValue] <> d.[ApprovedBoqValue]
                                  OR i.[ApprovedVariationAmount] <> d.[ApprovedVariationAmount]
                                  OR i.[ApprovedClaimAmount] <> d.[ApprovedClaimAmount]
                                  OR i.[ApprovedEscalationAmount] <> d.[ApprovedEscalationAmount]
                                  OR i.[AdvanceRecoveryAmount] <> d.[AdvanceRecoveryAmount]
                                  OR i.[MaterialDeductionAmount] <> d.[MaterialDeductionAmount]
                                  OR i.[OtherDeductionAmount] <> d.[OtherDeductionAmount]
                                  OR i.[FinalAccountValue] <> d.[FinalAccountValue]
                              ))
                          ))
                        THROW 51862, 'Governed QS final-account source, policy, commercial, and subject lineage is immutable.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN deleted d ON d.[Id] = i.[Id]
                        WHERE d.[ConfigurationProfileId] IS NOT NULL
                          AND NOT (
                              (d.[Status] = 'Draft' AND i.[Status] IN ('Draft', 'PendingApproval'))
                              OR (d.[Status] = 'Rejected' AND i.[Status] = 'Draft')
                              OR (d.[Status] = 'PendingApproval' AND i.[Status] IN ('Approved', 'Rejected'))
                              OR (d.[Status] = 'Approved' AND i.[Status] = 'Closed')
                          ))
                        THROW 51863, 'Invalid governed QS final-account lifecycle transition.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        WHERE i.[ConfigurationProfileId] IS NOT NULL
                          AND (
                              i.[ContractId] IS NULL
                              OR i.[ApprovedBoqVersionId] IS NULL
                              OR i.[ContractControlsDecisionId] IS NULL
                              OR i.[ApprovalWorkflowDefinitionId] IS NULL
                              OR i.[PreparedById] IS NULL
                              OR i.[PreparedAt] IS NULL
                              OR i.[ClientRequestId] = '00000000-0000-0000-0000-000000000000'
                              OR LEN(ISNULL(i.[ApprovedBoqSnapshotHash], '')) <> 64
                              OR LEN(ISNULL(i.[RequestHash], '')) <> 64
                              OR LEN(ISNULL(i.[PolicyHash], '')) <> 64
                              OR LEN(ISNULL(i.[ReconciliationHash], '')) <> 64
                              OR NOT (
                                  (i.[Status] = 'Draft' AND i.[ApprovalStatus] = 'Draft')
                                  OR (i.[Status] = 'PendingApproval' AND i.[ApprovalStatus] = 'Pending' AND i.[SubmittedById] IS NOT NULL AND i.[SubmittedAt] IS NOT NULL)
                                  OR (i.[Status] = 'Approved' AND i.[ApprovalStatus] = 'Approved' AND i.[ApprovedById] IS NOT NULL AND i.[ApprovedAt] IS NOT NULL)
                                  OR (i.[Status] = 'Rejected' AND i.[ApprovalStatus] = 'Rejected' AND i.[RejectionReason] IS NOT NULL)
                                  OR (i.[Status] = 'Closed' AND i.[ApprovalStatus] = 'Approved' AND i.[ApprovedById] IS NOT NULL AND i.[ApprovedAt] IS NOT NULL AND i.[ClosureReason] IS NOT NULL)
                              )
                              OR ABS(i.[FinalAccountValue] - (
                                  i.[OriginalContractValue] + i.[ApprovedVariationAmount] + i.[ApprovedClaimAmount]
                                  + i.[ApprovedEscalationAmount] - i.[AdvanceRecoveryAmount]
                                  - i.[MaterialDeductionAmount] - i.[OtherDeductionAmount])) > 0.01
                          ))
                        THROW 51864, 'Governed QS final-account policy, source, request, or arithmetic evidence is incomplete.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        WHERE i.[ConfigurationProfileId] IS NOT NULL
                          AND (
                              NOT EXISTS (SELECT 1 FROM [dbo].[Projects] p WHERE p.[Id] = i.[ProjectId] AND p.[TenantId] = i.[TenantId] AND p.[IsDeleted] = 0)
                              OR NOT EXISTS (SELECT 1 FROM [dbo].[Contracts] c WHERE c.[Id] = i.[ContractId] AND c.[TenantId] = i.[TenantId] AND c.[IsDeleted] = 0 AND c.[ContractType] = 'Works')
                              OR NOT EXISTS (SELECT 1 FROM [dbo].[ProjectBoqVersions] b WHERE b.[Id] = i.[ApprovedBoqVersionId] AND b.[ProjectId] = i.[ProjectId] AND b.[TenantId] = i.[TenantId] AND b.[IsDeleted] = 0)
                              OR NOT EXISTS (SELECT 1 FROM [dbo].[QuantitySurveyConfigurationProfiles] p WHERE p.[Id] = i.[ConfigurationProfileId] AND p.[TenantId] = i.[TenantId] AND p.[IsDeleted] = 0)
                              OR NOT EXISTS (SELECT 1 FROM [dbo].[QuantitySurveyConfigurationDecisions] d WHERE d.[Id] = i.[ContractControlsDecisionId] AND d.[ProfileId] = i.[ConfigurationProfileId] AND d.[TenantId] = i.[TenantId] AND d.[DecisionKey] = 'QS-DEC-012' AND d.[IsDeleted] = 0)
                              OR NOT EXISTS (
                                  SELECT 1 FROM [dbo].[WorkflowDefinitions] w
                                  JOIN [dbo].[WorkflowEntityTypes] e ON e.[Id] = w.[EntityTypeId]
                                  WHERE w.[Id] = i.[ApprovalWorkflowDefinitionId] AND w.[TenantId] = i.[TenantId]
                                    AND w.[IsDeleted] = 0 AND w.[IsActive] = 1 AND e.[Code] = 'QS_FINAL_ACCOUNT')
                          ))
                        THROW 51865, 'Governed QS final-account tenant or controlled-source lineage is invalid.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        WHERE i.[ConfigurationProfileId] IS NOT NULL
                          AND i.[Status] IN ('PendingApproval', 'Approved', 'Rejected', 'Closed')
                          AND (
                              i.[WorkflowInstanceId] IS NULL
                              OR NOT EXISTS (
                                  SELECT 1 FROM [dbo].[WorkflowInstances] w
                                  WHERE w.[Id] = i.[WorkflowInstanceId] AND w.[TenantId] = i.[TenantId]
                                    AND w.[EntityId] = i.[Id])
                          ))
                        THROW 51866, 'Governed QS final-account workflow lineage is invalid.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        WHERE i.[ConfigurationProfileId] IS NOT NULL
                          AND i.[Status] IN ('Approved', 'Closed')
                          AND (i.[ApprovedById] IS NULL OR i.[ApprovedById] = i.[PreparedById]
                               OR i.[ApprovedById] = i.[SubmittedById]))
                        THROW 51867, 'Governed QS final accounts require an independent approver.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        WHERE i.[ConfigurationProfileId] IS NOT NULL AND i.[Status] = 'Closed'
                          AND (i.[ClosedById] IS NULL OR i.[ClosedAt] IS NULL
                               OR i.[RetentionReleasedAmount] + 0.01 < i.[RetentionHeldAmount]
                               OR i.[PaidToDateAmount] + 0.01 < i.[FinalAccountValue]))
                        THROW 51868, 'Governed QS final accounts cannot close before retention and Finance settlement.', 1;
                END;
                """);

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [dbo].[TR_ProjectFinalAccountRevisions_QS0506AppendOnly]
                ON [dbo].[ProjectFinalAccountRevisions]
                INSTEAD OF UPDATE, DELETE
                AS
                BEGIN
                    THROW 51869, 'QS final-account audit revisions are append-only.', 1;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_ProjectFinalAccountRevisions_QS0506AppendOnly];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_ProjectFinalAccounts_QS0506Guard];");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectFinalAccounts_ProjectBoqVersions_ApprovedBoqVersionId",
                table: "ProjectFinalAccounts");

            migrationBuilder.DropTable(
                name: "ProjectFinalAccountRevisions");

            migrationBuilder.DropIndex(
                name: "IX_ProjectFinalAccounts_ApprovedBoqVersionId",
                table: "ProjectFinalAccounts");

            migrationBuilder.DropIndex(
                name: "IX_ProjectFinalAccounts_TenantId_ApprovalWorkflowDefinitionId",
                table: "ProjectFinalAccounts");

            migrationBuilder.DropIndex(
                name: "IX_ProjectFinalAccounts_TenantId_ApprovedBoqVersionId",
                table: "ProjectFinalAccounts");

            migrationBuilder.DropIndex(
                name: "IX_ProjectFinalAccounts_TenantId_ClientRequestId",
                table: "ProjectFinalAccounts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProjectFinalAccounts_QS0506Amounts",
                table: "ProjectFinalAccounts");

            migrationBuilder.DropColumn(
                name: "AdvanceRecoveryAmount",
                table: "ProjectFinalAccounts");

            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                table: "ProjectFinalAccounts");

            migrationBuilder.DropColumn(
                name: "ApprovalWorkflowDefinitionId",
                table: "ProjectFinalAccounts");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "ProjectFinalAccounts");

            migrationBuilder.DropColumn(
                name: "ApprovedBoqSnapshotHash",
                table: "ProjectFinalAccounts");

            migrationBuilder.DropColumn(
                name: "ApprovedBoqValue",
                table: "ProjectFinalAccounts");

            migrationBuilder.DropColumn(
                name: "ApprovedBoqVersionId",
                table: "ProjectFinalAccounts");

            migrationBuilder.DropColumn(
                name: "ApprovedById",
                table: "ProjectFinalAccounts");

            migrationBuilder.DropColumn(
                name: "ApprovedClaimAmount",
                table: "ProjectFinalAccounts");

            migrationBuilder.DropColumn(
                name: "ApprovedEscalationAmount",
                table: "ProjectFinalAccounts");

            migrationBuilder.DropColumn(
                name: "ClientRequestId",
                table: "ProjectFinalAccounts");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "ProjectFinalAccounts");

            migrationBuilder.DropColumn(
                name: "ClosedById",
                table: "ProjectFinalAccounts");

            migrationBuilder.DropColumn(
                name: "ClosureReason",
                table: "ProjectFinalAccounts");

            migrationBuilder.DropColumn(
                name: "ConfigurationProfileId",
                table: "ProjectFinalAccounts");

            migrationBuilder.DropColumn(
                name: "ContractControlsDecisionId",
                table: "ProjectFinalAccounts");

            migrationBuilder.DropColumn(
                name: "CorrelationId",
                table: "ProjectFinalAccounts");

            migrationBuilder.DropColumn(
                name: "LastMutationClientRequestId",
                table: "ProjectFinalAccounts");

            migrationBuilder.DropColumn(
                name: "LastMutationRequestHash",
                table: "ProjectFinalAccounts");

            migrationBuilder.DropColumn(
                name: "MaterialDeductionAmount",
                table: "ProjectFinalAccounts");

            migrationBuilder.DropColumn(
                name: "OtherDeductionAmount",
                table: "ProjectFinalAccounts");

            migrationBuilder.DropColumn(
                name: "PaidToDateAmount",
                table: "ProjectFinalAccounts");

            migrationBuilder.DropColumn(
                name: "PolicyHash",
                table: "ProjectFinalAccounts");

            migrationBuilder.DropColumn(
                name: "PreparedAt",
                table: "ProjectFinalAccounts");

            migrationBuilder.DropColumn(
                name: "PreparedById",
                table: "ProjectFinalAccounts");

            migrationBuilder.DropColumn(
                name: "ReconciliationHash",
                table: "ProjectFinalAccounts");

            migrationBuilder.DropColumn(
                name: "ReconciliationJson",
                table: "ProjectFinalAccounts");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "ProjectFinalAccounts");

            migrationBuilder.DropColumn(
                name: "RequestHash",
                table: "ProjectFinalAccounts");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ProjectFinalAccounts");

            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                table: "ProjectFinalAccounts");

            migrationBuilder.DropColumn(
                name: "SubmittedById",
                table: "ProjectFinalAccounts");

            migrationBuilder.DropColumn(
                name: "WorkflowInstanceId",
                table: "ProjectFinalAccounts");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectFinalAccounts_TenantId",
                table: "ProjectFinalAccounts",
                column: "TenantId");
        }
    }
}
