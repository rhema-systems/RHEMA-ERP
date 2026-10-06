using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReorderBankDepositAcknowledgementBeforePosting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "AutoPostBankDepositAfterApproval",
                table: "FinanceSettings",
                newName: "AutoPostBankDepositAfterConfirmation");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "AutoPostBankDepositAfterConfirmation",
                table: "FinanceSettings",
                newName: "AutoPostBankDepositAfterApproval");
        }
    }
}
