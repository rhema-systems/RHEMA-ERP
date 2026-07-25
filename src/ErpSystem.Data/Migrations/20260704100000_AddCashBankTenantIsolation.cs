using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    [Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(ApplicationDbContext))]
    [Migration("20260704100000_AddCashBankTenantIsolation")]
    public partial class AddCashBankTenantIsolation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var defaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "ReconciliationMatch",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: defaultTenantId);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "CashTransaction",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: defaultTenantId);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "BankStatementLine",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: defaultTenantId);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "BankStatement",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: defaultTenantId);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "BankReconciliation",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: defaultTenantId);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "BankAccounts",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: defaultTenantId);

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationMatch_TenantId",
                table: "ReconciliationMatch",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationMatch_TenantId_ReconciliationId",
                table: "ReconciliationMatch",
                columns: new[] { "TenantId", "ReconciliationId" });

            migrationBuilder.CreateIndex(
                name: "IX_CashTransaction_TenantId",
                table: "CashTransaction",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CashTransaction_TenantId_BankAccountId_TransactionDate",
                table: "CashTransaction",
                columns: new[] { "TenantId", "BankAccountId", "TransactionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_CashTransaction_TenantId_TransactionNumber",
                table: "CashTransaction",
                columns: new[] { "TenantId", "TransactionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementLine_TenantId",
                table: "BankStatementLine",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementLine_TenantId_BankStatementId",
                table: "BankStatementLine",
                columns: new[] { "TenantId", "BankStatementId" });

            migrationBuilder.CreateIndex(
                name: "IX_BankStatement_TenantId",
                table: "BankStatement",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_BankStatement_TenantId_BankAccountId_StatementDate",
                table: "BankStatement",
                columns: new[] { "TenantId", "BankAccountId", "StatementDate" });

            migrationBuilder.CreateIndex(
                name: "IX_BankReconciliation_TenantId",
                table: "BankReconciliation",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_BankReconciliation_TenantId_BankAccountId_ReconciliationDate",
                table: "BankReconciliation",
                columns: new[] { "TenantId", "BankAccountId", "ReconciliationDate" });

            migrationBuilder.CreateIndex(
                name: "IX_BankAccounts_TenantId",
                table: "BankAccounts",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_BankAccounts_TenantId_AccountNumber",
                table: "BankAccounts",
                columns: new[] { "TenantId", "AccountNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReconciliationMatch_TenantId",
                table: "ReconciliationMatch");

            migrationBuilder.DropIndex(
                name: "IX_ReconciliationMatch_TenantId_ReconciliationId",
                table: "ReconciliationMatch");

            migrationBuilder.DropIndex(
                name: "IX_CashTransaction_TenantId",
                table: "CashTransaction");

            migrationBuilder.DropIndex(
                name: "IX_CashTransaction_TenantId_BankAccountId_TransactionDate",
                table: "CashTransaction");

            migrationBuilder.DropIndex(
                name: "IX_CashTransaction_TenantId_TransactionNumber",
                table: "CashTransaction");

            migrationBuilder.DropIndex(
                name: "IX_BankStatementLine_TenantId",
                table: "BankStatementLine");

            migrationBuilder.DropIndex(
                name: "IX_BankStatementLine_TenantId_BankStatementId",
                table: "BankStatementLine");

            migrationBuilder.DropIndex(
                name: "IX_BankStatement_TenantId",
                table: "BankStatement");

            migrationBuilder.DropIndex(
                name: "IX_BankStatement_TenantId_BankAccountId_StatementDate",
                table: "BankStatement");

            migrationBuilder.DropIndex(
                name: "IX_BankReconciliation_TenantId",
                table: "BankReconciliation");

            migrationBuilder.DropIndex(
                name: "IX_BankReconciliation_TenantId_BankAccountId_ReconciliationDate",
                table: "BankReconciliation");

            migrationBuilder.DropIndex(
                name: "IX_BankAccounts_TenantId",
                table: "BankAccounts");

            migrationBuilder.DropIndex(
                name: "IX_BankAccounts_TenantId_AccountNumber",
                table: "BankAccounts");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ReconciliationMatch");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "CashTransaction");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "BankStatementLine");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "BankStatement");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "BankReconciliation");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "BankAccounts");
        }
    }
}
