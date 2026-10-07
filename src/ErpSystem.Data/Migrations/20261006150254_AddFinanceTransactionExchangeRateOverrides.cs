using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFinanceTransactionExchangeRateOverrides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FinanceExchangeRateOverrideRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceDocumentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SourceDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceDocumentReference = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    TransactionCurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    FunctionalCurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    GovernedExchangeRateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GovernedRate = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    GovernedRateSource = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    GovernedRateEffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GovernedRateType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    GovernedQuoteSide = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RequestedRate = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    SourceSnapshotHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ApprovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RejectedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SupersededByRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SupersededAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SupersessionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ConsumedByPostingEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ConsumedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_FinanceExchangeRateOverrideRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinanceExchangeRateOverrideRequests_ExchangeRates_GovernedExchangeRateId",
                        column: x => x.GovernedExchangeRateId,
                        principalTable: "ExchangeRates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceExchangeRateOverrideRequests_FinancePostingEvents_ConsumedByPostingEventId",
                        column: x => x.ConsumedByPostingEventId,
                        principalTable: "FinancePostingEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceExchangeRateOverrideRequests_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceExchangeRateOverrideRequests_WorkflowInstances_WorkflowInstanceId",
                        column: x => x.WorkflowInstanceId,
                        principalTable: "WorkflowInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceExchangeRateOverrideRequests_ConsumedByPostingEventId",
                table: "FinanceExchangeRateOverrideRequests",
                column: "ConsumedByPostingEventId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceExchangeRateOverrideRequests_GovernedExchangeRateId",
                table: "FinanceExchangeRateOverrideRequests",
                column: "GovernedExchangeRateId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceExchangeRateOverrideRequests_TenantId_SourceDocumentType_SourceDocumentId_TransactionCurrencyCode",
                table: "FinanceExchangeRateOverrideRequests",
                columns: new[] { "TenantId", "SourceDocumentType", "SourceDocumentId", "TransactionCurrencyCode" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [Status] IN ('PendingApproval', 'Approved')");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceExchangeRateOverrideRequests_TenantId_SourceDocumentType_SourceDocumentId_TransactionCurrencyCode_RequestedAtUtc",
                table: "FinanceExchangeRateOverrideRequests",
                columns: new[] { "TenantId", "SourceDocumentType", "SourceDocumentId", "TransactionCurrencyCode", "RequestedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceExchangeRateOverrideRequests_WorkflowInstanceId",
                table: "FinanceExchangeRateOverrideRequests",
                column: "WorkflowInstanceId",
                unique: true,
                filter: "[WorkflowInstanceId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FinanceExchangeRateOverrideRequests");
        }
    }
}
