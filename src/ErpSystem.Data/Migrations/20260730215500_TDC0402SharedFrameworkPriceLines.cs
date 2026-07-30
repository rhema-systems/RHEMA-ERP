using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260730215500_TDC0402SharedFrameworkPriceLines")]
public sealed class TDC0402SharedFrameworkPriceLines : Migration
{
    private const string IndexName =
        "IX_ProcurementFrameworkCallOffLines_TenantId_CallOffId_AgreementPriceLineId";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: IndexName,
            table: "ProcurementFrameworkCallOffLines");

        migrationBuilder.CreateIndex(
            name: IndexName,
            table: "ProcurementFrameworkCallOffLines",
            columns: new[] { "TenantId", "CallOffId", "AgreementPriceLineId" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: IndexName,
            table: "ProcurementFrameworkCallOffLines");

        migrationBuilder.CreateIndex(
            name: IndexName,
            table: "ProcurementFrameworkCallOffLines",
            columns: new[] { "TenantId", "CallOffId", "AgreementPriceLineId" },
            unique: true);
    }
}
