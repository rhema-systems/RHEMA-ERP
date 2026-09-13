using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Converts PaymentMethod and Cheque from global lookups to tenant-scoped entities.
    ///
    /// Data note: neither table had a tenant-aware write path before this migration, so any
    /// existing rows are pre-tenancy globals that cannot be attributed to a tenant. References
    /// from CashTransaction are nulled and the rows are deleted; each tenant re-seeds its
    /// default payment methods on first read (PaymentMethodController). A production cutover
    /// with meaningful rows would need a tenant remap script instead of this delete — tracked
    /// in the finance limitations register.
    /// </summary>
    [Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(ApplicationDbContext))]
    [Migration("20260714030000_TenantScopePaymentMethodsAndCheques")]
    public partial class TenantScopePaymentMethodsAndCheques : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE [CashTransaction] SET [PaymentMethodId] = NULL WHERE [PaymentMethodId] IS NOT NULL");
            migrationBuilder.Sql("UPDATE [CashTransaction] SET [ChequeId] = NULL WHERE [ChequeId] IS NOT NULL");
            migrationBuilder.Sql("DELETE FROM [Cheque]");
            migrationBuilder.Sql("DELETE FROM [PaymentMethod]");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "PaymentMethod",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Cheque",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_PaymentMethod_TenantId",
                table: "PaymentMethod",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentMethod_TenantId_Code",
                table: "PaymentMethod",
                columns: new[] { "TenantId", "Code" },
                unique: true,
                filter: "[Code] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Cheque_TenantId",
                table: "Cheque",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Cheque_TenantId_BankAccountId_Status",
                table: "Cheque",
                columns: new[] { "TenantId", "BankAccountId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Cheque_TenantId_BankAccountId_ChequeNumber",
                table: "Cheque",
                columns: new[] { "TenantId", "BankAccountId", "ChequeNumber" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentMethod_Tenants_TenantId",
                table: "PaymentMethod",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Cheque_Tenants_TenantId",
                table: "Cheque",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cheque_Tenants_TenantId",
                table: "Cheque");

            migrationBuilder.DropForeignKey(
                name: "FK_PaymentMethod_Tenants_TenantId",
                table: "PaymentMethod");

            migrationBuilder.DropIndex(
                name: "IX_Cheque_TenantId_BankAccountId_ChequeNumber",
                table: "Cheque");

            migrationBuilder.DropIndex(
                name: "IX_Cheque_TenantId_BankAccountId_Status",
                table: "Cheque");

            migrationBuilder.DropIndex(
                name: "IX_Cheque_TenantId",
                table: "Cheque");

            migrationBuilder.DropIndex(
                name: "IX_PaymentMethod_TenantId_Code",
                table: "PaymentMethod");

            migrationBuilder.DropIndex(
                name: "IX_PaymentMethod_TenantId",
                table: "PaymentMethod");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Cheque");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "PaymentMethod");
        }
    }
}
