using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProcurementSupplierOnboardingTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProcurementSupplierOnboardingTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RegistrationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenReference = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TokenHashSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TokenLastFour = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    Generation = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PaymentStatus = table.Column<int>(type: "int", nullable: false),
                    IssuedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActivatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpiredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpiryReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SourceConfigurationProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceConfigurationProfileCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SourceConfigurationProfileVersion = table.Column<int>(type: "int", nullable: false),
                    SourceConfigurationDecisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FeeMode = table.Column<int>(type: "int", nullable: false),
                    FeeType = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    FeeAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TaxPercent = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    RevenueAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TaxAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExemptionWorkflowDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PaymentChannelsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReceiptNumberFormat = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ExemptionRule = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    RefundRule = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    RenewalRule = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    DecisionSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DecisionSnapshotHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IntegrityHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreationCorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastOperationCorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastOperation = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ReissuedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReissuedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReissueReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_ProcurementSupplierOnboardingTokens", x => x.Id);
                    table.CheckConstraint("CK_ProcurementSupplierOnboardingTokens_State", "[Generation] >= 1 AND [Status] BETWEEN 0 AND 2 AND [PaymentStatus] BETWEEN 0 AND 5 AND [FeeMode] BETWEEN 0 AND 1 AND [SourceConfigurationProfileVersion] >= 1 AND [FeeAmount] >= 0 AND [TaxPercent] >= 0 AND [TaxAmount] >= 0 AND [TotalAmount] = [FeeAmount] + [TaxAmount] AND LEN([CurrencyCode]) = 3 AND LEN([TokenHashSha256]) = 64 AND LEN([DecisionSnapshotHash]) = 64 AND LEN([IntegrityHash]) = 64 AND ISJSON([PaymentChannelsJson]) = 1 AND ISJSON([DecisionSnapshotJson]) = 1 AND (([Status] = 2 AND [ExpiredAtUtc] IS NOT NULL AND [ExpiryReason] IS NOT NULL) OR ([Status] <> 2 AND [ExpiredAtUtc] IS NULL)) AND (([FeeMode] = 0 AND [FeeAmount] = 0 AND [TaxAmount] = 0 AND [PaymentStatus] IN (0, 4)) OR [FeeMode] = 1)");
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierOnboardingTokens_Accounts_RevenueAccountId",
                        column: x => x.RevenueAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierOnboardingTokens_Accounts_TaxAccountId",
                        column: x => x.TaxAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierOnboardingTokens_BusinessPartnerRegistrations_RegistrationId",
                        column: x => x.RegistrationId,
                        principalTable: "BusinessPartnerRegistrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierOnboardingTokens_ProcurementConfigurationDecisions_SourceConfigurationDecisionId",
                        column: x => x.SourceConfigurationDecisionId,
                        principalTable: "ProcurementConfigurationDecisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierOnboardingTokens_ProcurementConfigurationProfiles_SourceConfigurationProfileId",
                        column: x => x.SourceConfigurationProfileId,
                        principalTable: "ProcurementConfigurationProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierOnboardingTokens_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierOnboardingTokens_WorkflowDefinitions_ExemptionWorkflowDefinitionId",
                        column: x => x.ExemptionWorkflowDefinitionId,
                        principalTable: "WorkflowDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementSupplierOnboardingExemptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    WorkflowDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequestedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DecidedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecisionComment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    EvidenceJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EvidenceHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreationCorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastOperationCorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastOperation = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
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
                    table.PrimaryKey("PK_ProcurementSupplierOnboardingExemptions", x => x.Id);
                    table.CheckConstraint("CK_ProcurementSupplierOnboardingExemptions_State", "[Status] BETWEEN 0 AND 2 AND LEN([EvidenceHash]) = 64 AND LEN([IntegrityHash]) = 64 AND ISJSON([EvidenceJson]) = 1 AND (([Status] = 0 AND [DecidedById] IS NULL AND [DecidedAtUtc] IS NULL) OR ([Status] IN (1, 2) AND [DecidedById] IS NOT NULL AND [DecidedAtUtc] IS NOT NULL))");
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierOnboardingExemptions_ProcurementSupplierOnboardingTokens_TokenId",
                        column: x => x.TokenId,
                        principalTable: "ProcurementSupplierOnboardingTokens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierOnboardingExemptions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierOnboardingExemptions_WorkflowDefinitions_WorkflowDefinitionId",
                        column: x => x.WorkflowDefinitionId,
                        principalTable: "WorkflowDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierOnboardingExemptions_WorkflowInstances_WorkflowInstanceId",
                        column: x => x.WorkflowInstanceId,
                        principalTable: "WorkflowInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementSupplierOnboardingPayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentMethodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentMethodCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PaymentMethodName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PaymentReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FeeAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PaidAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PostedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PostingEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    JournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReceiptNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ReceiptIssuedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReconciledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReconciledById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReconciliationReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReconciliationNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreationCorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastOperationCorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastOperation = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
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
                    table.PrimaryKey("PK_ProcurementSupplierOnboardingPayments", x => x.Id);
                    table.CheckConstraint("CK_ProcurementSupplierOnboardingPayments_State", "[Status] BETWEEN 1 AND 5 AND [FeeAmount] >= 0 AND [TaxAmount] >= 0 AND [TotalAmount] > 0 AND [TotalAmount] = [FeeAmount] + [TaxAmount] AND LEN([CurrencyCode]) = 3 AND LEN([IntegrityHash]) = 64 AND (([Status] IN (2, 3) AND [PostedAtUtc] IS NOT NULL AND [PostingEventId] IS NOT NULL AND [JournalEntryId] IS NOT NULL AND [ReceiptNumber] IS NOT NULL AND [ReceiptIssuedAtUtc] IS NOT NULL) OR [Status] NOT IN (2, 3)) AND (([Status] = 3 AND [ReconciledAtUtc] IS NOT NULL AND [ReconciledById] IS NOT NULL AND [ReconciliationReference] IS NOT NULL) OR [Status] <> 3)");
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierOnboardingPayments_PaymentMethod_PaymentMethodId",
                        column: x => x.PaymentMethodId,
                        principalTable: "PaymentMethod",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierOnboardingPayments_ProcurementSupplierOnboardingTokens_TokenId",
                        column: x => x.TokenId,
                        principalTable: "ProcurementSupplierOnboardingTokens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierOnboardingPayments_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierOnboardingExemptions_TenantId_CreationCorrelationId",
                table: "ProcurementSupplierOnboardingExemptions",
                columns: new[] { "TenantId", "CreationCorrelationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierOnboardingExemptions_TenantId_TokenId",
                table: "ProcurementSupplierOnboardingExemptions",
                columns: new[] { "TenantId", "TokenId" },
                unique: true,
                filter: "[Status] = 0 AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierOnboardingExemptions_TenantId_WorkflowInstanceId",
                table: "ProcurementSupplierOnboardingExemptions",
                columns: new[] { "TenantId", "WorkflowInstanceId" },
                unique: true,
                filter: "[WorkflowInstanceId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierOnboardingExemptions_TokenId",
                table: "ProcurementSupplierOnboardingExemptions",
                column: "TokenId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierOnboardingExemptions_WorkflowDefinitionId",
                table: "ProcurementSupplierOnboardingExemptions",
                column: "WorkflowDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierOnboardingExemptions_WorkflowInstanceId",
                table: "ProcurementSupplierOnboardingExemptions",
                column: "WorkflowInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierOnboardingPayments_PaymentMethodId",
                table: "ProcurementSupplierOnboardingPayments",
                column: "PaymentMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierOnboardingPayments_TenantId_CreationCorrelationId",
                table: "ProcurementSupplierOnboardingPayments",
                columns: new[] { "TenantId", "CreationCorrelationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierOnboardingPayments_TenantId_PostingEventId",
                table: "ProcurementSupplierOnboardingPayments",
                columns: new[] { "TenantId", "PostingEventId" },
                unique: true,
                filter: "[PostingEventId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierOnboardingPayments_TenantId_ReceiptNumber",
                table: "ProcurementSupplierOnboardingPayments",
                columns: new[] { "TenantId", "ReceiptNumber" },
                unique: true,
                filter: "[ReceiptNumber] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierOnboardingPayments_TenantId_TokenId_Status",
                table: "ProcurementSupplierOnboardingPayments",
                columns: new[] { "TenantId", "TokenId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierOnboardingPayments_TokenId",
                table: "ProcurementSupplierOnboardingPayments",
                column: "TokenId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierOnboardingTokens_ExemptionWorkflowDefinitionId",
                table: "ProcurementSupplierOnboardingTokens",
                column: "ExemptionWorkflowDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierOnboardingTokens_RegistrationId",
                table: "ProcurementSupplierOnboardingTokens",
                column: "RegistrationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierOnboardingTokens_RevenueAccountId",
                table: "ProcurementSupplierOnboardingTokens",
                column: "RevenueAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierOnboardingTokens_SourceConfigurationDecisionId",
                table: "ProcurementSupplierOnboardingTokens",
                column: "SourceConfigurationDecisionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierOnboardingTokens_SourceConfigurationProfileId",
                table: "ProcurementSupplierOnboardingTokens",
                column: "SourceConfigurationProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierOnboardingTokens_TaxAccountId",
                table: "ProcurementSupplierOnboardingTokens",
                column: "TaxAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierOnboardingTokens_TenantId_CreationCorrelationId",
                table: "ProcurementSupplierOnboardingTokens",
                columns: new[] { "TenantId", "CreationCorrelationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierOnboardingTokens_TenantId_RegistrationId",
                table: "ProcurementSupplierOnboardingTokens",
                columns: new[] { "TenantId", "RegistrationId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierOnboardingTokens_TenantId_SourceConfigurationProfileId",
                table: "ProcurementSupplierOnboardingTokens",
                columns: new[] { "TenantId", "SourceConfigurationProfileId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierOnboardingTokens_TenantId_Status_PaymentStatus_IssuedAtUtc",
                table: "ProcurementSupplierOnboardingTokens",
                columns: new[] { "TenantId", "Status", "PaymentStatus", "IssuedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierOnboardingTokens_TenantId_TokenHashSha256",
                table: "ProcurementSupplierOnboardingTokens",
                columns: new[] { "TenantId", "TokenHashSha256" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierOnboardingTokens_TenantId_TokenReference",
                table: "ProcurementSupplierOnboardingTokens",
                columns: new[] { "TenantId", "TokenReference" },
                unique: true);

            migrationBuilder.Sql(
                """
                CREATE TRIGGER [dbo].[TR_ProcurementSupplierOnboardingTokens_Protected]
                ON [dbo].[ProcurementSupplierOnboardingTokens]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id = d.Id WHERE i.Id IS NULL)
                        THROW 51800, 'Supplier-onboarding tokens cannot be physically deleted.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
                        WHERE i.TenantId <> d.TenantId OR i.RegistrationId <> d.RegistrationId
                           OR i.TokenReference <> d.TokenReference
                           OR i.SourceConfigurationProfileId <> d.SourceConfigurationProfileId
                           OR i.SourceConfigurationProfileCode <> d.SourceConfigurationProfileCode
                           OR i.SourceConfigurationProfileVersion <> d.SourceConfigurationProfileVersion
                           OR i.SourceConfigurationDecisionId <> d.SourceConfigurationDecisionId
                           OR i.FeeMode <> d.FeeMode OR i.FeeType <> d.FeeType
                           OR i.FeeAmount <> d.FeeAmount OR i.TaxPercent <> d.TaxPercent
                           OR i.TaxAmount <> d.TaxAmount OR i.TotalAmount <> d.TotalAmount
                           OR i.CurrencyCode <> d.CurrencyCode
                           OR ISNULL(i.RevenueAccountId, '00000000-0000-0000-0000-000000000000')
                              <> ISNULL(d.RevenueAccountId, '00000000-0000-0000-0000-000000000000')
                           OR ISNULL(i.TaxAccountId, '00000000-0000-0000-0000-000000000000')
                              <> ISNULL(d.TaxAccountId, '00000000-0000-0000-0000-000000000000')
                           OR ISNULL(i.ExemptionWorkflowDefinitionId, '00000000-0000-0000-0000-000000000000')
                              <> ISNULL(d.ExemptionWorkflowDefinitionId, '00000000-0000-0000-0000-000000000000')
                           OR i.PaymentChannelsJson <> d.PaymentChannelsJson
                           OR i.ReceiptNumberFormat <> d.ReceiptNumberFormat
                           OR i.ExemptionRule <> d.ExemptionRule
                           OR i.RefundRule <> d.RefundRule OR i.RenewalRule <> d.RenewalRule
                           OR i.DecisionSnapshotJson <> d.DecisionSnapshotJson
                           OR i.DecisionSnapshotHash <> d.DecisionSnapshotHash
                           OR i.IssuedAtUtc <> d.IssuedAtUtc
                           OR i.CreationCorrelationId <> d.CreationCorrelationId
                           OR i.CreatedAt <> d.CreatedAt
                           OR ISNULL(i.CreatedBy, '') <> ISNULL(d.CreatedBy, '')
                           OR ISNULL(i.CreatedById, '00000000-0000-0000-0000-000000000000')
                              <> ISNULL(d.CreatedById, '00000000-0000-0000-0000-000000000000')
                           OR i.IsDeleted <> d.IsDeleted)
                        THROW 51801, 'Supplier-onboarding token configuration and identity lineage is immutable.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
                        WHERE NOT (
                            i.Status = d.Status OR
                            (d.Status = 0 AND i.Status IN (1, 2)) OR
                            (d.Status = 1 AND i.Status = 2)))
                        THROW 51802, 'Invalid supplier-onboarding token lifecycle transition.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
                        WHERE NOT (
                            i.PaymentStatus = d.PaymentStatus OR
                            (d.PaymentStatus = 1 AND i.PaymentStatus IN (2, 4, 5)) OR
                            (d.PaymentStatus = 2 AND i.PaymentStatus = 3) OR
                            (d.PaymentStatus = 5 AND i.PaymentStatus = 2)))
                        THROW 51803, 'Invalid supplier-onboarding payment lifecycle transition.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN deleted d ON d.Id = i.Id
                        LEFT JOIN BusinessPartnerRegistrations r
                          ON r.Id = i.RegistrationId AND r.TenantId = i.TenantId
                        WHERE d.Status <> 2 AND i.Status = 2
                          AND (r.Id IS NULL OR r.Status NOT IN ('Approved', 'Rejected')))
                        THROW 51804, 'A token can expire only when its linked application is Approved or Rejected.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
                        WHERE (i.TokenHashSha256 <> d.TokenHashSha256
                               AND (d.Status = 2 OR i.Status = 2 OR i.Generation <> d.Generation + 1))
                           OR (i.TokenHashSha256 = d.TokenHashSha256
                               AND (i.Generation <> d.Generation OR i.TokenLastFour <> d.TokenLastFour)))
                        THROW 51805, 'Token rotation must advance exactly one generation and cannot occur after application completion.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN BusinessPartnerRegistrations r
                          ON r.Id = i.RegistrationId AND r.TenantId = i.TenantId
                        LEFT JOIN ProcurementConfigurationProfiles p
                          ON p.Id = i.SourceConfigurationProfileId AND p.TenantId = i.TenantId
                        LEFT JOIN ProcurementConfigurationDecisions cd
                          ON cd.Id = i.SourceConfigurationDecisionId
                         AND cd.TenantId = i.TenantId
                         AND cd.ProfileId = i.SourceConfigurationProfileId
                        LEFT JOIN Accounts ra
                          ON ra.Id = i.RevenueAccountId AND ra.TenantId = i.TenantId
                        LEFT JOIN Accounts ta
                          ON ta.Id = i.TaxAccountId AND ta.TenantId = i.TenantId
                        LEFT JOIN WorkflowDefinitions wd
                          ON wd.Id = i.ExemptionWorkflowDefinitionId AND wd.TenantId = i.TenantId
                        WHERE r.Id IS NULL OR p.Id IS NULL OR cd.Id IS NULL
                           OR (i.RevenueAccountId IS NOT NULL AND ra.Id IS NULL)
                           OR (i.TaxAccountId IS NOT NULL AND ta.Id IS NULL)
                           OR (i.ExemptionWorkflowDefinitionId IS NOT NULL AND wd.Id IS NULL))
                        THROW 51806, 'Supplier-onboarding token tenant and DEC-007 lineage is invalid.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN deleted d ON d.Id = i.Id
                        JOIN ProcurementConfigurationProfiles p
                          ON p.Id = i.SourceConfigurationProfileId AND p.TenantId = i.TenantId
                        JOIN ProcurementConfigurationDecisions cd
                          ON cd.Id = i.SourceConfigurationDecisionId
                         AND cd.TenantId = i.TenantId
                         AND cd.ProfileId = p.Id
                        WHERE d.Id IS NULL
                          AND (p.IsDeleted = 1 OR p.LifecycleStatus <> 1
                               OR p.EffectiveFrom > i.IssuedAtUtc
                               OR (p.EffectiveTo IS NOT NULL AND p.EffectiveTo < i.IssuedAtUtc)
                               OR cd.IsDeleted = 1 OR cd.DecisionKey <> 'DEC-007'
                               OR cd.Status <> 2 OR cd.ApprovalStatus <> 1
                               OR cd.EvidenceStatus <> 2
                               OR (cd.EffectiveFrom IS NOT NULL AND cd.EffectiveFrom > i.IssuedAtUtc)
                               OR (cd.EffectiveTo IS NOT NULL AND cd.EffectiveTo < i.IssuedAtUtc)))
                        THROW 51807, 'A token can be inserted only from an evidenced Published and effective DEC-007 decision.', 1;

                END
                """);

            migrationBuilder.Sql(
                """
                CREATE TRIGGER [dbo].[TR_ProcurementSupplierOnboardingPayments_Protected]
                ON [dbo].[ProcurementSupplierOnboardingPayments]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id = d.Id WHERE i.Id IS NULL)
                        THROW 51810, 'Supplier-onboarding payments cannot be physically deleted.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
                        WHERE i.TenantId <> d.TenantId OR i.TokenId <> d.TokenId
                           OR i.PaymentMethodId <> d.PaymentMethodId
                           OR i.PaymentMethodCode <> d.PaymentMethodCode
                           OR i.PaymentMethodName <> d.PaymentMethodName
                           OR ISNULL(i.PaymentReference, '') <> ISNULL(d.PaymentReference, '')
                           OR i.FeeAmount <> d.FeeAmount OR i.TaxAmount <> d.TaxAmount
                           OR i.TotalAmount <> d.TotalAmount OR i.CurrencyCode <> d.CurrencyCode
                           OR i.PaidAtUtc <> d.PaidAtUtc
                           OR i.CreationCorrelationId <> d.CreationCorrelationId
                           OR i.CreatedAt <> d.CreatedAt
                           OR ISNULL(i.CreatedBy, '') <> ISNULL(d.CreatedBy, '')
                           OR ISNULL(i.CreatedById, '00000000-0000-0000-0000-000000000000')
                              <> ISNULL(d.CreatedById, '00000000-0000-0000-0000-000000000000')
                           OR i.IsDeleted <> d.IsDeleted)
                        THROW 51811, 'Supplier-onboarding payment source and amount lineage is immutable.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
                        WHERE NOT (
                            i.Status = d.Status OR
                            (d.Status = 1 AND i.Status IN (2, 5)) OR
                            (d.Status = 2 AND i.Status = 3)))
                        THROW 51812, 'Invalid supplier-onboarding payment transition.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
                        WHERE (d.PostingEventId IS NOT NULL AND
                               (i.PostingEventId <> d.PostingEventId OR i.JournalEntryId <> d.JournalEntryId
                                OR i.PostedAtUtc <> d.PostedAtUtc OR i.ReceiptNumber <> d.ReceiptNumber
                                OR i.ReceiptIssuedAtUtc <> d.ReceiptIssuedAtUtc))
                           OR (d.ReconciledAtUtc IS NOT NULL AND
                               (i.ReconciledAtUtc <> d.ReconciledAtUtc
                                OR i.ReconciledById <> d.ReconciledById
                                OR i.ReconciliationReference <> d.ReconciliationReference
                                OR ISNULL(i.ReconciliationNotes, '') <> ISNULL(d.ReconciliationNotes, ''))))
                        THROW 51813, 'Posted and reconciled supplier-onboarding payment lineage is immutable.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        LEFT JOIN ProcurementSupplierOnboardingTokens t
                          ON t.Id = i.TokenId AND t.TenantId = i.TenantId
                        LEFT JOIN PaymentMethod pm
                          ON pm.Id = i.PaymentMethodId AND pm.TenantId = i.TenantId
                        WHERE t.Id IS NULL OR pm.Id IS NULL)
                        THROW 51814, 'Supplier-onboarding payment tenant lineage is invalid.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        LEFT JOIN FinancePostingEvents f
                          ON f.Id = i.PostingEventId AND f.TenantId = i.TenantId
                        LEFT JOIN JournalEntries j
                          ON j.Id = i.JournalEntryId AND j.TenantId = i.TenantId
                        WHERE i.Status IN (2, 3) AND (f.Id IS NULL OR j.Id IS NULL))
                        THROW 51815, 'Supplier-onboarding payment Finance posting lineage is invalid.', 1;
                END
                """);

            migrationBuilder.Sql(
                """
                CREATE TRIGGER [dbo].[TR_ProcurementSupplierOnboardingExemptions_Protected]
                ON [dbo].[ProcurementSupplierOnboardingExemptions]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id = d.Id WHERE i.Id IS NULL)
                        THROW 51820, 'Supplier-onboarding exemptions cannot be physically deleted.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
                        WHERE i.TenantId <> d.TenantId OR i.TokenId <> d.TokenId
                           OR i.WorkflowDefinitionId <> d.WorkflowDefinitionId
                           OR i.RequestedById <> d.RequestedById
                           OR i.RequestedAtUtc <> d.RequestedAtUtc OR i.Reason <> d.Reason
                           OR i.CreationCorrelationId <> d.CreationCorrelationId
                           OR i.CreatedAt <> d.CreatedAt
                           OR ISNULL(i.CreatedBy, '') <> ISNULL(d.CreatedBy, '')
                           OR ISNULL(i.CreatedById, '00000000-0000-0000-0000-000000000000')
                              <> ISNULL(d.CreatedById, '00000000-0000-0000-0000-000000000000')
                           OR i.IsDeleted <> d.IsDeleted)
                        THROW 51821, 'Supplier-onboarding exemption request lineage is immutable.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
                        WHERE NOT (i.Status = d.Status OR (d.Status = 0 AND i.Status IN (1, 2))))
                        THROW 51822, 'Invalid supplier-onboarding exemption transition.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
                        WHERE d.WorkflowInstanceId IS NOT NULL
                          AND ISNULL(i.WorkflowInstanceId, '00000000-0000-0000-0000-000000000000')
                              <> d.WorkflowInstanceId)
                        THROW 51823, 'Supplier-onboarding exemption workflow lineage is immutable once assigned.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        LEFT JOIN ProcurementSupplierOnboardingTokens t
                          ON t.Id = i.TokenId AND t.TenantId = i.TenantId
                        LEFT JOIN WorkflowDefinitions wd
                          ON wd.Id = i.WorkflowDefinitionId AND wd.TenantId = i.TenantId
                        LEFT JOIN WorkflowInstances wi
                          ON wi.Id = i.WorkflowInstanceId AND wi.TenantId = i.TenantId
                         AND wi.WorkflowDefinitionId = i.WorkflowDefinitionId
                         AND wi.EntityId = i.Id
                        WHERE t.Id IS NULL OR wd.Id IS NULL
                           OR (i.WorkflowInstanceId IS NOT NULL AND wi.Id IS NULL))
                        THROW 51824, 'Supplier-onboarding exemption tenant or workflow lineage is invalid.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
                        WHERE d.Status = 0 AND i.Status IN (1, 2)
                          AND i.DecidedById = i.RequestedById)
                        THROW 51825, 'The exemption requester cannot decide the same exemption.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN deleted d ON d.Id = i.Id
                        WHERE d.Id IS NULL
                          AND (i.Status <> 0 OR i.WorkflowInstanceId IS NOT NULL))
                        THROW 51826, 'A supplier-onboarding exemption must be inserted as a pending request.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN deleted d ON d.Id = i.Id
                        LEFT JOIN WorkflowInstances wi
                          ON wi.Id = i.WorkflowInstanceId
                         AND wi.TenantId = i.TenantId
                         AND wi.WorkflowDefinitionId = i.WorkflowDefinitionId
                         AND wi.EntityId = i.Id
                        WHERE d.Status = 0
                          AND ((i.Status = 1 AND (wi.Id IS NULL OR wi.Status <> 2))
                               OR (i.Status = 2 AND (wi.Id IS NULL OR wi.Status NOT IN (3, 4)))))
                        THROW 51827, 'The exemption decision must match the terminal shared-workflow outcome.', 1;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProcurementSupplierOnboardingExemptions");

            migrationBuilder.DropTable(
                name: "ProcurementSupplierOnboardingPayments");

            migrationBuilder.DropTable(
                name: "ProcurementSupplierOnboardingTokens");
        }
    }
}
