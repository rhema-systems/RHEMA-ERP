using ErpSystem.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Adds private-storage evidence to the existing append-only Finance controlled-document issue
/// register. Existing cash/bank issue rows remain valid hash-only records; retained statutory PDF
/// artifacts opt into these nullable fields without rewriting prior audit evidence.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260825141500_AddFinanceControlledDocumentRetention")]
public class AddFinanceControlledDocumentRetention : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(
            name: "FileSize",
            table: "FinanceControlledDocumentIssues",
            type: "bigint",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "RetainUntilUtc",
            table: "FinanceControlledDocumentIssues",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "StoragePath",
            table: "FinanceControlledDocumentIssues",
            type: "nvarchar(1000)",
            maxLength: 1000,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "StorageProvider",
            table: "FinanceControlledDocumentIssues",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddCheckConstraint(
            name: "CK_FinanceControlledDocumentIssues_RetainedArtifact",
            table: "FinanceControlledDocumentIssues",
            sql: "([StoragePath] IS NULL AND [StorageProvider] IS NULL AND [FileSize] IS NULL AND [RetainUntilUtc] IS NULL) OR ([StoragePath] IS NOT NULL AND [StorageProvider] IS NOT NULL AND [FileSize] > 0 AND [RetainUntilUtc] IS NOT NULL)");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_FinanceControlledDocumentIssues_RetainedArtifact",
            table: "FinanceControlledDocumentIssues");
        migrationBuilder.DropColumn(name: "FileSize", table: "FinanceControlledDocumentIssues");
        migrationBuilder.DropColumn(name: "RetainUntilUtc", table: "FinanceControlledDocumentIssues");
        migrationBuilder.DropColumn(name: "StoragePath", table: "FinanceControlledDocumentIssues");
        migrationBuilder.DropColumn(name: "StorageProvider", table: "FinanceControlledDocumentIssues");
    }
}
