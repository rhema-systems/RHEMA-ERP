using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProcurementRfqStatutoryControls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProcurementRfqOpeningRegisters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RfqId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OpenedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClosedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OpenedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OpenedByName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    EvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ParticipantSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RegisterSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
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
                    table.PrimaryKey("PK_ProcurementRfqOpeningRegisters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcurementRfqOpeningRegisters_RequestForQuotations_RfqId",
                        column: x => x.RfqId,
                        principalTable: "RequestForQuotations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementRfqOpeningRegisters_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementRfqEvaluations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RfqId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OpeningRegisterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AwardMode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    RecommendationReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    EvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    MethodRuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MethodRuleCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    WorkflowDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovalReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubmittedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedByName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedByName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ApprovalActorsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
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
                    table.PrimaryKey("PK_ProcurementRfqEvaluations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcurementRfqEvaluations_ProcurementPolicyMethodRules_MethodRuleId",
                        column: x => x.MethodRuleId,
                        principalTable: "ProcurementPolicyMethodRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementRfqEvaluations_ProcurementRfqOpeningRegisters_OpeningRegisterId",
                        column: x => x.OpeningRegisterId,
                        principalTable: "ProcurementRfqOpeningRegisters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementRfqEvaluations_RequestForQuotations_RfqId",
                        column: x => x.RfqId,
                        principalTable: "RequestForQuotations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementRfqEvaluations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementRfqEvaluations_WorkflowInstances_WorkflowInstanceId",
                        column: x => x.WorkflowInstanceId,
                        principalTable: "WorkflowInstances",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ProcurementRfqOpeningParticipants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OpeningRegisterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParticipantUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ParticipantName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    RoleName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    IsObserver = table.Column<bool>(type: "bit", nullable: false),
                    SignedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SignatureReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
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
                    table.PrimaryKey("PK_ProcurementRfqOpeningParticipants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcurementRfqOpeningParticipants_ProcurementRfqOpeningRegisters_OpeningRegisterId",
                        column: x => x.OpeningRegisterId,
                        principalTable: "ProcurementRfqOpeningRegisters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementRfqOpeningParticipants_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementRfqReceipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RfqId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceiptSequence = table.Column<int>(type: "int", nullable: false),
                    ReceiptNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SubmissionDeadlineUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Disposition = table.Column<int>(type: "int", nullable: false),
                    SealedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SealedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OpeningRegisterId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OpenedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubmissionSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
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
                    table.PrimaryKey("PK_ProcurementRfqReceipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcurementRfqReceipts_BusinessPartners_BusinessPartnerId",
                        column: x => x.BusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementRfqReceipts_ProcurementRfqOpeningRegisters_OpeningRegisterId",
                        column: x => x.OpeningRegisterId,
                        principalTable: "ProcurementRfqOpeningRegisters",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProcurementRfqReceipts_RequestForQuotationQuotes_QuoteId",
                        column: x => x.QuoteId,
                        principalTable: "RequestForQuotationQuotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementRfqReceipts_RequestForQuotations_RfqId",
                        column: x => x.RfqId,
                        principalTable: "RequestForQuotations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementRfqReceipts_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementRfqEvaluationLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvaluationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RfqItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TechnicalScore = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    CommercialScore = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    TotalScore = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RecommendationReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_ProcurementRfqEvaluationLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcurementRfqEvaluationLines_BusinessPartners_BusinessPartnerId",
                        column: x => x.BusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementRfqEvaluationLines_ProcurementRfqEvaluations_EvaluationId",
                        column: x => x.EvaluationId,
                        principalTable: "ProcurementRfqEvaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementRfqEvaluationLines_RequestForQuotationItems_RfqItemId",
                        column: x => x.RfqItemId,
                        principalTable: "RequestForQuotationItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementRfqEvaluationLines_RequestForQuotationQuotes_QuoteId",
                        column: x => x.QuoteId,
                        principalTable: "RequestForQuotationQuotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementRfqEvaluationLines_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementRfqOpeningEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OpeningRegisterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceiptNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Disposition = table.Column<int>(type: "int", nullable: false),
                    DeclaredAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SecurityReference = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    QuoteIntegrityHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_ProcurementRfqOpeningEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcurementRfqOpeningEntries_BusinessPartners_BusinessPartnerId",
                        column: x => x.BusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementRfqOpeningEntries_ProcurementRfqOpeningRegisters_OpeningRegisterId",
                        column: x => x.OpeningRegisterId,
                        principalTable: "ProcurementRfqOpeningRegisters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementRfqOpeningEntries_ProcurementRfqReceipts_ReceiptId",
                        column: x => x.ReceiptId,
                        principalTable: "ProcurementRfqReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementRfqOpeningEntries_RequestForQuotationQuotes_QuoteId",
                        column: x => x.QuoteId,
                        principalTable: "RequestForQuotationQuotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementRfqOpeningEntries_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRfqEvaluationLines_BusinessPartnerId",
                table: "ProcurementRfqEvaluationLines",
                column: "BusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRfqEvaluationLines_EvaluationId",
                table: "ProcurementRfqEvaluationLines",
                column: "EvaluationId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRfqEvaluationLines_QuoteId",
                table: "ProcurementRfqEvaluationLines",
                column: "QuoteId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRfqEvaluationLines_RfqItemId",
                table: "ProcurementRfqEvaluationLines",
                column: "RfqItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRfqEvaluationLines_TenantId",
                table: "ProcurementRfqEvaluationLines",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRfqEvaluations_MethodRuleId",
                table: "ProcurementRfqEvaluations",
                column: "MethodRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRfqEvaluations_OpeningRegisterId",
                table: "ProcurementRfqEvaluations",
                column: "OpeningRegisterId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRfqEvaluations_RfqId",
                table: "ProcurementRfqEvaluations",
                column: "RfqId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRfqEvaluations_TenantId",
                table: "ProcurementRfqEvaluations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRfqEvaluations_WorkflowInstanceId",
                table: "ProcurementRfqEvaluations",
                column: "WorkflowInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRfqOpeningEntries_BusinessPartnerId",
                table: "ProcurementRfqOpeningEntries",
                column: "BusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRfqOpeningEntries_OpeningRegisterId",
                table: "ProcurementRfqOpeningEntries",
                column: "OpeningRegisterId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRfqOpeningEntries_QuoteId",
                table: "ProcurementRfqOpeningEntries",
                column: "QuoteId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRfqOpeningEntries_ReceiptId",
                table: "ProcurementRfqOpeningEntries",
                column: "ReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRfqOpeningEntries_TenantId",
                table: "ProcurementRfqOpeningEntries",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRfqOpeningParticipants_OpeningRegisterId",
                table: "ProcurementRfqOpeningParticipants",
                column: "OpeningRegisterId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRfqOpeningParticipants_TenantId",
                table: "ProcurementRfqOpeningParticipants",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRfqOpeningRegisters_RfqId",
                table: "ProcurementRfqOpeningRegisters",
                column: "RfqId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRfqOpeningRegisters_TenantId",
                table: "ProcurementRfqOpeningRegisters",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRfqReceipts_BusinessPartnerId",
                table: "ProcurementRfqReceipts",
                column: "BusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRfqReceipts_OpeningRegisterId",
                table: "ProcurementRfqReceipts",
                column: "OpeningRegisterId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRfqReceipts_QuoteId",
                table: "ProcurementRfqReceipts",
                column: "QuoteId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRfqReceipts_RfqId",
                table: "ProcurementRfqReceipts",
                column: "RfqId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRfqReceipts_TenantId",
                table: "ProcurementRfqReceipts",
                column: "TenantId");

            migrationBuilder.Sql("""
                CREATE TRIGGER [dbo].[TR_ProcurementRfqReceipts_Immutable]
                ON [dbo].[ProcurementRfqReceipts]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id = d.Id WHERE i.Id IS NULL)
                        THROW 51080, 'RFQ quotation receipts are append-only.', 1;
                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        LEFT JOIN [dbo].[RequestForQuotations] r ON r.Id = i.RfqId AND r.TenantId = i.TenantId
                        LEFT JOIN [dbo].[RequestForQuotationQuotes] q ON q.Id = i.QuoteId AND q.RfqId = i.RfqId AND q.BusinessPartnerId = i.BusinessPartnerId AND q.TenantId = i.TenantId
                        LEFT JOIN [dbo].[BusinessPartners] bp ON bp.Id = i.BusinessPartnerId AND bp.TenantId = i.TenantId
                        LEFT JOIN [dbo].[ProcurementRfqOpeningRegisters] o ON o.Id = i.OpeningRegisterId AND o.RfqId = i.RfqId AND o.TenantId = i.TenantId
                        WHERE r.Id IS NULL OR q.Id IS NULL OR bp.Id IS NULL OR (i.OpeningRegisterId IS NOT NULL AND o.Id IS NULL))
                        THROW 51081, 'RFQ receipt tenant or source lineage is invalid.', 1;
                    IF EXISTS (
                        SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
                        WHERE d.OpeningRegisterId IS NOT NULL OR d.OpenedAtUtc IS NOT NULL
                           OR i.OpeningRegisterId IS NULL OR i.OpenedAtUtc IS NULL
                           OR i.TenantId <> d.TenantId OR i.RfqId <> d.RfqId OR i.QuoteId <> d.QuoteId
                           OR i.BusinessPartnerId <> d.BusinessPartnerId OR i.ReceiptSequence <> d.ReceiptSequence
                           OR i.ReceiptNumber <> d.ReceiptNumber OR i.SubmissionDeadlineUtc <> d.SubmissionDeadlineUtc
                           OR i.ReceivedAtUtc <> d.ReceivedAtUtc OR i.Disposition <> d.Disposition
                           OR i.SealedByUserId <> d.SealedByUserId OR i.SealedAtUtc <> d.SealedAtUtc
                           OR i.SubmissionSnapshotJson <> d.SubmissionSnapshotJson OR i.IntegrityHash <> d.IntegrityHash
                           OR i.IsDeleted <> d.IsDeleted)
                        THROW 51082, 'Only the first controlled opening link may update a sealed RFQ receipt.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER [dbo].[TR_ProcurementRfqOpeningRegisters_Immutable]
                ON [dbo].[ProcurementRfqOpeningRegisters]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM deleted) THROW 51083, 'RFQ opening registers are immutable.', 1;
                    IF EXISTS (SELECT 1 FROM inserted i LEFT JOIN [dbo].[RequestForQuotations] r ON r.Id = i.RfqId AND r.TenantId = i.TenantId WHERE r.Id IS NULL)
                        THROW 51084, 'RFQ opening-register tenant lineage is invalid.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER [dbo].[TR_ProcurementRfqOpeningParticipants_Immutable]
                ON [dbo].[ProcurementRfqOpeningParticipants]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM deleted) THROW 51085, 'RFQ opening participants are immutable.', 1;
                    IF EXISTS (SELECT 1 FROM inserted i LEFT JOIN [dbo].[ProcurementRfqOpeningRegisters] r ON r.Id = i.OpeningRegisterId AND r.TenantId = i.TenantId WHERE r.Id IS NULL)
                        THROW 51086, 'RFQ opening-participant tenant lineage is invalid.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER [dbo].[TR_ProcurementRfqOpeningEntries_Immutable]
                ON [dbo].[ProcurementRfqOpeningEntries]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM deleted) THROW 51087, 'RFQ opening entries are immutable.', 1;
                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        LEFT JOIN [dbo].[ProcurementRfqOpeningRegisters] o ON o.Id = i.OpeningRegisterId AND o.TenantId = i.TenantId
                        LEFT JOIN [dbo].[ProcurementRfqReceipts] r ON r.Id = i.ReceiptId AND r.RfqId = o.RfqId AND r.QuoteId = i.QuoteId AND r.BusinessPartnerId = i.BusinessPartnerId AND r.TenantId = i.TenantId
                        LEFT JOIN [dbo].[RequestForQuotationQuotes] q ON q.Id = i.QuoteId AND q.RfqId = o.RfqId AND q.BusinessPartnerId = i.BusinessPartnerId AND q.TenantId = i.TenantId
                        LEFT JOIN [dbo].[BusinessPartners] bp ON bp.Id = i.BusinessPartnerId AND bp.TenantId = i.TenantId
                        WHERE o.Id IS NULL OR r.Id IS NULL OR q.Id IS NULL OR bp.Id IS NULL)
                        THROW 51088, 'RFQ opening-entry tenant or source lineage is invalid.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER [dbo].[TR_ProcurementRfqEvaluations_Lifecycle]
                ON [dbo].[ProcurementRfqEvaluations]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id = d.Id WHERE i.Id IS NULL)
                        THROW 51089, 'RFQ evaluations cannot be deleted.', 1;
                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        LEFT JOIN [dbo].[RequestForQuotations] r ON r.Id = i.RfqId AND r.TenantId = i.TenantId
                        LEFT JOIN [dbo].[ProcurementRfqOpeningRegisters] o ON o.Id = i.OpeningRegisterId AND o.RfqId = i.RfqId AND o.TenantId = i.TenantId
                        LEFT JOIN [dbo].[ProcurementPolicyMethodRules] m ON m.Id = i.MethodRuleId AND m.TenantId = i.TenantId
                        LEFT JOIN [dbo].[WorkflowInstances] w ON w.Id = i.WorkflowInstanceId AND w.TenantId = i.TenantId
                        WHERE r.Id IS NULL OR o.Id IS NULL OR m.Id IS NULL OR (i.WorkflowInstanceId IS NOT NULL AND w.Id IS NULL))
                        THROW 51090, 'RFQ evaluation tenant or control lineage is invalid.', 1;
                    IF EXISTS (
                        SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
                        WHERE NOT ((d.Status = 0 AND i.Status IN (0,1)) OR (d.Status = 1 AND i.Status IN (1,2,3)))
                           OR i.TenantId <> d.TenantId OR i.RfqId <> d.RfqId OR i.OpeningRegisterId <> d.OpeningRegisterId
                           OR i.MethodRuleId <> d.MethodRuleId OR i.MethodRuleCode <> d.MethodRuleCode
                           OR ISNULL(i.WorkflowDefinitionId, '00000000-0000-0000-0000-000000000000') <> ISNULL(d.WorkflowDefinitionId, '00000000-0000-0000-0000-000000000000')
                           OR (d.WorkflowInstanceId IS NOT NULL AND i.WorkflowInstanceId <> d.WorkflowInstanceId)
                           OR (d.Status <> 0 AND (i.AwardMode <> d.AwardMode OR i.RecommendationReason <> d.RecommendationReason OR i.EvidenceReference <> d.EvidenceReference))
                           OR i.IsDeleted <> d.IsDeleted)
                        THROW 51091, 'The RFQ evaluation lifecycle transition or immutable lineage is invalid.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER [dbo].[TR_ProcurementRfqEvaluationLines_Lifecycle]
                ON [dbo].[ProcurementRfqEvaluationLines]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (
                        SELECT 1 FROM (SELECT EvaluationId FROM inserted UNION SELECT EvaluationId FROM deleted) x
                        LEFT JOIN [dbo].[ProcurementRfqEvaluations] e ON e.Id = x.EvaluationId
                        WHERE e.Id IS NULL OR e.Status <> 0)
                        THROW 51092, 'RFQ evaluation lines may change only while the evaluation is Draft.', 1;
                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        JOIN [dbo].[ProcurementRfqEvaluations] e ON e.Id = i.EvaluationId
                        JOIN [dbo].[RequestForQuotations] r ON r.Id = e.RfqId
                        LEFT JOIN [dbo].[RequestForQuotationItems] ri ON ri.Id = i.RfqItemId AND ri.RfqId = e.RfqId AND ri.TenantId = i.TenantId
                        LEFT JOIN [dbo].[RequestForQuotationQuotes] q ON q.Id = i.QuoteId AND q.RfqId = e.RfqId AND q.BusinessPartnerId = i.BusinessPartnerId AND q.TenantId = i.TenantId
                        LEFT JOIN [dbo].[BusinessPartners] bp ON bp.Id = i.BusinessPartnerId AND bp.TenantId = i.TenantId
                        WHERE e.TenantId <> i.TenantId OR r.TenantId <> i.TenantId OR ri.Id IS NULL OR q.Id IS NULL OR bp.Id IS NULL)
                        THROW 51093, 'RFQ evaluation-line tenant or source lineage is invalid.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER [dbo].[TR_RequestForQuotations_StatutoryLifecycleGuard]
                ON [dbo].[RequestForQuotations]
                AFTER UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (
                        SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
                        WHERE (d.Status <> 'Draft' AND
                              (ISNULL(i.SubmissionDeadline, '19000101') <> ISNULL(d.SubmissionDeadline, '19000101')
                               OR i.Title <> d.Title OR ISNULL(i.Description, '') <> ISNULL(d.Description, '')
                               OR ISNULL(i.ExternalRecipientEmails, '') <> ISNULL(d.ExternalRecipientEmails, '')
                               OR i.Currency <> d.Currency OR i.EstimatedValue <> d.EstimatedValue
                               OR i.IsDeleted <> d.IsDeleted))
                           OR (d.Status = 'Draft' AND i.Status NOT IN ('Draft','Sent','Cancelled'))
                           OR (d.Status = 'Sent' AND i.Status NOT IN ('Sent','Evaluation','Cancelled'))
                           OR (d.Status = 'Evaluation' AND i.Status NOT IN ('Evaluation','PendingApproval','Cancelled'))
                           OR (d.Status = 'PendingApproval' AND i.Status NOT IN ('PendingApproval','Approved','Rejected'))
                           OR (d.Status = 'Approved' AND i.Status NOT IN ('Approved','Awarded'))
                           OR (d.Status IN ('Awarded','Rejected','Cancelled','Closed') AND i.Status <> d.Status))
                        THROW 51094, 'RFQ issue terms or statutory lifecycle transition is invalid.', 1;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_RequestForQuotations_StatutoryLifecycleGuard]");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementRfqEvaluationLines_Lifecycle]");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementRfqEvaluations_Lifecycle]");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementRfqOpeningEntries_Immutable]");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementRfqOpeningParticipants_Immutable]");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementRfqOpeningRegisters_Immutable]");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementRfqReceipts_Immutable]");

            migrationBuilder.DropTable(
                name: "ProcurementRfqEvaluationLines");

            migrationBuilder.DropTable(
                name: "ProcurementRfqOpeningEntries");

            migrationBuilder.DropTable(
                name: "ProcurementRfqOpeningParticipants");

            migrationBuilder.DropTable(
                name: "ProcurementRfqEvaluations");

            migrationBuilder.DropTable(
                name: "ProcurementRfqReceipts");

            migrationBuilder.DropTable(
                name: "ProcurementRfqOpeningRegisters");
        }
    }
}
