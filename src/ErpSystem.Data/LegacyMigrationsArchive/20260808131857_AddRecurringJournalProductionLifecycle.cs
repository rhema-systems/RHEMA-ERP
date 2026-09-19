using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Adds durable maker-checker, occurrence review, posting and reversal-schedule
/// evidence to the recurring-journal foundation introduced in July 2026.
///
/// This migration is deliberately hand-scoped. EF also detected unrelated FX
/// precision drift in the accumulated snapshot; changing those columns here
/// would make this FR-GL-006 release unsafe and harder to review.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260808131857_AddRecurringJournalProductionLifecycle")]
public sealed class AddRecurringJournalProductionLifecycle : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("ReviewComment", "RecurringJournalTemplates", "nvarchar(1000)", maxLength: 1000, nullable: true);
        migrationBuilder.AddColumn<DateTime>("ReviewedAt", "RecurringJournalTemplates", "datetime2", nullable: true);
        migrationBuilder.AddColumn<Guid>("ReviewedByUserId", "RecurringJournalTemplates", "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<DateTime>("SubmittedAt", "RecurringJournalTemplates", "datetime2", nullable: true);
        migrationBuilder.AddColumn<Guid>("SubmittedByUserId", "RecurringJournalTemplates", "uniqueidentifier", nullable: true);

        migrationBuilder.AddColumn<DateTime>("PostedAt", "RecurringJournalOccurrences", "datetime2", nullable: true);
        migrationBuilder.AddColumn<Guid>("PostedByUserId", "RecurringJournalOccurrences", "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<DateOnly>("ReversalDueDate", "RecurringJournalOccurrences", "date", nullable: true);
        migrationBuilder.AddColumn<DateTime>("ReversedAt", "RecurringJournalOccurrences", "datetime2", nullable: true);
        migrationBuilder.AddColumn<string>("ReviewComment", "RecurringJournalOccurrences", "nvarchar(1000)", maxLength: 1000, nullable: true);
        migrationBuilder.AddColumn<DateTime>("ReviewedAt", "RecurringJournalOccurrences", "datetime2", nullable: true);
        migrationBuilder.AddColumn<Guid>("ReviewedByUserId", "RecurringJournalOccurrences", "uniqueidentifier", nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("ReviewComment", "RecurringJournalTemplates");
        migrationBuilder.DropColumn("ReviewedAt", "RecurringJournalTemplates");
        migrationBuilder.DropColumn("ReviewedByUserId", "RecurringJournalTemplates");
        migrationBuilder.DropColumn("SubmittedAt", "RecurringJournalTemplates");
        migrationBuilder.DropColumn("SubmittedByUserId", "RecurringJournalTemplates");

        migrationBuilder.DropColumn("PostedAt", "RecurringJournalOccurrences");
        migrationBuilder.DropColumn("PostedByUserId", "RecurringJournalOccurrences");
        migrationBuilder.DropColumn("ReversalDueDate", "RecurringJournalOccurrences");
        migrationBuilder.DropColumn("ReversedAt", "RecurringJournalOccurrences");
        migrationBuilder.DropColumn("ReviewComment", "RecurringJournalOccurrences");
        migrationBuilder.DropColumn("ReviewedAt", "RecurringJournalOccurrences");
        migrationBuilder.DropColumn("ReviewedByUserId", "RecurringJournalOccurrences");
    }
}
