using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class CanonicalVendorInvoiceBusinessPartnerIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // This is an intentional development cutover, not a guessed Supplier-to-partner
            // migration. Supplier ids are not canonical Business Partner ids. The approved
            // Finance reset must remove AP transactions before this FK is redirected.
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM dbo.VendorInvoice)
                    THROW 51940, 'CANONICAL_BP_CUTOVER_REQUIRED: VendorInvoice must be empty after the verified Finance reset before canonical Business Partner identity is installed.', 1;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_VendorInvoice_Suppliers_SupplierId",
                table: "VendorInvoice");

            migrationBuilder.RenameColumn(
                name: "SupplierId",
                table: "VendorInvoice",
                newName: "BusinessPartnerId");

            migrationBuilder.RenameIndex(
                name: "IX_VendorInvoice_SupplierId",
                table: "VendorInvoice",
                newName: "IX_VendorInvoice_BusinessPartnerId");

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessPartnerApProfileVersionId",
                table: "VendorInvoice",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BusinessPartnerCode",
                table: "VendorInvoice",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BusinessPartnerLegalName",
                table: "VendorInvoice",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessPartnerRoleId",
                table: "VendorInvoice",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BusinessPartnerTaxIdentificationNumber",
                table: "VendorInvoice",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_VendorInvoice_BusinessPartnerApProfileVersionId",
                table: "VendorInvoice",
                column: "BusinessPartnerApProfileVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorInvoice_BusinessPartnerRoleId",
                table: "VendorInvoice",
                column: "BusinessPartnerRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorInvoice_TenantId_BusinessPartnerId_InvoiceDate",
                table: "VendorInvoice",
                columns: new[] { "TenantId", "BusinessPartnerId", "InvoiceDate" });

            migrationBuilder.AddForeignKey(
                name: "FK_VendorInvoice_BusinessPartnerApProfileVersions_BusinessPartnerApProfileVersionId",
                table: "VendorInvoice",
                column: "BusinessPartnerApProfileVersionId",
                principalTable: "BusinessPartnerApProfileVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_VendorInvoice_BusinessPartnerRoles_BusinessPartnerRoleId",
                table: "VendorInvoice",
                column: "BusinessPartnerRoleId",
                principalTable: "BusinessPartnerRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_VendorInvoice_BusinessPartners_BusinessPartnerId",
                table: "VendorInvoice",
                column: "BusinessPartnerId",
                principalTable: "BusinessPartners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_VendorInvoice_BusinessPartnerApProfileVersions_BusinessPartnerApProfileVersionId",
                table: "VendorInvoice");

            migrationBuilder.DropForeignKey(
                name: "FK_VendorInvoice_BusinessPartnerRoles_BusinessPartnerRoleId",
                table: "VendorInvoice");

            migrationBuilder.DropForeignKey(
                name: "FK_VendorInvoice_BusinessPartners_BusinessPartnerId",
                table: "VendorInvoice");

            migrationBuilder.DropIndex(
                name: "IX_VendorInvoice_BusinessPartnerApProfileVersionId",
                table: "VendorInvoice");

            migrationBuilder.DropIndex(
                name: "IX_VendorInvoice_BusinessPartnerRoleId",
                table: "VendorInvoice");

            migrationBuilder.DropIndex(
                name: "IX_VendorInvoice_TenantId_BusinessPartnerId_InvoiceDate",
                table: "VendorInvoice");

            migrationBuilder.DropColumn(
                name: "BusinessPartnerApProfileVersionId",
                table: "VendorInvoice");

            migrationBuilder.DropColumn(
                name: "BusinessPartnerCode",
                table: "VendorInvoice");

            migrationBuilder.DropColumn(
                name: "BusinessPartnerLegalName",
                table: "VendorInvoice");

            migrationBuilder.DropColumn(
                name: "BusinessPartnerRoleId",
                table: "VendorInvoice");

            migrationBuilder.DropColumn(
                name: "BusinessPartnerTaxIdentificationNumber",
                table: "VendorInvoice");

            migrationBuilder.RenameColumn(
                name: "BusinessPartnerId",
                table: "VendorInvoice",
                newName: "SupplierId");

            migrationBuilder.RenameIndex(
                name: "IX_VendorInvoice_BusinessPartnerId",
                table: "VendorInvoice",
                newName: "IX_VendorInvoice_SupplierId");

            migrationBuilder.AddForeignKey(
                name: "FK_VendorInvoice_Suppliers_SupplierId",
                table: "VendorInvoice",
                column: "SupplierId",
                principalTable: "Suppliers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
