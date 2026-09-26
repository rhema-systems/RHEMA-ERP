using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class CanonicalCustomerPaymentBusinessPartnerIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF EXISTS (SELECT 1 FROM [CustomerPayment])
BEGIN
    THROW 51000, 'Canonical CustomerPayment cutover requires a fresh Finance transactional database. Reset Finance transactions before applying this migration; legacy CustomerId rows cannot supply governed Customer role/profile evidence safely.', 1;
END
""");

            migrationBuilder.DropForeignKey(
                name: "FK_CustomerPayment_BusinessPartners_CustomerId",
                table: "CustomerPayment");

            migrationBuilder.DropIndex(
                name: "IX_CustomerPayment_CustomerId",
                table: "CustomerPayment");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "CustomerPayment");

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessPartnerArProfileVersionId",
                table: "CustomerPayment",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "BusinessPartnerCode",
                table: "CustomerPayment",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessPartnerId",
                table: "CustomerPayment",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessPartnerRoleId",
                table: "CustomerPayment",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "BusinessPartnerLegalName",
                table: "CustomerPayment",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BusinessPartnerName",
                table: "CustomerPayment",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BusinessPartnerTaxIdentificationNumber",
                table: "CustomerPayment",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPayment_BusinessPartnerArProfileVersionId",
                table: "CustomerPayment",
                column: "BusinessPartnerArProfileVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPayment_BusinessPartnerId",
                table: "CustomerPayment",
                column: "BusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPayment_BusinessPartnerRoleId",
                table: "CustomerPayment",
                column: "BusinessPartnerRoleId");

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerPayment_BusinessPartnerArProfileVersions_BusinessPartnerArProfileVersionId",
                table: "CustomerPayment",
                column: "BusinessPartnerArProfileVersionId",
                principalTable: "BusinessPartnerArProfileVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerPayment_BusinessPartnerRoles_BusinessPartnerRoleId",
                table: "CustomerPayment",
                column: "BusinessPartnerRoleId",
                principalTable: "BusinessPartnerRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerPayment_BusinessPartners_BusinessPartnerId",
                table: "CustomerPayment",
                column: "BusinessPartnerId",
                principalTable: "BusinessPartners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CustomerPayment_BusinessPartnerArProfileVersions_BusinessPartnerArProfileVersionId",
                table: "CustomerPayment");

            migrationBuilder.DropForeignKey(
                name: "FK_CustomerPayment_BusinessPartnerRoles_BusinessPartnerRoleId",
                table: "CustomerPayment");

            migrationBuilder.DropForeignKey(
                name: "FK_CustomerPayment_BusinessPartners_BusinessPartnerId",
                table: "CustomerPayment");

            migrationBuilder.DropIndex(
                name: "IX_CustomerPayment_BusinessPartnerArProfileVersionId",
                table: "CustomerPayment");

            migrationBuilder.DropIndex(
                name: "IX_CustomerPayment_BusinessPartnerId",
                table: "CustomerPayment");

            migrationBuilder.DropIndex(
                name: "IX_CustomerPayment_BusinessPartnerRoleId",
                table: "CustomerPayment");

            migrationBuilder.DropColumn(
                name: "BusinessPartnerArProfileVersionId",
                table: "CustomerPayment");

            migrationBuilder.DropColumn(
                name: "BusinessPartnerCode",
                table: "CustomerPayment");

            migrationBuilder.DropColumn(
                name: "BusinessPartnerId",
                table: "CustomerPayment");

            migrationBuilder.DropColumn(
                name: "BusinessPartnerRoleId",
                table: "CustomerPayment");

            migrationBuilder.DropColumn(
                name: "BusinessPartnerLegalName",
                table: "CustomerPayment");

            migrationBuilder.DropColumn(
                name: "BusinessPartnerName",
                table: "CustomerPayment");

            migrationBuilder.DropColumn(
                name: "BusinessPartnerTaxIdentificationNumber",
                table: "CustomerPayment");

            migrationBuilder.AddColumn<Guid>(
                table: "CustomerPayment",
                name: "CustomerId",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                table: "CustomerPayment",
                name: "IX_CustomerPayment_CustomerId",
                column: "CustomerId");

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerPayment_BusinessPartners_CustomerId",
                table: "CustomerPayment",
                column: "CustomerId",
                principalTable: "BusinessPartners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
