using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class CustomerPostingAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CustomerCostOfSalesAccountId",
                table: "BusinessPartners",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerInventoryAccountId",
                table: "BusinessPartners",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerSalesAccountId",
                table: "BusinessPartners",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerSalesReturnsAccountId",
                table: "BusinessPartners",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerTermsDiscountsTakenAccountId",
                table: "BusinessPartners",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartners_CustomerCostOfSalesAccountId",
                table: "BusinessPartners",
                column: "CustomerCostOfSalesAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartners_CustomerInventoryAccountId",
                table: "BusinessPartners",
                column: "CustomerInventoryAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartners_CustomerSalesAccountId",
                table: "BusinessPartners",
                column: "CustomerSalesAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartners_CustomerSalesReturnsAccountId",
                table: "BusinessPartners",
                column: "CustomerSalesReturnsAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartners_CustomerTermsDiscountsTakenAccountId",
                table: "BusinessPartners",
                column: "CustomerTermsDiscountsTakenAccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_BusinessPartners_Accounts_CustomerCostOfSalesAccountId",
                table: "BusinessPartners",
                column: "CustomerCostOfSalesAccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BusinessPartners_Accounts_CustomerInventoryAccountId",
                table: "BusinessPartners",
                column: "CustomerInventoryAccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BusinessPartners_Accounts_CustomerSalesAccountId",
                table: "BusinessPartners",
                column: "CustomerSalesAccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BusinessPartners_Accounts_CustomerSalesReturnsAccountId",
                table: "BusinessPartners",
                column: "CustomerSalesReturnsAccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BusinessPartners_Accounts_CustomerTermsDiscountsTakenAccountId",
                table: "BusinessPartners",
                column: "CustomerTermsDiscountsTakenAccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM dbo.BusinessPartners WHERE
                    CustomerCostOfSalesAccountId IS NOT NULL OR CustomerInventoryAccountId IS NOT NULL OR
                    CustomerSalesAccountId IS NOT NULL OR CustomerSalesReturnsAccountId IS NOT NULL OR
                    CustomerTermsDiscountsTakenAccountId IS NOT NULL)
                    THROW 51728, 'Customer posting mappings are in use; preserve them before rolling back this migration.', 1;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_BusinessPartners_Accounts_CustomerCostOfSalesAccountId",
                table: "BusinessPartners");

            migrationBuilder.DropForeignKey(
                name: "FK_BusinessPartners_Accounts_CustomerInventoryAccountId",
                table: "BusinessPartners");

            migrationBuilder.DropForeignKey(
                name: "FK_BusinessPartners_Accounts_CustomerSalesAccountId",
                table: "BusinessPartners");

            migrationBuilder.DropForeignKey(
                name: "FK_BusinessPartners_Accounts_CustomerSalesReturnsAccountId",
                table: "BusinessPartners");

            migrationBuilder.DropForeignKey(
                name: "FK_BusinessPartners_Accounts_CustomerTermsDiscountsTakenAccountId",
                table: "BusinessPartners");

            migrationBuilder.DropIndex(
                name: "IX_BusinessPartners_CustomerCostOfSalesAccountId",
                table: "BusinessPartners");

            migrationBuilder.DropIndex(
                name: "IX_BusinessPartners_CustomerInventoryAccountId",
                table: "BusinessPartners");

            migrationBuilder.DropIndex(
                name: "IX_BusinessPartners_CustomerSalesAccountId",
                table: "BusinessPartners");

            migrationBuilder.DropIndex(
                name: "IX_BusinessPartners_CustomerSalesReturnsAccountId",
                table: "BusinessPartners");

            migrationBuilder.DropIndex(
                name: "IX_BusinessPartners_CustomerTermsDiscountsTakenAccountId",
                table: "BusinessPartners");

            migrationBuilder.DropColumn(
                name: "CustomerCostOfSalesAccountId",
                table: "BusinessPartners");

            migrationBuilder.DropColumn(
                name: "CustomerInventoryAccountId",
                table: "BusinessPartners");

            migrationBuilder.DropColumn(
                name: "CustomerSalesAccountId",
                table: "BusinessPartners");

            migrationBuilder.DropColumn(
                name: "CustomerSalesReturnsAccountId",
                table: "BusinessPartners");

            migrationBuilder.DropColumn(
                name: "CustomerTermsDiscountsTakenAccountId",
                table: "BusinessPartners");
        }
    }
}
