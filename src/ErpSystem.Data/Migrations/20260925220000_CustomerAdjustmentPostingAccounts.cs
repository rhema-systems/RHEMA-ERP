using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260925220000_CustomerAdjustmentPostingAccounts")]
public sealed class CustomerAdjustmentPostingAccounts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        foreach (var column in new[] { "CustomerFinanceChargesAccountId", "CustomerWriteoffAccountId", "CustomerOverpaymentWriteoffAccountId" })
        {
            migrationBuilder.AddColumn<Guid>(column, "BusinessPartners", "uniqueidentifier", nullable: true);
            migrationBuilder.CreateIndex($"IX_BusinessPartners_{column}", "BusinessPartners", column);
            migrationBuilder.AddForeignKey($"FK_BusinessPartners_Accounts_{column}", "BusinessPartners", column, "Accounts", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        }
        migrationBuilder.AddColumn<Guid>("ControlAccountId", "SubledgerAdjustmentJournals", "uniqueidentifier", nullable: true);
        migrationBuilder.CreateIndex("IX_SubledgerAdjustmentJournals_ControlAccountId", "SubledgerAdjustmentJournals", "ControlAccountId");
        migrationBuilder.AddForeignKey("FK_SubledgerAdjustmentJournals_Accounts_ControlAccountId", "SubledgerAdjustmentJournals", "ControlAccountId", "Accounts", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF EXISTS (SELECT 1 FROM dbo.BusinessPartners WHERE CustomerFinanceChargesAccountId IS NOT NULL OR CustomerWriteoffAccountId IS NOT NULL OR CustomerOverpaymentWriteoffAccountId IS NOT NULL)
                OR EXISTS (SELECT 1 FROM dbo.SubledgerAdjustmentJournals WHERE ControlAccountId IS NOT NULL)
                THROW 51730, 'Customer adjustment mappings or posting evidence are in use; preserve them before rollback.', 1;
            """);
        migrationBuilder.DropForeignKey("FK_SubledgerAdjustmentJournals_Accounts_ControlAccountId", "SubledgerAdjustmentJournals");
        migrationBuilder.DropIndex("IX_SubledgerAdjustmentJournals_ControlAccountId", "SubledgerAdjustmentJournals");
        migrationBuilder.DropColumn("ControlAccountId", "SubledgerAdjustmentJournals");
        foreach (var column in new[] { "CustomerFinanceChargesAccountId", "CustomerWriteoffAccountId", "CustomerOverpaymentWriteoffAccountId" })
        {
            migrationBuilder.DropForeignKey($"FK_BusinessPartners_Accounts_{column}", "BusinessPartners");
            migrationBuilder.DropIndex($"IX_BusinessPartners_{column}", "BusinessPartners");
            migrationBuilder.DropColumn(column, "BusinessPartners");
        }
    }
}
