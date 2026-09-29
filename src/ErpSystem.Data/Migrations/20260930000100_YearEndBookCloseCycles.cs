using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260930000100_YearEndBookCloseCycles")]
public sealed class YearEndBookCloseCycles : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddUniqueConstraint("AK_FiscalYears_TenantId_Id", "FiscalYears", new[] { "TenantId", "Id" });
        migrationBuilder.CreateTable(
            name: "YearEndBookCloseCycles",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FiscalYearId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AccountingBookId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AccountingBookCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                FunctionalCurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                CycleNumber = table.Column<int>(type: "int", nullable: false),
                PeriodAuthoritySnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                RetainedEarningsAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                ClosedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ClosedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                ClosingNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                ClosingJournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                NetIncomeTransferred = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                ReversalJournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ReopenedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ReopenedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                ReopenReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
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
                table.PrimaryKey("PK_YearEndBookCloseCycles", x => x.Id);
                table.CheckConstraint("CK_YearEndBookCloseCycles_NoDelete", "[IsDeleted] = 0");
                table.CheckConstraint("CK_YearEndBookCloseCycles_Status", "[Status] IN ('Closing', 'Closed', 'Reopened')");
                table.CheckConstraint("CK_YearEndBookCloseCycles_Cycle", "[CycleNumber] > 0");
                table.CheckConstraint("CK_YearEndBookCloseCycles_Reopen", "([Status] <> 'Reopened' AND [ReopenedAtUtc] IS NULL AND [ReopenedByUserId] IS NULL AND [ReopenReason] IS NULL AND [ReversalJournalEntryId] IS NULL) OR ([Status] = 'Reopened' AND [ReopenedAtUtc] IS NOT NULL AND [ReopenedByUserId] IS NOT NULL AND [ReopenReason] IS NOT NULL AND ([ClosingJournalEntryId] IS NULL OR [ReversalJournalEntryId] IS NOT NULL))");
                table.ForeignKey("FK_YearEndBookCloseCycles_Tenants_TenantId", x => x.TenantId, "Tenants", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_YearEndBookCloseCycles_FiscalYears_TenantId_FiscalYearId",
                    x => new { x.TenantId, x.FiscalYearId }, "FiscalYears", new[] { "TenantId", "Id" }, onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_YearEndBookCloseCycles_AccountingBooks_TenantId_AccountingBookId_AccountingBookCode",
                    x => new { x.TenantId, x.AccountingBookId, x.AccountingBookCode }, "AccountingBooks",
                    new[] { "TenantId", "Id", "Code" }, onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_YearEndBookCloseCycles_Accounts_TenantId_RetainedEarningsAccountId",
                    x => new { x.TenantId, x.RetainedEarningsAccountId }, "Accounts", new[] { "TenantId", "Id" }, onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_YearEndBookCloseCycles_JournalEntries_TenantId_ClosingJournalEntryId_AccountingBookId",
                    x => new { x.TenantId, x.ClosingJournalEntryId, x.AccountingBookId }, "JournalEntries",
                    new[] { "TenantId", "Id", "AccountingBookId" }, onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_YearEndBookCloseCycles_JournalEntries_TenantId_ReversalJournalEntryId_AccountingBookId",
                    x => new { x.TenantId, x.ReversalJournalEntryId, x.AccountingBookId }, "JournalEntries",
                    new[] { "TenantId", "Id", "AccountingBookId" }, onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex("IX_YearEndBookCloseCycles_TenantId_FiscalYearId_AccountingBookId_CycleNumber",
            "YearEndBookCloseCycles", new[] { "TenantId", "FiscalYearId", "AccountingBookId", "CycleNumber" }, unique: true);
        migrationBuilder.CreateIndex("IX_YearEndBookCloseCycles_TenantId_IdempotencyKey",
            "YearEndBookCloseCycles", new[] { "TenantId", "IdempotencyKey" }, unique: true);
        migrationBuilder.CreateIndex("IX_YearEndBookCloseCycles_TenantId_FiscalYearId_AccountingBookId",
            "YearEndBookCloseCycles", new[] { "TenantId", "FiscalYearId", "AccountingBookId" },
            unique: true, filter: "[Status] IN ('Closing', 'Closed')");
        migrationBuilder.CreateIndex("IX_YearEndBookCloseCycles_TenantId_AccountingBookId_AccountingBookCode",
            "YearEndBookCloseCycles", new[] { "TenantId", "AccountingBookId", "AccountingBookCode" });
        migrationBuilder.CreateIndex("IX_YearEndBookCloseCycles_TenantId_RetainedEarningsAccountId",
            "YearEndBookCloseCycles", new[] { "TenantId", "RetainedEarningsAccountId" });
        migrationBuilder.CreateIndex("IX_YearEndBookCloseCycles_TenantId_ClosingJournalEntryId_AccountingBookId",
            "YearEndBookCloseCycles", new[] { "TenantId", "ClosingJournalEntryId", "AccountingBookId" });
        migrationBuilder.CreateIndex("IX_YearEndBookCloseCycles_TenantId_ReversalJournalEntryId_AccountingBookId",
            "YearEndBookCloseCycles", new[] { "TenantId", "ReversalJournalEntryId", "AccountingBookId" });
        // Existing fiscal-year-wide closes remain unchanged and fail closed in the service.
        // Their original book authority must be reconciled through a separate governed migration.
        migrationBuilder.Sql("""
CREATE TRIGGER [TR_YearEndBookCloseCycles_ImmutableEvidence] ON [YearEndBookCloseCycles]
AFTER UPDATE, DELETE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id = d.Id WHERE i.Id IS NULL)
        THROW 51000, 'Book-year close evidence cannot be deleted.', 1;
    IF EXISTS (
        SELECT 1 FROM inserted i JOIN deleted d ON i.Id = d.Id
        WHERE EXISTS (
            SELECT i.TenantId, i.FiscalYearId, i.AccountingBookId, i.AccountingBookCode, i.FunctionalCurrencyCode,
                   i.CycleNumber, i.IdempotencyKey, i.RetainedEarningsAccountId, i.ClosedByUserId, i.ClosedAtUtc,
                   i.ClosingNotes, i.PeriodAuthoritySnapshotJson
            EXCEPT
            SELECT d.TenantId, d.FiscalYearId, d.AccountingBookId, d.AccountingBookCode, d.FunctionalCurrencyCode,
                   d.CycleNumber, d.IdempotencyKey, d.RetainedEarningsAccountId, d.ClosedByUserId, d.ClosedAtUtc,
                   d.ClosingNotes, d.PeriodAuthoritySnapshotJson)
          OR NOT ((d.Status = 'Closing' AND i.Status = 'Closed') OR (d.Status = 'Closed' AND i.Status = 'Reopened'))
          OR (d.Status = 'Closed' AND EXISTS (
              SELECT i.ClosingJournalEntryId, i.NetIncomeTransferred
              EXCEPT SELECT d.ClosingJournalEntryId, d.NetIncomeTransferred)))
        THROW 51000, 'Book-year close evidence is immutable; only close completion or an evidenced reopen is permitted.', 1;
END
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [YearEndBookCloseCycles]) THROW 51000, 'Cannot remove retained book-year close evidence. Archive through an approved data-retention process first.', 1;");
        migrationBuilder.DropTable("YearEndBookCloseCycles");
        migrationBuilder.DropUniqueConstraint("AK_FiscalYears_TenantId_Id", "FiscalYears");
    }
}
