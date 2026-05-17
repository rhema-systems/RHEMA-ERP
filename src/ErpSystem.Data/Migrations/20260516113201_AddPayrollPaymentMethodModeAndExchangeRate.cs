using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollPaymentMethodModeAndExchangeRate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ExchangeRate",
                table: "PayrollPaymentMethods",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentMode",
                table: "PayrollPaymentMethods",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Percentage");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExchangeRate",
                table: "PayrollPaymentMethods");

            migrationBuilder.DropColumn(
                name: "PaymentMode",
                table: "PayrollPaymentMethods");
        }
    }
}
