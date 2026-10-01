using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class FinanceCurrencyMinorUnitPrecision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "TotalDebitAmount",
                table: "FinancePostingEvents",
                type: "decimal(20,4)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalCreditAmount",
                table: "FinancePostingEvents",
                type: "decimal(20,4)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalDebitAmount",
                table: "JournalEntries",
                type: "decimal(20,4)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalCreditAmount",
                table: "JournalEntries",
                type: "decimal(20,4)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "BalanceDifference",
                table: "JournalEntries",
                type: "decimal(20,4)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,4)");

            migrationBuilder.AlterColumn<decimal>(
                name: "TransactionDebitAmount",
                table: "AccountTransactions",
                type: "decimal(20,4)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "TransactionCreditAmount",
                table: "AccountTransactions",
                type: "decimal(20,4)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "ForeignCurrencyAmount",
                table: "AccountTransactions",
                type: "decimal(20,4)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "DebitAmount",
                table: "AccountTransactions",
                type: "decimal(20,4)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "CreditAmount",
                table: "AccountTransactions",
                type: "decimal(20,4)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "TotalDebitAmount",
                table: "FinancePostingEvents",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(20,4)");

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalCreditAmount",
                table: "FinancePostingEvents",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(20,4)");

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalDebitAmount",
                table: "JournalEntries",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(20,4)");

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalCreditAmount",
                table: "JournalEntries",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(20,4)");

            migrationBuilder.AlterColumn<decimal>(
                name: "BalanceDifference",
                table: "JournalEntries",
                type: "decimal(18,4)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(20,4)");

            migrationBuilder.AlterColumn<decimal>(
                name: "TransactionDebitAmount",
                table: "AccountTransactions",
                type: "decimal(18,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(20,4)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "TransactionCreditAmount",
                table: "AccountTransactions",
                type: "decimal(18,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(20,4)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "ForeignCurrencyAmount",
                table: "AccountTransactions",
                type: "decimal(18,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(20,4)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "DebitAmount",
                table: "AccountTransactions",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(20,4)");

            migrationBuilder.AlterColumn<decimal>(
                name: "CreditAmount",
                table: "AccountTransactions",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(20,4)");
        }
    }
}
