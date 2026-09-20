using ErpSystem.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Adds Finance-owned budget reservation and override evidence. This deliberately does not
/// alter ProcurementBudget or any Procurement commitment table.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260824080000_AddFinanceBudgetControlFoundation")]
public class AddFinanceBudgetControlFoundation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "FinanceBudgetOverrideRequests",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SourceDocumentType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                SourceDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                EvaluationHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                RequestedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                ShortfallAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                ApprovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                RejectedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                RejectedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                RejectionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FinanceBudgetOverrideRequests", x => x.Id);
                table.ForeignKey("FK_FinanceBudgetOverrideRequests_Tenants_TenantId", x => x.TenantId, "Tenants", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "FinanceBudgetReservations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                BudgetScenarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                BudgetReturnId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                BudgetEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FiscalPeriodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SegmentValueId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                CurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                SourceDocumentType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                SourceDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ReservedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                BudgetAmountSnapshot = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                PostedActualSnapshot = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                OtherReservationsSnapshot = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                AvailableBeforeReservationSnapshot = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                EvaluationHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                OverrideRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ReservedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ReservedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                ReleasedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ReleasedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                ReleaseReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                ConsumedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ConsumedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                JournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                PostingEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FinanceBudgetReservations", x => x.Id);
                table.ForeignKey("FK_FinanceBudgetReservations_Tenants_TenantId", x => x.TenantId, "Tenants", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex("IX_FinanceBudgetOverrideRequests_TenantId_SourceDocumentType_SourceDocumentId_EvaluationHash_Status",
            "FinanceBudgetOverrideRequests", new[] { "TenantId", "SourceDocumentType", "SourceDocumentId", "EvaluationHash", "Status" });
        migrationBuilder.CreateIndex("IX_FinanceBudgetOverrideRequests_WorkflowInstanceId",
            "FinanceBudgetOverrideRequests", "WorkflowInstanceId", unique: true, filter: "[WorkflowInstanceId] IS NOT NULL");
        migrationBuilder.CreateIndex("IX_FinanceBudgetOverrideRequests_TenantId",
            "FinanceBudgetOverrideRequests", "TenantId");

        migrationBuilder.CreateIndex("IX_FinanceBudgetReservations_PostingEventId",
            "FinanceBudgetReservations", "PostingEventId");
        migrationBuilder.CreateIndex("IX_FinanceBudgetReservations_TenantId_BudgetEntryId_Status",
            "FinanceBudgetReservations", new[] { "TenantId", "BudgetEntryId", "Status" });
        migrationBuilder.CreateIndex("IX_FinanceBudgetReservations_TenantId_SourceDocumentType_SourceDocumentId_BudgetEntryId",
            "FinanceBudgetReservations", new[] { "TenantId", "SourceDocumentType", "SourceDocumentId", "BudgetEntryId" },
            unique: true, filter: "[IsDeleted] = 0 AND [Status] = 'Reserved'");
        migrationBuilder.CreateIndex("IX_FinanceBudgetReservations_TenantId",
            "FinanceBudgetReservations", "TenantId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("FinanceBudgetReservations");
        migrationBuilder.DropTable("FinanceBudgetOverrideRequests");
    }
}
