using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260902080000_AddRecurringJournalAutomaticReversalControl")]
public sealed class AddRecurringJournalAutomaticReversalControl : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(name: "ReversalAuthorizedAt", table: "RecurringJournalOccurrences", type: "datetime2", nullable: true);
        migrationBuilder.AddColumn<Guid>(name: "ReversalAuthorizedByUserId", table: "RecurringJournalOccurrences", type: "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<int>(name: "ReversalAttemptCount", table: "RecurringJournalOccurrences", type: "int", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<string>(name: "ReversalError", table: "RecurringJournalOccurrences", type: "nvarchar(2000)", maxLength: 2000, nullable: true);
        migrationBuilder.AddColumn<DateTime>(name: "ReversalLastAttemptAt", table: "RecurringJournalOccurrences", type: "datetime2", nullable: true);
        migrationBuilder.AddColumn<Guid>(name: "ReversalPostingEventId", table: "RecurringJournalOccurrences", type: "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<string>(name: "ReversalProcessedBy", table: "RecurringJournalOccurrences", type: "nvarchar(100)", maxLength: 100, nullable: true);
        migrationBuilder.AddColumn<int>(name: "ReversalStatus", table: "RecurringJournalOccurrences", type: "int", nullable: false, defaultValue: 0);

        // Existing occurrences were approved before the checker wording explicitly
        // authorised an automatic posting. They remain non-automatic; only approvals
        // completed after this migration transition a reversal to Scheduled.
        migrationBuilder.CreateIndex(
            name: "IX_RecurringJournalOccurrences_TenantId_ReversalStatus_ReversalDueDate",
            table: "RecurringJournalOccurrences",
            columns: new[] { "TenantId", "ReversalStatus", "ReversalDueDate" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_RecurringJournalOccurrences_TenantId_ReversalStatus_ReversalDueDate", table: "RecurringJournalOccurrences");
        migrationBuilder.DropColumn(name: "ReversalAuthorizedAt", table: "RecurringJournalOccurrences");
        migrationBuilder.DropColumn(name: "ReversalAuthorizedByUserId", table: "RecurringJournalOccurrences");
        migrationBuilder.DropColumn(name: "ReversalAttemptCount", table: "RecurringJournalOccurrences");
        migrationBuilder.DropColumn(name: "ReversalError", table: "RecurringJournalOccurrences");
        migrationBuilder.DropColumn(name: "ReversalLastAttemptAt", table: "RecurringJournalOccurrences");
        migrationBuilder.DropColumn(name: "ReversalPostingEventId", table: "RecurringJournalOccurrences");
        migrationBuilder.DropColumn(name: "ReversalProcessedBy", table: "RecurringJournalOccurrences");
        migrationBuilder.DropColumn(name: "ReversalStatus", table: "RecurringJournalOccurrences");
    }
}
