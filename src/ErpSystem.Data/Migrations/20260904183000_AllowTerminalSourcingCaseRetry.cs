using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Preserves terminal sourcing attempts while allowing one governed successor
/// to reuse the unchanged immutable requisition release. Only Ready or InProgress
/// cases participate in the unique active-case boundary.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260904183000_AllowTerminalSourcingCaseRetry")]
public sealed class AllowTerminalSourcingCaseRetry : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_ProcurementSourcingCases_TenantId_SourcingReleaseId",
            table: "ProcurementSourcingCases");

        migrationBuilder.CreateIndex(
            name: "IX_ProcurementSourcingCases_TenantId_SourcingReleaseId",
            table: "ProcurementSourcingCases",
            columns: new[] { "TenantId", "SourcingReleaseId" },
            unique: true,
            filter: "[IsDeleted] = 0 AND [Status] IN (0, 1)");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_ProcurementSourcingCases_TenantId_SourcingReleaseId",
            table: "ProcurementSourcingCases");

        migrationBuilder.CreateIndex(
            name: "IX_ProcurementSourcingCases_TenantId_SourcingReleaseId",
            table: "ProcurementSourcingCases",
            columns: new[] { "TenantId", "SourcingReleaseId" },
            unique: true);
    }
}
