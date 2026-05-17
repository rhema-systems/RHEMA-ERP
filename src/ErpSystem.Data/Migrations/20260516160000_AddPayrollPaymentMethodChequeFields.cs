using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ErpSystem.Data;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260516160000_AddPayrollPaymentMethodChequeFields")]
    public partial class AddPayrollPaymentMethodChequeFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChequeBankCode",
                table: "PayrollPaymentMethods",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChequeNumber",
                table: "PayrollPaymentMethods",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChequeBankCode",
                table: "PayrollPaymentMethods");

            migrationBuilder.DropColumn(
                name: "ChequeNumber",
                table: "PayrollPaymentMethods");
        }
    }
}
