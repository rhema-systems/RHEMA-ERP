using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260720043000_AllowRecreatingDeletedPaymentTermCodes")]
public partial class AllowRecreatingDeletedPaymentTermCodes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_PaymentTerms_TenantId_Code",
            table: "PaymentTerms");

        migrationBuilder.CreateIndex(
            name: "IX_PaymentTerms_TenantId_Code",
            table: "PaymentTerms",
            columns: new[] { "TenantId", "Code" },
            unique: true,
            filter: "[IsDeleted] = 0");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_PaymentTerms_TenantId_Code",
            table: "PaymentTerms");

        migrationBuilder.CreateIndex(
            name: "IX_PaymentTerms_TenantId_Code",
            table: "PaymentTerms",
            columns: new[] { "TenantId", "Code" },
            unique: true);
    }
}
