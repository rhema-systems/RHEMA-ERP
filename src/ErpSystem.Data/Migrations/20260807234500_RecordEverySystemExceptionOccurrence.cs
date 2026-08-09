using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260807234500_RecordEverySystemExceptionOccurrence")]
public partial class RecordEverySystemExceptionOccurrence : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_SystemExceptionLogs_TenantId_Fingerprint",
            table: "SystemExceptionLogs");

        migrationBuilder.CreateIndex(
            name: "IX_SystemExceptionLogs_TenantId_Fingerprint",
            table: "SystemExceptionLogs",
            columns: new[] { "TenantId", "Fingerprint" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF EXISTS (
                SELECT 1
                FROM [dbo].[SystemExceptionLogs]
                WHERE [IsDeleted] = 0
                GROUP BY [TenantId], [Fingerprint]
                HAVING COUNT_BIG(*) > 1)
                THROW 51801, 'Cannot restore unique exception fingerprints while per-occurrence log rows exist.', 1;
            """);

        migrationBuilder.DropIndex(
            name: "IX_SystemExceptionLogs_TenantId_Fingerprint",
            table: "SystemExceptionLogs");

        migrationBuilder.CreateIndex(
            name: "IX_SystemExceptionLogs_TenantId_Fingerprint",
            table: "SystemExceptionLogs",
            columns: new[] { "TenantId", "Fingerprint" },
            unique: true);
    }
}
