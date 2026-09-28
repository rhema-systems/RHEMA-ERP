using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Preserves the user's explanation when a manually captured AP invoice uses a currency
/// different from the governed supplier-master default.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260928220500_AddVendorInvoiceCurrencyOverrideReason")]
public sealed class AddVendorInvoiceCurrencyOverrideReason : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "CurrencyOverrideReason",
            table: "VendorInvoice",
            type: "nvarchar(500)",
            maxLength: 500,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "CurrencyOverrideReason",
            table: "VendorInvoice");
    }
}
