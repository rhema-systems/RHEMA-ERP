using ErpSystem.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Extends the Finance-owned reservation evidence with producer/version/currency lineage and
/// immutable idempotency operations. It does not alter Procurement-owned tables.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260824160000_AddGenericFinanceBudgetCommitmentContract")]
public class AddGenericFinanceBudgetCommitmentContract : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "BudgetDate", table: "FinanceBudgetReservations", type: "datetime2",
            nullable: false, defaultValue: new DateTime(1900, 1, 1));
        migrationBuilder.AddColumn<decimal>(
            name: "ExchangeRate", table: "FinanceBudgetReservations", type: "decimal(18,6)",
            nullable: false, defaultValue: 1m);
        migrationBuilder.AddColumn<Guid>(
            name: "ExchangeRateId", table: "FinanceBudgetReservations", type: "uniqueidentifier",
            nullable: true);
        migrationBuilder.AddColumn<int>(
            name: "ReservationVersion", table: "FinanceBudgetReservations", type: "int",
            nullable: false, defaultValue: 1);
        migrationBuilder.AddColumn<string>(
            name: "SourceDocumentReference", table: "FinanceBudgetReservations", type: "nvarchar(100)",
            maxLength: 100, nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "SourceLineIdsJson", table: "FinanceBudgetReservations", type: "nvarchar(2000)",
            maxLength: 2000, nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "SourceVersion", table: "FinanceBudgetReservations", type: "nvarchar(64)",
            maxLength: 64, nullable: true);
        migrationBuilder.AddColumn<decimal>(
            name: "TransactionAmount", table: "FinanceBudgetReservations", type: "decimal(18,2)",
            nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<string>(
            name: "TransactionCurrencyCode", table: "FinanceBudgetReservations", type: "nvarchar(3)",
            maxLength: 3, nullable: false, defaultValue: "");

        // Existing manual-journal reservations already store their authoritative currency in
        // CurrencyCode. Backfill that exact evidence before enforcing the generic contract.
        migrationBuilder.Sql(@"
UPDATE [FinanceBudgetReservations]
SET [TransactionCurrencyCode] = [CurrencyCode]
WHERE LEN([TransactionCurrencyCode]) <> 3;");
        migrationBuilder.AddCheckConstraint(
            name: "CK_FinanceBudgetReservations_TransactionCurrencyCode",
            table: "FinanceBudgetReservations",
            sql: "LEN([TransactionCurrencyCode]) = 3");
        migrationBuilder.AddCheckConstraint(
            name: "CK_FinanceBudgetReservations_TransactionAmount",
            table: "FinanceBudgetReservations",
            sql: "[TransactionAmount] >= 0");
        migrationBuilder.AddCheckConstraint(
            name: "CK_FinanceBudgetReservations_ExchangeRate",
            table: "FinanceBudgetReservations",
            sql: "[ExchangeRate] > 0");
        migrationBuilder.AddCheckConstraint(
            name: "CK_FinanceBudgetReservations_ReservationVersion",
            table: "FinanceBudgetReservations",
            sql: "[ReservationVersion] >= 1");

        migrationBuilder.CreateTable(
            name: "FinanceBudgetReservationOperations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FinanceBudgetReservationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ReservationIdsJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                OperationType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                PayloadHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                PriorStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                ResultStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                PriorReservedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                ResultReservedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                JournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                PostingEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                table.PrimaryKey("PK_FinanceBudgetReservationOperations", x => x.Id);
                table.ForeignKey(
                    name: "FK_FinanceBudgetReservationOperations_FinanceBudgetReservations_FinanceBudgetReservationId",
                    column: x => x.FinanceBudgetReservationId,
                    principalTable: "FinanceBudgetReservations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_FinanceBudgetReservationOperations_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_FinanceBudgetReservationOperations_FinanceBudgetReservationId",
            table: "FinanceBudgetReservationOperations",
            column: "FinanceBudgetReservationId");
        migrationBuilder.CreateIndex(
            name: "IX_FinanceBudgetReservationOperations_PostingEventId",
            table: "FinanceBudgetReservationOperations",
            column: "PostingEventId");
        migrationBuilder.CreateIndex(
            name: "IX_FinanceBudgetReservationOperations_TenantId_FinanceBudgetReservationId_OccurredAt",
            table: "FinanceBudgetReservationOperations",
            columns: new[] { "TenantId", "FinanceBudgetReservationId", "OccurredAt" });
        migrationBuilder.CreateIndex(
            name: "IX_FinanceBudgetReservationOperations_TenantId_IdempotencyKey",
            table: "FinanceBudgetReservationOperations",
            columns: new[] { "TenantId", "IdempotencyKey" },
            unique: true,
            filter: "[IsDeleted] = 0");
        migrationBuilder.CreateIndex(
            name: "IX_FinanceBudgetReservationOperations_TenantId",
            table: "FinanceBudgetReservationOperations",
            column: "TenantId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "FinanceBudgetReservationOperations");
        migrationBuilder.DropCheckConstraint(
            name: "CK_FinanceBudgetReservations_TransactionCurrencyCode",
            table: "FinanceBudgetReservations");
        migrationBuilder.DropCheckConstraint(
            name: "CK_FinanceBudgetReservations_TransactionAmount",
            table: "FinanceBudgetReservations");
        migrationBuilder.DropCheckConstraint(
            name: "CK_FinanceBudgetReservations_ExchangeRate",
            table: "FinanceBudgetReservations");
        migrationBuilder.DropCheckConstraint(
            name: "CK_FinanceBudgetReservations_ReservationVersion",
            table: "FinanceBudgetReservations");
        migrationBuilder.DropColumn(name: "BudgetDate", table: "FinanceBudgetReservations");
        migrationBuilder.DropColumn(name: "ExchangeRate", table: "FinanceBudgetReservations");
        migrationBuilder.DropColumn(name: "ExchangeRateId", table: "FinanceBudgetReservations");
        migrationBuilder.DropColumn(name: "ReservationVersion", table: "FinanceBudgetReservations");
        migrationBuilder.DropColumn(name: "SourceDocumentReference", table: "FinanceBudgetReservations");
        migrationBuilder.DropColumn(name: "SourceLineIdsJson", table: "FinanceBudgetReservations");
        migrationBuilder.DropColumn(name: "SourceVersion", table: "FinanceBudgetReservations");
        migrationBuilder.DropColumn(name: "TransactionAmount", table: "FinanceBudgetReservations");
        migrationBuilder.DropColumn(name: "TransactionCurrencyCode", table: "FinanceBudgetReservations");
    }
}
