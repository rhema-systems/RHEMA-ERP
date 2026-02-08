using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemExceptionLogDedupAndRetentionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add dedup + retention fields.
            migrationBuilder.AddColumn<string>(
                name: "Fingerprint",
                table: "SystemExceptionLogs",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OccurrenceCount",
                table: "SystemExceptionLogs",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<DateTime>(
                name: "FirstOccurredAt",
                table: "SystemExceptionLogs",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()"
            );

            migrationBuilder.AddColumn<DateTime>(
                name: "LastOccurredAt",
                table: "SystemExceptionLogs",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()"
            );

            // Backfill existing rows.
            migrationBuilder.Sql(@"
UPDATE SystemExceptionLogs
SET
    Fingerprint = LOWER(CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', CONVERT(NVARCHAR(36), Id)), 2)),
    OccurrenceCount = CASE WHEN OccurrenceCount IS NULL OR OccurrenceCount = 0 THEN 1 ELSE OccurrenceCount END,
    FirstOccurredAt = CASE WHEN FirstOccurredAt IS NULL OR FirstOccurredAt = '0001-01-01T00:00:00' THEN CreatedAt ELSE FirstOccurredAt END,
    LastOccurredAt = CASE WHEN LastOccurredAt IS NULL OR LastOccurredAt = '0001-01-01T00:00:00' THEN CreatedAt ELSE LastOccurredAt END
WHERE Fingerprint IS NULL OR Fingerprint = '';
");

            // Make Fingerprint required after backfill.
            migrationBuilder.AlterColumn<string>(
                name: "Fingerprint",
                table: "SystemExceptionLogs",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldNullable: true);

            // Indexes for dedup & retention.
            migrationBuilder.CreateIndex(
                name: "IX_SystemExceptionLogs_TenantId_Fingerprint",
                table: "SystemExceptionLogs",
                columns: new[] { "TenantId", "Fingerprint" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SystemExceptionLogs_TenantId_LastOccurredAt",
                table: "SystemExceptionLogs",
                columns: new[] { "TenantId", "LastOccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SystemExceptionLogs_TenantId_Fingerprint",
                table: "SystemExceptionLogs");

            migrationBuilder.DropIndex(
                name: "IX_SystemExceptionLogs_TenantId_LastOccurredAt",
                table: "SystemExceptionLogs");

            migrationBuilder.DropColumn(
                name: "Fingerprint",
                table: "SystemExceptionLogs");

            migrationBuilder.DropColumn(
                name: "OccurrenceCount",
                table: "SystemExceptionLogs");

            migrationBuilder.DropColumn(
                name: "FirstOccurredAt",
                table: "SystemExceptionLogs");

            migrationBuilder.DropColumn(
                name: "LastOccurredAt",
                table: "SystemExceptionLogs");
        }
    }
}
