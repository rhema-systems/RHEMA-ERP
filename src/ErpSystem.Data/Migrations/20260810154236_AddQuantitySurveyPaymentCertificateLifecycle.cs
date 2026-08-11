using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddQuantitySurveyPaymentCertificateLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProjectPaymentCertificates_TenantId",
                table: "ProjectPaymentCertificates");

            migrationBuilder.AddColumn<Guid>(
                name: "AccountsPayableAccountId",
                table: "ProjectPaymentCertificates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AdvanceRecoveryAmount",
                table: "ProjectPaymentCertificates",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "AdvanceRecoveryApplied",
                table: "ProjectPaymentCertificates",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApHandoffAt",
                table: "ProjectPaymentCertificates",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApHandoffFailure",
                table: "ProjectPaymentCertificates",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApHandoffStatus",
                table: "ProjectPaymentCertificates",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "NotReady");

            migrationBuilder.AddColumn<string>(
                name: "ApprovalStatus",
                table: "ProjectPaymentCertificates",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Draft");

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovalWorkflowDefinitionId",
                table: "ProjectPaymentCertificates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "ProjectPaymentCertificates",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedById",
                table: "ProjectPaymentCertificates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CentralDocumentRecordId",
                table: "ProjectPaymentCertificates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CentralDocumentVersionId",
                table: "ProjectPaymentCertificates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CertificateMetadataTemplateCodeSnapshot",
                table: "ProjectPaymentCertificates",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CertificateMetadataTemplateId",
                table: "ProjectPaymentCertificates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CertificateTemplateId",
                table: "ProjectPaymentCertificates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CertificateTemplateVersionSnapshot",
                table: "ProjectPaymentCertificates",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "CertifiedToDateAmount",
                table: "ProjectPaymentCertificates",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "ClientRequestId",
                table: "ProjectPaymentCertificates",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "ConfigurationProfileId",
                table: "ProjectPaymentCertificates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ExpenseAccountId",
                table: "ProjectPaymentCertificates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "GeneratedAt",
                table: "ProjectPaymentCertificates",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GeneratedDocumentHash",
                table: "ProjectPaymentCertificates",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastMutationClientRequestId",
                table: "ProjectPaymentCertificates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastMutationRequestHash",
                table: "ProjectPaymentCertificates",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MaterialDeductionAmount",
                table: "ProjectPaymentCertificates",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "PaymentStatusSnapshot",
                table: "ProjectPaymentCertificates",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "NotInvoiced");

            migrationBuilder.AddColumn<DateTime>(
                name: "PaymentStatusUpdatedAt",
                table: "ProjectPaymentCertificates",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PaymentTermId",
                table: "ProjectPaymentCertificates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PolicyHash",
                table: "ProjectPaymentCertificates",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PreparedAt",
                table: "ProjectPaymentCertificates",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "PreparedById",
                table: "ProjectPaymentCertificates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PreviousCertificateRequired",
                table: "ProjectPaymentCertificates",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "PreviousPaymentCertificateId",
                table: "ProjectPaymentCertificates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PreviouslyCertifiedAmount",
                table: "ProjectPaymentCertificates",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "QuantitySurveyValuationWorksheetId",
                table: "ProjectPaymentCertificates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "ProjectPaymentCertificates",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestHash",
                table: "ProjectPaymentCertificates",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RetentionApplied",
                table: "ProjectPaymentCertificates",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ProjectPaymentCertificates",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedAt",
                table: "ProjectPaymentCertificates",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SubmittedById",
                table: "ProjectPaymentCertificates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxAmount",
                table: "ProjectPaymentCertificates",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "TaxGroupId",
                table: "ProjectPaymentCertificates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxHandling",
                table: "ProjectPaymentCertificates",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "FinanceCalculated");

            migrationBuilder.AddColumn<Guid>(
                name: "ValuationDecisionId",
                table: "ProjectPaymentCertificates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VendorInvoiceId",
                table: "ProjectPaymentCertificates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WithholdingTaxId",
                table: "ProjectPaymentCertificates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WorkflowInstanceId",
                table: "ProjectPaymentCertificates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "QuantitySurveyPaymentCertificateRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentCertificateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ActorRoles = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
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
                    table.PrimaryKey("PK_QuantitySurveyPaymentCertificateRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyPaymentCertificateRevisions_ProjectPaymentCertificates_PaymentCertificateId",
                        column: x => x.PaymentCertificateId,
                        principalTable: "ProjectPaymentCertificates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyPaymentCertificateRevisions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyPaymentCertificateRevisions_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPaymentCertificates_AccountsPayableAccountId",
                table: "ProjectPaymentCertificates",
                column: "AccountsPayableAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPaymentCertificates_ApprovalWorkflowDefinitionId",
                table: "ProjectPaymentCertificates",
                column: "ApprovalWorkflowDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPaymentCertificates_ApprovedById",
                table: "ProjectPaymentCertificates",
                column: "ApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPaymentCertificates_CentralDocumentRecordId",
                table: "ProjectPaymentCertificates",
                column: "CentralDocumentRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPaymentCertificates_CentralDocumentVersionId",
                table: "ProjectPaymentCertificates",
                column: "CentralDocumentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPaymentCertificates_CertificateMetadataTemplateId",
                table: "ProjectPaymentCertificates",
                column: "CertificateMetadataTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPaymentCertificates_CertificateTemplateId",
                table: "ProjectPaymentCertificates",
                column: "CertificateTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPaymentCertificates_ConfigurationProfileId",
                table: "ProjectPaymentCertificates",
                column: "ConfigurationProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPaymentCertificates_ExpenseAccountId",
                table: "ProjectPaymentCertificates",
                column: "ExpenseAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPaymentCertificates_PaymentTermId",
                table: "ProjectPaymentCertificates",
                column: "PaymentTermId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPaymentCertificates_PreparedById",
                table: "ProjectPaymentCertificates",
                column: "PreparedById");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPaymentCertificates_PreviousPaymentCertificateId",
                table: "ProjectPaymentCertificates",
                column: "PreviousPaymentCertificateId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPaymentCertificates_QuantitySurveyValuationWorksheetId",
                table: "ProjectPaymentCertificates",
                column: "QuantitySurveyValuationWorksheetId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPaymentCertificates_SubmittedById",
                table: "ProjectPaymentCertificates",
                column: "SubmittedById");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPaymentCertificates_TaxGroupId",
                table: "ProjectPaymentCertificates",
                column: "TaxGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPaymentCertificates_TenantId_CertificateNumber",
                table: "ProjectPaymentCertificates",
                columns: new[] { "TenantId", "CertificateNumber" },
                unique: true,
                filter: "[CertificateNumber] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPaymentCertificates_TenantId_ClientRequestId",
                table: "ProjectPaymentCertificates",
                columns: new[] { "TenantId", "ClientRequestId" },
                unique: true,
                filter: "[ClientRequestId] <> '00000000-0000-0000-0000-000000000000'");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPaymentCertificates_TenantId_LastMutationClientRequestId",
                table: "ProjectPaymentCertificates",
                columns: new[] { "TenantId", "LastMutationClientRequestId" },
                unique: true,
                filter: "[LastMutationClientRequestId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPaymentCertificates_TenantId_QuantitySurveyValuationWorksheetId",
                table: "ProjectPaymentCertificates",
                columns: new[] { "TenantId", "QuantitySurveyValuationWorksheetId" },
                unique: true,
                filter: "[QuantitySurveyValuationWorksheetId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPaymentCertificates_TenantId_VendorInvoiceId",
                table: "ProjectPaymentCertificates",
                columns: new[] { "TenantId", "VendorInvoiceId" },
                unique: true,
                filter: "[VendorInvoiceId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPaymentCertificates_ValuationDecisionId",
                table: "ProjectPaymentCertificates",
                column: "ValuationDecisionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPaymentCertificates_VendorInvoiceId",
                table: "ProjectPaymentCertificates",
                column: "VendorInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPaymentCertificates_WithholdingTaxId",
                table: "ProjectPaymentCertificates",
                column: "WithholdingTaxId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProjectPaymentCertificates_Amounts",
                table: "ProjectPaymentCertificates",
                sql: "[CertifiedToDateAmount] >= 0 AND [PreviouslyCertifiedAmount] >= 0 AND [GrossCertifiedAmount] >= 0 AND [RetentionHeldAmount] >= 0 AND [RetentionReleasedAmount] >= 0 AND [OtherDeductionsAmount] >= 0 AND [AdvanceRecoveryAmount] >= 0 AND [MaterialDeductionAmount] >= 0 AND [TaxAmount] >= 0 AND [NetCertifiedAmount] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProjectPaymentCertificates_ApHandoff",
                table: "ProjectPaymentCertificates",
                sql: "[ApHandoffStatus] IN ('NotReady','Ready','Created','Failed')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProjectPaymentCertificates_ApLink",
                table: "ProjectPaymentCertificates",
                sql: "([VendorInvoiceId] IS NULL AND [ApHandoffStatus] IN ('NotReady','Ready','Failed')) OR ([VendorInvoiceId] IS NOT NULL AND [ApHandoffStatus] = 'Created')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProjectPaymentCertificates_Approval",
                table: "ProjectPaymentCertificates",
                sql: "[ApprovalStatus] IN ('Draft','Pending','Approved','Rejected')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProjectPaymentCertificates_GovernedState",
                table: "ProjectPaymentCertificates",
                sql: "[QuantitySurveyValuationWorksheetId] IS NULL OR (([Status] = 'Draft' AND [ApprovalStatus] = 'Draft' AND [WorkflowInstanceId] IS NULL AND [ApprovedAt] IS NULL) OR ([Status] = 'Issued' AND [ApprovalStatus] = 'Pending' AND [WorkflowInstanceId] IS NOT NULL AND [ApprovedAt] IS NULL) OR ([Status] IN ('Approved','Paid') AND [ApprovalStatus] = 'Approved' AND [WorkflowInstanceId] IS NOT NULL AND [ApprovedById] IS NOT NULL AND [ApprovedAt] IS NOT NULL) OR ([Status] = 'Cancelled' AND [ApprovalStatus] = 'Rejected' AND [WorkflowInstanceId] IS NOT NULL AND [RejectionReason] IS NOT NULL))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProjectPaymentCertificates_QsLineage",
                table: "ProjectPaymentCertificates",
                sql: "[QuantitySurveyValuationWorksheetId] IS NULL OR ([ProjectInterimValuationId] IS NOT NULL AND [ConfigurationProfileId] IS NOT NULL AND [ValuationDecisionId] IS NOT NULL AND [ApprovalWorkflowDefinitionId] IS NOT NULL AND [CertificateTemplateId] IS NOT NULL AND [CertificateMetadataTemplateId] IS NOT NULL AND [CertificateMetadataTemplateCodeSnapshot] IS NOT NULL AND [ExpenseAccountId] IS NOT NULL AND [AccountsPayableAccountId] IS NOT NULL AND [PaymentTermId] IS NOT NULL AND [TaxGroupId] IS NOT NULL AND [PreparedById] IS NOT NULL AND [PreparedAt] > '2000-01-01' AND [ClientRequestId] <> '00000000-0000-0000-0000-000000000000' AND [CertificateNumber] IS NOT NULL AND LEN([PolicyHash]) = 64 AND LEN([RequestHash]) = 64)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProjectPaymentCertificates_Status",
                table: "ProjectPaymentCertificates",
                sql: "[Status] IN ('Draft','Issued','Approved','Paid','Cancelled')");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyPaymentCertificateRevisions_ActorUserId",
                table: "QuantitySurveyPaymentCertificateRevisions",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyPaymentCertificateRevisions_PaymentCertificateId",
                table: "QuantitySurveyPaymentCertificateRevisions",
                column: "PaymentCertificateId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyPaymentCertificateRevisions_TenantId_CorrelationId",
                table: "QuantitySurveyPaymentCertificateRevisions",
                columns: new[] { "TenantId", "CorrelationId" });

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyPaymentCertificateRevisions_TenantId_PaymentCertificateId_CreatedAt",
                table: "QuantitySurveyPaymentCertificateRevisions",
                columns: new[] { "TenantId", "PaymentCertificateId", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectPaymentCertificates_Accounts_AccountsPayableAccountId",
                table: "ProjectPaymentCertificates",
                column: "AccountsPayableAccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectPaymentCertificates_Accounts_ExpenseAccountId",
                table: "ProjectPaymentCertificates",
                column: "ExpenseAccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectPaymentCertificates_CentralDocumentMetadataTemplates_CertificateMetadataTemplateId",
                table: "ProjectPaymentCertificates",
                column: "CertificateMetadataTemplateId",
                principalTable: "CentralDocumentMetadataTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectPaymentCertificates_CentralDocumentRecords_CentralDocumentRecordId",
                table: "ProjectPaymentCertificates",
                column: "CentralDocumentRecordId",
                principalTable: "CentralDocumentRecords",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectPaymentCertificates_CentralDocumentVersions_CentralDocumentVersionId",
                table: "ProjectPaymentCertificates",
                column: "CentralDocumentVersionId",
                principalTable: "CentralDocumentVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectPaymentCertificates_PaymentTerms_PaymentTermId",
                table: "ProjectPaymentCertificates",
                column: "PaymentTermId",
                principalTable: "PaymentTerms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectPaymentCertificates_ProjectPaymentCertificates_PreviousPaymentCertificateId",
                table: "ProjectPaymentCertificates",
                column: "PreviousPaymentCertificateId",
                principalTable: "ProjectPaymentCertificates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectPaymentCertificates_QuantitySurveyConfigurationDecisions_ValuationDecisionId",
                table: "ProjectPaymentCertificates",
                column: "ValuationDecisionId",
                principalTable: "QuantitySurveyConfigurationDecisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectPaymentCertificates_QuantitySurveyConfigurationProfiles_ConfigurationProfileId",
                table: "ProjectPaymentCertificates",
                column: "ConfigurationProfileId",
                principalTable: "QuantitySurveyConfigurationProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectPaymentCertificates_QuantitySurveyValuationWorksheets_QuantitySurveyValuationWorksheetId",
                table: "ProjectPaymentCertificates",
                column: "QuantitySurveyValuationWorksheetId",
                principalTable: "QuantitySurveyValuationWorksheets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectPaymentCertificates_ReportTemplates_CertificateTemplateId",
                table: "ProjectPaymentCertificates",
                column: "CertificateTemplateId",
                principalTable: "ReportTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectPaymentCertificates_TaxGroups_TaxGroupId",
                table: "ProjectPaymentCertificates",
                column: "TaxGroupId",
                principalTable: "TaxGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectPaymentCertificates_Taxes_WithholdingTaxId",
                table: "ProjectPaymentCertificates",
                column: "WithholdingTaxId",
                principalTable: "Taxes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectPaymentCertificates_Users_ApprovedById",
                table: "ProjectPaymentCertificates",
                column: "ApprovedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectPaymentCertificates_Users_PreparedById",
                table: "ProjectPaymentCertificates",
                column: "PreparedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectPaymentCertificates_Users_SubmittedById",
                table: "ProjectPaymentCertificates",
                column: "SubmittedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectPaymentCertificates_VendorInvoice_VendorInvoiceId",
                table: "ProjectPaymentCertificates",
                column: "VendorInvoiceId",
                principalTable: "VendorInvoice",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectPaymentCertificates_WorkflowDefinitions_ApprovalWorkflowDefinitionId",
                table: "ProjectPaymentCertificates",
                column: "ApprovalWorkflowDefinitionId",
                principalTable: "WorkflowDefinitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(@"
CREATE OR ALTER TRIGGER [dbo].[TR_ProjectPaymentCertificates_QsLifecycle]
ON [dbo].[ProjectPaymentCertificates]
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (
        SELECT 1 FROM deleted d
        LEFT JOIN inserted i ON i.Id = d.Id
        WHERE d.QuantitySurveyValuationWorksheetId IS NOT NULL AND i.Id IS NULL)
        THROW 51970, 'Governed QS payment certificates cannot be physically deleted.', 1;

    IF EXISTS (
        SELECT 1 FROM inserted i
        WHERE i.QuantitySurveyValuationWorksheetId IS NOT NULL
          AND (
            NOT EXISTS (SELECT 1 FROM dbo.QuantitySurveyValuationWorksheets w WHERE w.Id = i.QuantitySurveyValuationWorksheetId AND w.TenantId = i.TenantId AND w.ProjectId = i.ProjectId AND w.ProjectInterimValuationId = i.ProjectInterimValuationId AND w.IsDeleted = 0)
            OR NOT EXISTS (SELECT 1 FROM dbo.QuantitySurveyConfigurationProfiles p WHERE p.Id = i.ConfigurationProfileId AND p.TenantId = i.TenantId AND p.IsDeleted = 0)
            OR NOT EXISTS (SELECT 1 FROM dbo.QuantitySurveyConfigurationDecisions q WHERE q.Id = i.ValuationDecisionId AND q.TenantId = i.TenantId AND q.ProfileId = i.ConfigurationProfileId AND q.DecisionKey = 'QS-DEC-008' AND q.IsDeleted = 0)
            OR NOT EXISTS (SELECT 1 FROM dbo.Accounts a WHERE a.Id = i.ExpenseAccountId AND a.TenantId = i.TenantId AND a.IsDeleted = 0)
            OR NOT EXISTS (SELECT 1 FROM dbo.Accounts a WHERE a.Id = i.AccountsPayableAccountId AND a.TenantId = i.TenantId AND a.IsDeleted = 0)
          ))
        THROW 51971, 'Governed QS payment-certificate lineage must remain in the same tenant and project.', 1;

    IF EXISTS (
        SELECT 1 FROM inserted i
        WHERE i.QuantitySurveyValuationWorksheetId IS NOT NULL
          AND ROUND(i.NetCertifiedAmount, 2) <> ROUND(
            i.GrossCertifiedAmount + i.RetentionReleasedAmount
            + CASE WHEN i.TaxHandling = 'Inclusive' THEN 0 ELSE i.TaxAmount END
            - i.RetentionHeldAmount - i.AdvanceRecoveryAmount - i.MaterialDeductionAmount - i.OtherDeductionsAmount, 2))
        THROW 51972, 'Governed QS payment-certificate totals do not reconcile.', 1;

    IF EXISTS (
        SELECT 1 FROM inserted i
        JOIN deleted d ON d.Id = i.Id
        WHERE d.QuantitySurveyValuationWorksheetId IS NOT NULL
          AND (
            i.TenantId <> d.TenantId OR i.ProjectId <> d.ProjectId
            OR ISNULL(i.ProjectInterimValuationId, '00000000-0000-0000-0000-000000000000') <> ISNULL(d.ProjectInterimValuationId, '00000000-0000-0000-0000-000000000000')
            OR i.QuantitySurveyValuationWorksheetId <> d.QuantitySurveyValuationWorksheetId
            OR ISNULL(i.PreviousPaymentCertificateId, '00000000-0000-0000-0000-000000000000') <> ISNULL(d.PreviousPaymentCertificateId, '00000000-0000-0000-0000-000000000000')
            OR i.ConfigurationProfileId <> d.ConfigurationProfileId OR i.ValuationDecisionId <> d.ValuationDecisionId
            OR i.ApprovalWorkflowDefinitionId <> d.ApprovalWorkflowDefinitionId OR i.CertificateTemplateId <> d.CertificateTemplateId
            OR i.CertificateMetadataTemplateId <> d.CertificateMetadataTemplateId OR i.ExpenseAccountId <> d.ExpenseAccountId
            OR i.AccountsPayableAccountId <> d.AccountsPayableAccountId OR i.PaymentTermId <> d.PaymentTermId
            OR i.TaxGroupId <> d.TaxGroupId OR ISNULL(i.WithholdingTaxId, '00000000-0000-0000-0000-000000000000') <> ISNULL(d.WithholdingTaxId, '00000000-0000-0000-0000-000000000000')
            OR i.PreparedById <> d.PreparedById OR i.PreparedAt <> d.PreparedAt OR i.ClientRequestId <> d.ClientRequestId
            OR i.RequestHash <> d.RequestHash OR i.PolicyHash <> d.PolicyHash OR i.Currency <> d.Currency
            OR i.CertifiedToDateAmount <> d.CertifiedToDateAmount OR i.PreviouslyCertifiedAmount <> d.PreviouslyCertifiedAmount
            OR i.GrossCertifiedAmount <> d.GrossCertifiedAmount OR i.RetentionHeldAmount <> d.RetentionHeldAmount
            OR i.RetentionReleasedAmount <> d.RetentionReleasedAmount
          ))
        THROW 51973, 'Governed QS payment-certificate source, policy, actor, and cumulative lineage is immutable.', 1;

    IF EXISTS (
        SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
        WHERE d.QuantitySurveyValuationWorksheetId IS NOT NULL
          AND NOT (
            (d.Status = 'Draft' AND d.ApprovalStatus = 'Draft' AND i.Status = 'Draft' AND i.ApprovalStatus = 'Draft')
            OR (d.Status = 'Draft' AND d.ApprovalStatus = 'Draft' AND i.Status = 'Issued' AND i.ApprovalStatus = 'Pending')
            OR (d.Status = 'Issued' AND d.ApprovalStatus = 'Pending' AND i.Status = 'Approved' AND i.ApprovalStatus = 'Approved')
            OR (d.Status = 'Issued' AND d.ApprovalStatus = 'Pending' AND i.Status = 'Cancelled' AND i.ApprovalStatus = 'Rejected')
            OR (d.Status = 'Approved' AND d.ApprovalStatus = 'Approved' AND i.Status IN ('Approved','Paid') AND i.ApprovalStatus = 'Approved')
            OR (d.Status = 'Paid' AND d.ApprovalStatus = 'Approved' AND i.Status = 'Paid' AND i.ApprovalStatus = 'Approved')
          ))
        THROW 51974, 'Invalid governed QS payment-certificate lifecycle transition.', 1;

    IF EXISTS (
        SELECT 1 FROM inserted i
        WHERE i.QuantitySurveyValuationWorksheetId IS NOT NULL AND i.VendorInvoiceId IS NOT NULL
          AND NOT EXISTS (SELECT 1 FROM dbo.VendorInvoice v WHERE v.Id = i.VendorInvoiceId AND v.TenantId = i.TenantId AND v.IsDeleted = 0))
        THROW 51975, 'The linked Finance AP invoice must belong to the same tenant.', 1;
END");

            migrationBuilder.Sql(@"
CREATE OR ALTER TRIGGER [dbo].[TR_QuantitySurveyPaymentCertificateRevisions_AppendOnly]
ON [dbo].[QuantitySurveyPaymentCertificateRevisions]
AFTER UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM deleted)
        THROW 51976, 'QS payment-certificate revision history is append-only.', 1;
END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_QuantitySurveyPaymentCertificateRevisions_AppendOnly]");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_ProjectPaymentCertificates_QsLifecycle]");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectPaymentCertificates_Accounts_AccountsPayableAccountId",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectPaymentCertificates_Accounts_ExpenseAccountId",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectPaymentCertificates_CentralDocumentMetadataTemplates_CertificateMetadataTemplateId",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectPaymentCertificates_CentralDocumentRecords_CentralDocumentRecordId",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectPaymentCertificates_CentralDocumentVersions_CentralDocumentVersionId",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectPaymentCertificates_PaymentTerms_PaymentTermId",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectPaymentCertificates_ProjectPaymentCertificates_PreviousPaymentCertificateId",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectPaymentCertificates_QuantitySurveyConfigurationDecisions_ValuationDecisionId",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectPaymentCertificates_QuantitySurveyConfigurationProfiles_ConfigurationProfileId",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectPaymentCertificates_QuantitySurveyValuationWorksheets_QuantitySurveyValuationWorksheetId",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectPaymentCertificates_ReportTemplates_CertificateTemplateId",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectPaymentCertificates_TaxGroups_TaxGroupId",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectPaymentCertificates_Taxes_WithholdingTaxId",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectPaymentCertificates_Users_ApprovedById",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectPaymentCertificates_Users_PreparedById",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectPaymentCertificates_Users_SubmittedById",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectPaymentCertificates_VendorInvoice_VendorInvoiceId",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectPaymentCertificates_WorkflowDefinitions_ApprovalWorkflowDefinitionId",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropTable(
                name: "QuantitySurveyPaymentCertificateRevisions");

            migrationBuilder.DropIndex(
                name: "IX_ProjectPaymentCertificates_AccountsPayableAccountId",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropIndex(
                name: "IX_ProjectPaymentCertificates_ApprovalWorkflowDefinitionId",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropIndex(
                name: "IX_ProjectPaymentCertificates_ApprovedById",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropIndex(
                name: "IX_ProjectPaymentCertificates_CentralDocumentRecordId",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropIndex(
                name: "IX_ProjectPaymentCertificates_CentralDocumentVersionId",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropIndex(
                name: "IX_ProjectPaymentCertificates_CertificateMetadataTemplateId",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropIndex(
                name: "IX_ProjectPaymentCertificates_CertificateTemplateId",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropIndex(
                name: "IX_ProjectPaymentCertificates_ConfigurationProfileId",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropIndex(
                name: "IX_ProjectPaymentCertificates_ExpenseAccountId",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropIndex(
                name: "IX_ProjectPaymentCertificates_PaymentTermId",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropIndex(
                name: "IX_ProjectPaymentCertificates_PreparedById",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropIndex(
                name: "IX_ProjectPaymentCertificates_PreviousPaymentCertificateId",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropIndex(
                name: "IX_ProjectPaymentCertificates_QuantitySurveyValuationWorksheetId",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropIndex(
                name: "IX_ProjectPaymentCertificates_SubmittedById",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropIndex(
                name: "IX_ProjectPaymentCertificates_TaxGroupId",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropIndex(
                name: "IX_ProjectPaymentCertificates_TenantId_CertificateNumber",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropIndex(
                name: "IX_ProjectPaymentCertificates_TenantId_ClientRequestId",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropIndex(
                name: "IX_ProjectPaymentCertificates_TenantId_LastMutationClientRequestId",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropIndex(
                name: "IX_ProjectPaymentCertificates_TenantId_QuantitySurveyValuationWorksheetId",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropIndex(
                name: "IX_ProjectPaymentCertificates_TenantId_VendorInvoiceId",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropIndex(
                name: "IX_ProjectPaymentCertificates_ValuationDecisionId",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropIndex(
                name: "IX_ProjectPaymentCertificates_VendorInvoiceId",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropIndex(
                name: "IX_ProjectPaymentCertificates_WithholdingTaxId",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProjectPaymentCertificates_Amounts",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProjectPaymentCertificates_ApHandoff",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProjectPaymentCertificates_ApLink",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProjectPaymentCertificates_Approval",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProjectPaymentCertificates_GovernedState",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProjectPaymentCertificates_QsLineage",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProjectPaymentCertificates_Status",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropColumn(
                name: "AccountsPayableAccountId",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropColumn(
                name: "AdvanceRecoveryAmount",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropColumn(
                name: "AdvanceRecoveryApplied",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropColumn(
                name: "ApHandoffAt",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropColumn(
                name: "ApHandoffFailure",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropColumn(
                name: "ApHandoffStatus",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropColumn(
                name: "ApprovalWorkflowDefinitionId",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropColumn(
                name: "ApprovedById",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropColumn(
                name: "CentralDocumentRecordId",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropColumn(
                name: "CentralDocumentVersionId",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropColumn(
                name: "CertificateMetadataTemplateCodeSnapshot",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropColumn(
                name: "CertificateMetadataTemplateId",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropColumn(
                name: "CertificateTemplateId",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropColumn(
                name: "CertificateTemplateVersionSnapshot",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropColumn(
                name: "CertifiedToDateAmount",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropColumn(
                name: "ClientRequestId",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropColumn(
                name: "ConfigurationProfileId",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropColumn(
                name: "ExpenseAccountId",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropColumn(
                name: "GeneratedAt",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropColumn(
                name: "GeneratedDocumentHash",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropColumn(
                name: "LastMutationClientRequestId",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropColumn(
                name: "LastMutationRequestHash",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropColumn(
                name: "MaterialDeductionAmount",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropColumn(
                name: "PaymentStatusSnapshot",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropColumn(
                name: "PaymentStatusUpdatedAt",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropColumn(
                name: "PaymentTermId",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropColumn(
                name: "PolicyHash",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropColumn(
                name: "PreparedAt",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropColumn(
                name: "PreparedById",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropColumn(
                name: "PreviousCertificateRequired",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropColumn(
                name: "PreviousPaymentCertificateId",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropColumn(
                name: "PreviouslyCertifiedAmount",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropColumn(
                name: "QuantitySurveyValuationWorksheetId",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropColumn(
                name: "RequestHash",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropColumn(
                name: "RetentionApplied",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropColumn(
                name: "SubmittedById",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropColumn(
                name: "TaxAmount",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropColumn(
                name: "TaxGroupId",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropColumn(
                name: "TaxHandling",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropColumn(
                name: "ValuationDecisionId",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropColumn(
                name: "VendorInvoiceId",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropColumn(
                name: "WithholdingTaxId",
                table: "ProjectPaymentCertificates");

            migrationBuilder.DropColumn(
                name: "WorkflowInstanceId",
                table: "ProjectPaymentCertificates");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPaymentCertificates_TenantId",
                table: "ProjectPaymentCertificates",
                column: "TenantId");
        }
    }
}
