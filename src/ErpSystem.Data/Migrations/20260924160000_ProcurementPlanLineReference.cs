using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260924160000_ProcurementPlanLineReference")]
public sealed class ProcurementPlanLineReference : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Deterministic persisted expression also backfills existing lines without changing
        // any approved plan, budget, posted transaction or historical source relationship.
        migrationBuilder.AddColumn<string>(name: "ReferenceNumber", table: "ProcurementPlanItems",
            type: "nvarchar(36)", maxLength: 36, nullable: false,
            computedColumnSql: "N'PPL-' + LOWER(REPLACE(CONVERT(nvarchar(36), [Id]), N'-', N''))", stored: true);
        // SQL Server forbids computed columns in an index filter. Use explicit unfiltered SQL
        // so EF's legacy index fallback for a hand-authored migration cannot add IS NOT NULL.
        migrationBuilder.Sql("CREATE UNIQUE INDEX [IX_ProcurementPlanItems_TenantId_ReferenceNumber] ON [ProcurementPlanItems] ([TenantId], [ReferenceNumber]);");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_ProcurementPlanItems_TenantId_ReferenceNumber", table: "ProcurementPlanItems");
        migrationBuilder.DropColumn(name: "ReferenceNumber", table: "ProcurementPlanItems");
    }
}
