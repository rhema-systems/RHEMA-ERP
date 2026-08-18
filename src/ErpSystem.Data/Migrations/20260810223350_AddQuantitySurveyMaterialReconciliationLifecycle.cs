using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddQuantitySurveyMaterialReconciliationLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_QsValuationEvidence_Type",
                table: "QuantitySurveyValuationWorksheetEvidence");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProjectPaymentCertificates_Amounts",
                table: "ProjectPaymentCertificates");

            migrationBuilder.AddColumn<decimal>(
                name: "MaterialOffSiteAmount",
                table: "ProjectPaymentCertificates",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MaterialOnSiteAmount",
                table: "ProjectPaymentCertificates",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "QuantitySurveyMaterialReconciliationId",
                table: "ProjectPaymentCertificates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "QuantitySurveyMaterialReconciliations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ValuationWorksheetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractorBusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    LastMutationClientRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastMutationRequestHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ReconciliationNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ApprovalStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ContractNumberSnapshot = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ContractorNameSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CurrencyCodeSnapshot = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ValuationBasis = table.Column<int>(type: "int", nullable: false),
                    InventoryReconciliationRequired = table.Column<bool>(type: "bit", nullable: false),
                    MaterialOnSiteAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MaterialOffSiteAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TdcSuppliedDeductionAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ConfigurationProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MaterialDecisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovalWorkflowDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PolicyHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    PreparedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreparedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ContractorConfirmedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ContractorConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ContractorConfirmationHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true),
                    SubmittedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_QuantitySurveyMaterialReconciliations", x => x.Id);
                    table.CheckConstraint("CK_QsMaterialReconciliations_Amounts", "[MaterialOnSiteAmount] >= 0 AND [MaterialOffSiteAmount] >= 0 AND [TdcSuppliedDeductionAmount] >= 0");
                    table.CheckConstraint("CK_QsMaterialReconciliations_Approval", "[ApprovalStatus] IN ('Draft','Pending','Approved','Rejected')");
                    table.CheckConstraint("CK_QsMaterialReconciliations_Lifecycle", "([Status] = 'Draft' AND [ApprovalStatus] = 'Draft' AND [ContractorConfirmedAt] IS NULL AND [WorkflowInstanceId] IS NULL) OR ([Status] = 'ContractorConfirmed' AND [ApprovalStatus] = 'Draft' AND [ContractorConfirmedById] IS NOT NULL AND [ContractorConfirmedAt] IS NOT NULL AND LEN([ContractorConfirmationHash]) = 64 AND [WorkflowInstanceId] IS NULL) OR ([Status] = 'PendingApproval' AND [ApprovalStatus] = 'Pending' AND [ContractorConfirmedAt] IS NOT NULL AND [SubmittedById] IS NOT NULL AND [SubmittedAt] IS NOT NULL AND [WorkflowInstanceId] IS NOT NULL) OR ([Status] = 'Approved' AND [ApprovalStatus] = 'Approved' AND [ApprovedById] IS NOT NULL AND [ApprovedAt] IS NOT NULL AND [WorkflowInstanceId] IS NOT NULL) OR ([Status] = 'Rejected' AND [ApprovalStatus] = 'Rejected' AND [RejectionReason] IS NOT NULL)");
                    table.CheckConstraint("CK_QsMaterialReconciliations_Status", "[Status] IN ('Draft','ContractorConfirmed','PendingApproval','Approved','Rejected')");
                    table.ForeignKey(
                        name: "FK_QuantitySurveyMaterialReconciliations_BusinessPartners_ContractorBusinessPartnerId",
                        column: x => x.ContractorBusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyMaterialReconciliations_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyMaterialReconciliations_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyMaterialReconciliations_QuantitySurveyConfigurationDecisions_MaterialDecisionId",
                        column: x => x.MaterialDecisionId,
                        principalTable: "QuantitySurveyConfigurationDecisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyMaterialReconciliations_QuantitySurveyConfigurationProfiles_ConfigurationProfileId",
                        column: x => x.ConfigurationProfileId,
                        principalTable: "QuantitySurveyConfigurationProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyMaterialReconciliations_QuantitySurveyValuationWorksheets_ValuationWorksheetId",
                        column: x => x.ValuationWorksheetId,
                        principalTable: "QuantitySurveyValuationWorksheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyMaterialReconciliations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyMaterialReconciliations_Users_ApprovedById",
                        column: x => x.ApprovedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyMaterialReconciliations_Users_ContractorConfirmedById",
                        column: x => x.ContractorConfirmedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyMaterialReconciliations_Users_PreparedById",
                        column: x => x.PreparedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyMaterialReconciliations_Users_SubmittedById",
                        column: x => x.SubmittedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyMaterialReconciliations_WorkflowDefinitions_ApprovalWorkflowDefinitionId",
                        column: x => x.ApprovalWorkflowDefinitionId,
                        principalTable: "WorkflowDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QuantitySurveyMaterialReconciliationLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReconciliationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    LineType = table.Column<int>(type: "int", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryItemCodeSnapshot = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    InventoryItemNameSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UnitOfMeasureSnapshot = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    DeliveredUnitCost = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ApprovedRateId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedUnitRateSnapshot = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    AppliedUnitRate = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    TotalValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    InventoryIssueVoucherLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IssueVoucherNumberSnapshot = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IssueVoucherIntegrityHashSnapshot = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true),
                    ValuationEvidenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CentralDocumentRecordIdSnapshot = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CentralDocumentVersionIdSnapshot = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EvidenceChecksumSnapshot = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true),
                    SourceHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_QuantitySurveyMaterialReconciliationLines", x => x.Id);
                    table.CheckConstraint("CK_QsMaterialReconciliationLines_Amounts", "[Sequence] > 0 AND [Quantity] > 0 AND [AppliedUnitRate] > 0 AND [TotalValue] > 0 AND LEN([SourceHash]) = 64");
                    table.CheckConstraint("CK_QsMaterialReconciliationLines_Source", "([LineType] IN (0,1) AND [InventoryIssueVoucherLineId] IS NULL AND [ValuationEvidenceId] IS NOT NULL AND [CentralDocumentRecordIdSnapshot] IS NOT NULL AND [CentralDocumentVersionIdSnapshot] IS NOT NULL AND LEN([EvidenceChecksumSnapshot]) = 64) OR ([LineType] = 2 AND [InventoryIssueVoucherLineId] IS NOT NULL AND [ValuationEvidenceId] IS NULL AND [CentralDocumentRecordIdSnapshot] IS NULL AND [CentralDocumentVersionIdSnapshot] IS NULL AND [EvidenceChecksumSnapshot] IS NULL AND [IssueVoucherNumberSnapshot] IS NOT NULL AND LEN([IssueVoucherIntegrityHashSnapshot]) = 64)");
                    table.CheckConstraint("CK_QsMaterialReconciliationLines_Type", "[LineType] BETWEEN 0 AND 2");
                    table.ForeignKey(
                        name: "FK_QuantitySurveyMaterialReconciliationLines_InventoryIssueVoucherLines_InventoryIssueVoucherLineId",
                        column: x => x.InventoryIssueVoucherLineId,
                        principalTable: "InventoryIssueVoucherLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyMaterialReconciliationLines_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyMaterialReconciliationLines_QuantitySurveyMaterialReconciliations_ReconciliationId",
                        column: x => x.ReconciliationId,
                        principalTable: "QuantitySurveyMaterialReconciliations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyMaterialReconciliationLines_QuantitySurveyRateLibraryRates_ApprovedRateId",
                        column: x => x.ApprovedRateId,
                        principalTable: "QuantitySurveyRateLibraryRates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyMaterialReconciliationLines_QuantitySurveyValuationWorksheetEvidence_ValuationEvidenceId",
                        column: x => x.ValuationEvidenceId,
                        principalTable: "QuantitySurveyValuationWorksheetEvidence",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyMaterialReconciliationLines_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QuantitySurveyMaterialReconciliationRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReconciliationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ActorRoles = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    BeforeJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AfterJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
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
                    table.PrimaryKey("PK_QuantitySurveyMaterialReconciliationRevisions", x => x.Id);
                    table.CheckConstraint("CK_QsMaterialReconciliationRevisions_Request", "[ClientRequestId] <> '00000000-0000-0000-0000-000000000000' AND LEN([RequestHash]) = 64");
                    table.ForeignKey(
                        name: "FK_QuantitySurveyMaterialReconciliationRevisions_QuantitySurveyMaterialReconciliations_ReconciliationId",
                        column: x => x.ReconciliationId,
                        principalTable: "QuantitySurveyMaterialReconciliations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyMaterialReconciliationRevisions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_QsValuationEvidence_Type",
                table: "QuantitySurveyValuationWorksheetEvidence",
                sql: "[EvidenceType] IN (0,1,2,3,4,5,6)");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPaymentCertificates_QuantitySurveyMaterialReconciliationId",
                table: "ProjectPaymentCertificates",
                column: "QuantitySurveyMaterialReconciliationId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPaymentCertificates_TenantId_QuantitySurveyMaterialReconciliationId",
                table: "ProjectPaymentCertificates",
                columns: new[] { "TenantId", "QuantitySurveyMaterialReconciliationId" },
                unique: true,
                filter: "[QuantitySurveyMaterialReconciliationId] IS NOT NULL AND [Status] <> 'Cancelled' AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPaymentCertificates_TenantId_QuantitySurveyMaterialReconciliationId_Status",
                table: "ProjectPaymentCertificates",
                columns: new[] { "TenantId", "QuantitySurveyMaterialReconciliationId", "Status" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProjectPaymentCertificates_Amounts",
                table: "ProjectPaymentCertificates",
                sql: "[CertifiedToDateAmount] >= 0 AND [PreviouslyCertifiedAmount] >= 0 AND [GrossCertifiedAmount] >= 0 AND [RetentionHeldAmount] >= 0 AND [RetentionReleasedAmount] >= 0 AND [OtherDeductionsAmount] >= 0 AND [AdvanceRecoveryAmount] >= 0 AND [MaterialDeductionAmount] >= 0 AND [MaterialOnSiteAmount] >= 0 AND [MaterialOffSiteAmount] >= 0 AND [TaxAmount] >= 0 AND [NetCertifiedAmount] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProjectPaymentCertificates_QsMaterial",
                table: "ProjectPaymentCertificates",
                sql: "[QuantitySurveyValuationWorksheetId] IS NULL OR (([QuantitySurveyMaterialReconciliationId] IS NULL AND [MaterialDeductionAmount] = 0 AND [MaterialOnSiteAmount] = 0 AND [MaterialOffSiteAmount] = 0) OR [QuantitySurveyMaterialReconciliationId] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyMaterialReconciliationLines_ApprovedRateId",
                table: "QuantitySurveyMaterialReconciliationLines",
                column: "ApprovedRateId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyMaterialReconciliationLines_InventoryIssueVoucherLineId",
                table: "QuantitySurveyMaterialReconciliationLines",
                column: "InventoryIssueVoucherLineId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyMaterialReconciliationLines_InventoryItemId",
                table: "QuantitySurveyMaterialReconciliationLines",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyMaterialReconciliationLines_ReconciliationId",
                table: "QuantitySurveyMaterialReconciliationLines",
                column: "ReconciliationId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyMaterialReconciliationLines_TenantId_InventoryIssueVoucherLineId",
                table: "QuantitySurveyMaterialReconciliationLines",
                columns: new[] { "TenantId", "InventoryIssueVoucherLineId" },
                unique: true,
                filter: "[InventoryIssueVoucherLineId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyMaterialReconciliationLines_TenantId_ReconciliationId_Sequence",
                table: "QuantitySurveyMaterialReconciliationLines",
                columns: new[] { "TenantId", "ReconciliationId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyMaterialReconciliationLines_TenantId_ValuationEvidenceId",
                table: "QuantitySurveyMaterialReconciliationLines",
                columns: new[] { "TenantId", "ValuationEvidenceId" },
                unique: true,
                filter: "[ValuationEvidenceId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyMaterialReconciliationLines_ValuationEvidenceId",
                table: "QuantitySurveyMaterialReconciliationLines",
                column: "ValuationEvidenceId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyMaterialReconciliationRevisions_ReconciliationId",
                table: "QuantitySurveyMaterialReconciliationRevisions",
                column: "ReconciliationId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyMaterialReconciliationRevisions_TenantId_ClientRequestId",
                table: "QuantitySurveyMaterialReconciliationRevisions",
                columns: new[] { "TenantId", "ClientRequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyMaterialReconciliationRevisions_TenantId_ReconciliationId_CreatedAt",
                table: "QuantitySurveyMaterialReconciliationRevisions",
                columns: new[] { "TenantId", "ReconciliationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyMaterialReconciliations_ApprovalWorkflowDefinitionId",
                table: "QuantitySurveyMaterialReconciliations",
                column: "ApprovalWorkflowDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyMaterialReconciliations_ApprovedById",
                table: "QuantitySurveyMaterialReconciliations",
                column: "ApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyMaterialReconciliations_ConfigurationProfileId",
                table: "QuantitySurveyMaterialReconciliations",
                column: "ConfigurationProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyMaterialReconciliations_ContractId",
                table: "QuantitySurveyMaterialReconciliations",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyMaterialReconciliations_ContractorBusinessPartnerId",
                table: "QuantitySurveyMaterialReconciliations",
                column: "ContractorBusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyMaterialReconciliations_ContractorConfirmedById",
                table: "QuantitySurveyMaterialReconciliations",
                column: "ContractorConfirmedById");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyMaterialReconciliations_MaterialDecisionId",
                table: "QuantitySurveyMaterialReconciliations",
                column: "MaterialDecisionId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyMaterialReconciliations_PreparedById",
                table: "QuantitySurveyMaterialReconciliations",
                column: "PreparedById");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyMaterialReconciliations_ProjectId",
                table: "QuantitySurveyMaterialReconciliations",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyMaterialReconciliations_SubmittedById",
                table: "QuantitySurveyMaterialReconciliations",
                column: "SubmittedById");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyMaterialReconciliations_TenantId_ClientRequestId",
                table: "QuantitySurveyMaterialReconciliations",
                columns: new[] { "TenantId", "ClientRequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyMaterialReconciliations_TenantId_ProjectId_ContractId_Status",
                table: "QuantitySurveyMaterialReconciliations",
                columns: new[] { "TenantId", "ProjectId", "ContractId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyMaterialReconciliations_TenantId_ValuationWorksheetId",
                table: "QuantitySurveyMaterialReconciliations",
                columns: new[] { "TenantId", "ValuationWorksheetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyMaterialReconciliations_ValuationWorksheetId",
                table: "QuantitySurveyMaterialReconciliations",
                column: "ValuationWorksheetId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectPaymentCertificates_QuantitySurveyMaterialReconciliations_QuantitySurveyMaterialReconciliationId",
                table: "ProjectPaymentCertificates",
                column: "QuantitySurveyMaterialReconciliationId",
                principalTable: "QuantitySurveyMaterialReconciliations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [dbo].[TR_QS0507_MaterialReconciliation_Governance]
                ON [dbo].[QuantitySurveyMaterialReconciliations]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id = d.Id WHERE i.Id IS NULL)
                        THROW 51871, 'Quantity Survey material reconciliations cannot be deleted.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
                        WHERE i.TenantId <> d.TenantId OR i.ProjectId <> d.ProjectId OR i.ContractId <> d.ContractId
                           OR i.ValuationWorksheetId <> d.ValuationWorksheetId
                           OR i.ContractorBusinessPartnerId <> d.ContractorBusinessPartnerId
                           OR i.ClientRequestId <> d.ClientRequestId OR i.RequestHash <> d.RequestHash
                           OR i.ReconciliationNumber <> d.ReconciliationNumber
                           OR i.ContractNumberSnapshot <> d.ContractNumberSnapshot
                           OR i.ContractorNameSnapshot <> d.ContractorNameSnapshot
                           OR i.CurrencyCodeSnapshot <> d.CurrencyCodeSnapshot
                           OR i.ConfigurationProfileId <> d.ConfigurationProfileId
                           OR i.MaterialDecisionId <> d.MaterialDecisionId
                           OR i.ApprovalWorkflowDefinitionId <> d.ApprovalWorkflowDefinitionId
                           OR i.PolicyHash <> d.PolicyHash OR i.ValuationBasis <> d.ValuationBasis
                           OR i.InventoryReconciliationRequired <> d.InventoryReconciliationRequired
                           OR i.PreparedById <> d.PreparedById OR i.PreparedAt <> d.PreparedAt)
                        THROW 51872, 'Quantity Survey material source, contract, policy, and preparer lineage is immutable.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
                        WHERE d.Status = 'Approved'
                           OR (i.Status <> d.Status AND NOT (
                                (d.Status = 'Draft' AND i.Status = 'ContractorConfirmed') OR
                                (d.Status = 'ContractorConfirmed' AND i.Status = 'PendingApproval') OR
                                (d.Status = 'PendingApproval' AND i.Status IN ('Approved','Rejected')) OR
                                (d.Status = 'Rejected' AND i.Status = 'Draft'))))
                        THROW 51873, 'Invalid Quantity Survey material-reconciliation lifecycle transition.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
                        WHERE d.Status IN ('ContractorConfirmed','PendingApproval') AND i.Status = d.Status
                          AND (i.MaterialOnSiteAmount <> d.MaterialOnSiteAmount
                            OR i.MaterialOffSiteAmount <> d.MaterialOffSiteAmount
                            OR i.TdcSuppliedDeductionAmount <> d.TdcSuppliedDeductionAmount))
                        THROW 51874, 'Confirmed Quantity Survey material values are immutable pending a workflow outcome.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        WHERE i.ContractorConfirmedById IS NOT NULL
                          AND (i.PreparedById = i.ContractorConfirmedById OR NOT EXISTS (
                              SELECT 1 FROM dbo.BusinessPartnerUsers bpu
                              WHERE bpu.TenantId = i.TenantId
                                AND bpu.BusinessPartnerId = i.ContractorBusinessPartnerId
                                AND bpu.UserId = i.ContractorConfirmedById
                                AND bpu.IsActive = 1 AND bpu.IsDeleted = 0)))
                        THROW 51875, 'Contractor confirmation must be made by an active, independent user of the assigned contractor.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        WHERE i.SubmittedById IS NOT NULL
                          AND (i.SubmittedById = i.PreparedById OR i.SubmittedById = i.ContractorConfirmedById))
                        THROW 51876, 'Quantity Survey material submission violates maker-checker separation.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        WHERE i.ApprovedById IS NOT NULL
                          AND (i.ApprovedById = i.PreparedById OR i.ApprovedById = i.ContractorConfirmedById
                            OR i.ApprovedById = i.SubmittedById))
                        THROW 51877, 'Quantity Survey material approval violates maker-checker separation.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        OUTER APPLY (
                            SELECT
                                ROUND(COALESCE(SUM(CASE WHEN l.LineType = 0 AND l.IsDeleted = 0 THEN l.TotalValue ELSE 0 END), 0), 2) OnSite,
                                ROUND(COALESCE(SUM(CASE WHEN l.LineType = 1 AND l.IsDeleted = 0 THEN l.TotalValue ELSE 0 END), 0), 2) OffSite,
                                ROUND(COALESCE(SUM(CASE WHEN l.LineType = 2 AND l.IsDeleted = 0 THEN l.TotalValue ELSE 0 END), 0), 2) TdcDeduction
                            FROM dbo.QuantitySurveyMaterialReconciliationLines l
                            WHERE l.TenantId = i.TenantId AND l.ReconciliationId = i.Id
                        ) totals
                        WHERE i.Status <> 'Draft'
                          AND (i.MaterialOnSiteAmount <> totals.OnSite OR i.MaterialOffSiteAmount <> totals.OffSite
                            OR i.TdcSuppliedDeductionAmount <> totals.TdcDeduction))
                        THROW 51878, 'Quantity Survey material totals do not match their governed source lines.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [dbo].[TR_QS0507_MaterialReconciliationLines_Governance]
                ON [dbo].[QuantitySurveyMaterialReconciliationLines]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (
                        SELECT 1 FROM deleted d
                        JOIN dbo.QuantitySurveyMaterialReconciliations r ON r.Id = d.ReconciliationId AND r.TenantId = d.TenantId
                        WHERE r.Status NOT IN ('Draft','Rejected'))
                        THROW 51879, 'Material source lines are immutable after contractor confirmation.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        LEFT JOIN dbo.QuantitySurveyMaterialReconciliations r
                          ON r.Id = i.ReconciliationId AND r.TenantId = i.TenantId AND r.IsDeleted = 0
                        LEFT JOIN dbo.InventoryItems item
                          ON item.Id = i.InventoryItemId AND item.TenantId = i.TenantId AND item.IsDeleted = 0
                        WHERE i.IsDeleted = 0
                          AND (r.Id IS NULL OR item.Id IS NULL OR ROUND(i.Quantity * i.AppliedUnitRate, 2) <> i.TotalValue))
                        THROW 51880, 'Material lines must use tenant-owned Inventory items and server-calculated values.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        JOIN dbo.QuantitySurveyMaterialReconciliations r ON r.Id = i.ReconciliationId AND r.TenantId = i.TenantId
                        LEFT JOIN dbo.QuantitySurveyValuationWorksheetEvidence e
                          ON e.Id = i.ValuationEvidenceId AND e.TenantId = i.TenantId
                         AND e.WorksheetId = r.ValuationWorksheetId AND e.IsDeleted = 0
                        WHERE i.IsDeleted = 0 AND i.LineType IN (0,1)
                          AND (e.Id IS NULL OR e.EvidenceType <> CASE WHEN i.LineType = 0 THEN 5 ELSE 6 END
                            OR e.CentralDocumentRecordId <> i.CentralDocumentRecordIdSnapshot
                            OR e.CentralDocumentVersionId <> i.CentralDocumentVersionIdSnapshot
                            OR e.ChecksumSha256 <> i.EvidenceChecksumSnapshot))
                        THROW 51881, 'On-site and off-site materials require matching centrally governed valuation evidence.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        JOIN dbo.QuantitySurveyMaterialReconciliations r ON r.Id = i.ReconciliationId AND r.TenantId = i.TenantId
                        LEFT JOIN dbo.InventoryIssueVoucherLines il
                          ON il.Id = i.InventoryIssueVoucherLineId AND il.TenantId = i.TenantId AND il.IsDeleted = 0
                        LEFT JOIN dbo.InventoryIssueVouchers iv
                          ON iv.Id = il.InventoryIssueVoucherId AND iv.TenantId = i.TenantId AND iv.IsDeleted = 0
                        WHERE i.IsDeleted = 0 AND i.LineType = 2
                          AND (il.Id IS NULL OR iv.Id IS NULL OR iv.Status <> 2 OR iv.ProjectId <> r.ProjectId
                            OR il.InventoryItemId <> i.InventoryItemId OR il.Quantity <> i.Quantity
                            OR il.UnitCost <> i.DeliveredUnitCost OR il.IntegrityHash <> i.IssueVoucherIntegrityHashSnapshot
                            OR iv.VoucherNumber <> i.IssueVoucherNumberSnapshot))
                        THROW 51882, 'TDC-supplied material deductions require the matching acknowledged project Inventory issue.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        LEFT JOIN dbo.QuantitySurveyRateLibraryRates rate
                          ON rate.Id = i.ApprovedRateId AND rate.TenantId = i.TenantId AND rate.IsDeleted = 0
                        LEFT JOIN dbo.QuantitySurveyRateLibraryItems rateItem
                          ON rateItem.Id = rate.RateLibraryItemId AND rateItem.TenantId = i.TenantId AND rateItem.IsDeleted = 0
                        WHERE i.IsDeleted = 0 AND i.ApprovedRateId IS NOT NULL
                          AND (rate.Id IS NULL OR rateItem.InventoryItemId <> i.InventoryItemId
                            OR rate.UnitRate <> i.ApprovedUnitRateSnapshot))
                        THROW 51883, 'Approved material rates must retain their tenant-owned rate-library lineage.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [dbo].[TR_QS0507_MaterialReconciliationRevisions_AppendOnly]
                ON [dbo].[QuantitySurveyMaterialReconciliationRevisions]
                AFTER UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM deleted)
                        THROW 51884, 'Quantity Survey material-reconciliation revisions are append-only.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [dbo].[TR_QS0507_PaymentCertificateMaterialLineage]
                ON [dbo].[ProjectPaymentCertificates]
                AFTER INSERT, UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        LEFT JOIN dbo.QuantitySurveyMaterialReconciliations r
                          ON r.Id = i.QuantitySurveyMaterialReconciliationId
                         AND r.TenantId = i.TenantId AND r.ProjectId = i.ProjectId
                         AND r.ContractId = i.ContractId
                         AND r.ValuationWorksheetId = i.QuantitySurveyValuationWorksheetId
                         AND r.Status = 'Approved' AND r.ApprovalStatus = 'Approved' AND r.IsDeleted = 0
                        WHERE i.IsDeleted = 0 AND (
                            (i.QuantitySurveyMaterialReconciliationId IS NULL
                              AND (i.MaterialOnSiteAmount <> 0 OR i.MaterialOffSiteAmount <> 0 OR i.MaterialDeductionAmount <> 0))
                            OR (i.QuantitySurveyMaterialReconciliationId IS NOT NULL
                              AND (r.Id IS NULL OR i.MaterialOnSiteAmount <> r.MaterialOnSiteAmount
                                OR i.MaterialOffSiteAmount <> r.MaterialOffSiteAmount
                                OR i.MaterialDeductionAmount <> r.TdcSuppliedDeductionAmount))))
                        THROW 51885, 'Payment certificates must freeze exact values from an approved material reconciliation for the same valuation and contract.', 1;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_QS0507_PaymentCertificateMaterialLineage];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_QS0507_MaterialReconciliationRevisions_AppendOnly];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_QS0507_MaterialReconciliationLines_Governance];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_QS0507_MaterialReconciliation_Governance];");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectPaymentCertificates_QuantitySurveyMaterialReconciliations_QuantitySurveyMaterialReconciliationId",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropTable(
                name: "QuantitySurveyMaterialReconciliationLines");

            migrationBuilder.DropTable(
                name: "QuantitySurveyMaterialReconciliationRevisions");

            migrationBuilder.DropTable(
                name: "QuantitySurveyMaterialReconciliations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_QsValuationEvidence_Type",
                table: "QuantitySurveyValuationWorksheetEvidence");

            migrationBuilder.DropIndex(
                name: "IX_ProjectPaymentCertificates_QuantitySurveyMaterialReconciliationId",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropIndex(
                name: "IX_ProjectPaymentCertificates_TenantId_QuantitySurveyMaterialReconciliationId",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropIndex(
                name: "IX_ProjectPaymentCertificates_TenantId_QuantitySurveyMaterialReconciliationId_Status",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProjectPaymentCertificates_Amounts",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProjectPaymentCertificates_QsMaterial",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropColumn(
                name: "MaterialOffSiteAmount",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropColumn(
                name: "MaterialOnSiteAmount",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropColumn(
                name: "QuantitySurveyMaterialReconciliationId",
                table: "ProjectPaymentCertificates");

            migrationBuilder.AddCheckConstraint(
                name: "CK_QsValuationEvidence_Type",
                table: "QuantitySurveyValuationWorksheetEvidence",
                sql: "[EvidenceType] IN (0,1,2,3,4)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProjectPaymentCertificates_Amounts",
                table: "ProjectPaymentCertificates",
                sql: "[CertifiedToDateAmount] >= 0 AND [PreviouslyCertifiedAmount] >= 0 AND [GrossCertifiedAmount] >= 0 AND [RetentionHeldAmount] >= 0 AND [RetentionReleasedAmount] >= 0 AND [OtherDeductionsAmount] >= 0 AND [AdvanceRecoveryAmount] >= 0 AND [MaterialDeductionAmount] >= 0 AND [TaxAmount] >= 0 AND [NetCertifiedAmount] >= 0");
        }
    }
}
