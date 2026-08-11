using ErpSystem.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Adds the immutable FIN-LIM-0043 translation evidence used when fixed-asset sale proceeds are
/// denominated outside the tenant's functional currency. This is intentionally a focused manual
/// migration because the shared snapshot contains unrelated module drift.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260811160000_AddFixedAssetForeignCurrencyDisposalProceeds")]
public partial class AddFixedAssetForeignCurrencyDisposalProceeds : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>("ProceedsFunctionalAmount", "AssetDisposals", "decimal(18,2)", nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<Guid>("ProceedsExchangeRateId", "AssetDisposals", "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<decimal>("ProceedsExchangeRateValue", "AssetDisposals", "decimal(18,6)", nullable: false, defaultValue: 1m);
        migrationBuilder.AddColumn<string>("ProceedsExchangeRateSource", "AssetDisposals", "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: "Functional currency");
        migrationBuilder.AddColumn<DateTime>("ProceedsExchangeRateDate", "AssetDisposals", "datetime2", nullable: false, defaultValueSql: "CONVERT(date, SYSUTCDATETIME())");
        migrationBuilder.AddColumn<int>("ProceedsExchangeRateType", "AssetDisposals", "int", nullable: false, defaultValue: 1);
        migrationBuilder.AddColumn<int>("ProceedsExchangeRateQuoteSide", "AssetDisposals", "int", nullable: false, defaultValue: 1);

        migrationBuilder.CreateIndex(
            name: "IX_AssetDisposals_TenantId_ProceedsExchangeRateId",
            table: "AssetDisposals",
            columns: new[] { "TenantId", "ProceedsExchangeRateId" });
        migrationBuilder.AddForeignKey(
            name: "FK_AssetDisposals_ExchangeRates_ProceedsExchangeRateId",
            table: "AssetDisposals",
            column: "ProceedsExchangeRateId",
            principalTable: "ExchangeRates",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddCheckConstraint(
            name: "CK_AssetDisposals_ProceedsExchangeRateValue",
            table: "AssetDisposals",
            sql: "[ProceedsExchangeRateValue] > 0");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint("CK_AssetDisposals_ProceedsExchangeRateValue", "AssetDisposals");
        migrationBuilder.DropForeignKey("FK_AssetDisposals_ExchangeRates_ProceedsExchangeRateId", "AssetDisposals");
        migrationBuilder.DropIndex("IX_AssetDisposals_TenantId_ProceedsExchangeRateId", "AssetDisposals");
        migrationBuilder.DropColumn("ProceedsFunctionalAmount", "AssetDisposals");
        migrationBuilder.DropColumn("ProceedsExchangeRateId", "AssetDisposals");
        migrationBuilder.DropColumn("ProceedsExchangeRateValue", "AssetDisposals");
        migrationBuilder.DropColumn("ProceedsExchangeRateSource", "AssetDisposals");
        migrationBuilder.DropColumn("ProceedsExchangeRateDate", "AssetDisposals");
        migrationBuilder.DropColumn("ProceedsExchangeRateType", "AssetDisposals");
        migrationBuilder.DropColumn("ProceedsExchangeRateQuoteSide", "AssetDisposals");
    }
}
