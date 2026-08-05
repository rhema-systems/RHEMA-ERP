using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    [Migration("20260611110000_AddFinancePurchaseOrderPaymentTerm")]
    public partial class AddFinancePurchaseOrderPaymentTerm : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PaymentTermId",
                table: "FinancePurchaseOrders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinancePurchaseOrders_PaymentTermId",
                table: "FinancePurchaseOrders",
                column: "PaymentTermId");

            migrationBuilder.AddForeignKey(
                name: "FK_FinancePurchaseOrders_PaymentTerms_PaymentTermId",
                table: "FinancePurchaseOrders",
                column: "PaymentTermId",
                principalTable: "PaymentTerms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FinancePurchaseOrders_PaymentTerms_PaymentTermId",
                table: "FinancePurchaseOrders");

            migrationBuilder.DropIndex(
                name: "IX_FinancePurchaseOrders_PaymentTermId",
                table: "FinancePurchaseOrders");

            migrationBuilder.DropColumn(
                name: "PaymentTermId",
                table: "FinancePurchaseOrders");
        }
    }
}
