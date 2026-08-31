using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260830143000_AddJournalApprovalWithdrawalMetadata")]
public sealed class AddJournalApprovalWithdrawalMetadata : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "WithdrawalReason",
            table: "JournalEntries",
            type: "nvarchar(1000)",
            maxLength: 1000,
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "WithdrawnByUserId",
            table: "JournalEntries",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "WithdrawnDate",
            table: "JournalEntries",
            type: "datetime2",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "WithdrawalReason",
            table: "JournalEntries");

        migrationBuilder.DropColumn(
            name: "WithdrawnByUserId",
            table: "JournalEntries");

        migrationBuilder.DropColumn(
            name: "WithdrawnDate",
            table: "JournalEntries");
    }
}
