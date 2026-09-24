using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class CanonicalApSettlementBusinessPartnerIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // This cutover intentionally does not reinterpret legacy Supplier GUIDs as canonical
            // Business Partner or role GUIDs. The approved rollout resets Finance transactions;
            // fail closed if that prerequisite has not been completed.
            migrationBuilder.Sql(
                """
                IF EXISTS (SELECT 1 FROM [VendorPayment])
                    THROW 51941, 'Canonical AP settlement cutover requires VendorPayment to be empty. Run the verified Finance reset before applying this migration.', 1;
                IF EXISTS (SELECT 1 FROM [SupplierDebitNotes])
                    THROW 51942, 'Canonical AP settlement cutover requires SupplierDebitNotes to be empty. Run the verified Finance reset before applying this migration.', 1;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_SupplierDebitNotes_Suppliers_SupplierId",
                table: "SupplierDebitNotes");

            migrationBuilder.DropForeignKey(
                name: "FK_VendorPayment_Suppliers_SupplierId",
                table: "VendorPayment");

            migrationBuilder.RenameColumn(
                name: "SupplierId",
                table: "VendorPayment",
                newName: "BusinessPartnerId");

            migrationBuilder.RenameIndex(
                name: "IX_VendorPayment_SupplierId",
                table: "VendorPayment",
                newName: "IX_VendorPayment_BusinessPartnerId");

            migrationBuilder.RenameColumn(
                name: "SupplierId",
                table: "SupplierDebitNotes",
                newName: "BusinessPartnerRoleId");

            migrationBuilder.RenameIndex(
                name: "IX_SupplierDebitNotes_SupplierId",
                table: "SupplierDebitNotes",
                newName: "IX_SupplierDebitNotes_BusinessPartnerRoleId");

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessPartnerApProfileVersionId",
                table: "VendorPayment",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BusinessPartnerCode",
                table: "VendorPayment",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BusinessPartnerLegalName",
                table: "VendorPayment",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BusinessPartnerName",
                table: "VendorPayment",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessPartnerRoleId",
                table: "VendorPayment",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BusinessPartnerTaxIdentificationNumber",
                table: "VendorPayment",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessPartnerApProfileVersionId",
                table: "SupplierDebitNotes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BusinessPartnerCode",
                table: "SupplierDebitNotes",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BusinessPartnerLegalName",
                table: "SupplierDebitNotes",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BusinessPartnerName",
                table: "SupplierDebitNotes",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BusinessPartnerTaxIdentificationNumber",
                table: "SupplierDebitNotes",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_VendorPayment_BusinessPartnerApProfileVersionId",
                table: "VendorPayment",
                column: "BusinessPartnerApProfileVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorPayment_BusinessPartnerRoleId",
                table: "VendorPayment",
                column: "BusinessPartnerRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorPayment_TenantId_BusinessPartnerId_PaymentDate",
                table: "VendorPayment",
                columns: new[] { "TenantId", "BusinessPartnerId", "PaymentDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierDebitNotes_BusinessPartnerApProfileVersionId",
                table: "SupplierDebitNotes",
                column: "BusinessPartnerApProfileVersionId");

            migrationBuilder.AddForeignKey(
                name: "FK_SupplierDebitNotes_BusinessPartnerApProfileVersions_BusinessPartnerApProfileVersionId",
                table: "SupplierDebitNotes",
                column: "BusinessPartnerApProfileVersionId",
                principalTable: "BusinessPartnerApProfileVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SupplierDebitNotes_BusinessPartnerRoles_BusinessPartnerRoleId",
                table: "SupplierDebitNotes",
                column: "BusinessPartnerRoleId",
                principalTable: "BusinessPartnerRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_VendorPayment_BusinessPartnerApProfileVersions_BusinessPartnerApProfileVersionId",
                table: "VendorPayment",
                column: "BusinessPartnerApProfileVersionId",
                principalTable: "BusinessPartnerApProfileVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_VendorPayment_BusinessPartnerRoles_BusinessPartnerRoleId",
                table: "VendorPayment",
                column: "BusinessPartnerRoleId",
                principalTable: "BusinessPartnerRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_VendorPayment_BusinessPartners_BusinessPartnerId",
                table: "VendorPayment",
                column: "BusinessPartnerId",
                principalTable: "BusinessPartners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SupplierDebitNotes_BusinessPartnerApProfileVersions_BusinessPartnerApProfileVersionId",
                table: "SupplierDebitNotes");

            migrationBuilder.DropForeignKey(
                name: "FK_SupplierDebitNotes_BusinessPartnerRoles_BusinessPartnerRoleId",
                table: "SupplierDebitNotes");

            migrationBuilder.DropForeignKey(
                name: "FK_VendorPayment_BusinessPartnerApProfileVersions_BusinessPartnerApProfileVersionId",
                table: "VendorPayment");

            migrationBuilder.DropForeignKey(
                name: "FK_VendorPayment_BusinessPartnerRoles_BusinessPartnerRoleId",
                table: "VendorPayment");

            migrationBuilder.DropForeignKey(
                name: "FK_VendorPayment_BusinessPartners_BusinessPartnerId",
                table: "VendorPayment");

            migrationBuilder.DropIndex(
                name: "IX_VendorPayment_BusinessPartnerApProfileVersionId",
                table: "VendorPayment");

            migrationBuilder.DropIndex(
                name: "IX_VendorPayment_BusinessPartnerRoleId",
                table: "VendorPayment");

            migrationBuilder.DropIndex(
                name: "IX_VendorPayment_TenantId_BusinessPartnerId_PaymentDate",
                table: "VendorPayment");

            migrationBuilder.DropIndex(
                name: "IX_SupplierDebitNotes_BusinessPartnerApProfileVersionId",
                table: "SupplierDebitNotes");

            migrationBuilder.DropColumn(
                name: "BusinessPartnerApProfileVersionId",
                table: "VendorPayment");

            migrationBuilder.DropColumn(
                name: "BusinessPartnerCode",
                table: "VendorPayment");

            migrationBuilder.DropColumn(
                name: "BusinessPartnerLegalName",
                table: "VendorPayment");

            migrationBuilder.DropColumn(
                name: "BusinessPartnerName",
                table: "VendorPayment");

            migrationBuilder.DropColumn(
                name: "BusinessPartnerRoleId",
                table: "VendorPayment");

            migrationBuilder.DropColumn(
                name: "BusinessPartnerTaxIdentificationNumber",
                table: "VendorPayment");

            migrationBuilder.DropColumn(
                name: "BusinessPartnerApProfileVersionId",
                table: "SupplierDebitNotes");

            migrationBuilder.DropColumn(
                name: "BusinessPartnerCode",
                table: "SupplierDebitNotes");

            migrationBuilder.DropColumn(
                name: "BusinessPartnerLegalName",
                table: "SupplierDebitNotes");

            migrationBuilder.DropColumn(
                name: "BusinessPartnerName",
                table: "SupplierDebitNotes");

            migrationBuilder.DropColumn(
                name: "BusinessPartnerTaxIdentificationNumber",
                table: "SupplierDebitNotes");

            migrationBuilder.RenameColumn(
                name: "BusinessPartnerId",
                table: "VendorPayment",
                newName: "SupplierId");

            migrationBuilder.RenameIndex(
                name: "IX_VendorPayment_BusinessPartnerId",
                table: "VendorPayment",
                newName: "IX_VendorPayment_SupplierId");

            migrationBuilder.RenameColumn(
                name: "BusinessPartnerRoleId",
                table: "SupplierDebitNotes",
                newName: "SupplierId");

            migrationBuilder.RenameIndex(
                name: "IX_SupplierDebitNotes_BusinessPartnerRoleId",
                table: "SupplierDebitNotes",
                newName: "IX_SupplierDebitNotes_SupplierId");

            migrationBuilder.AddForeignKey(
                name: "FK_SupplierDebitNotes_Suppliers_SupplierId",
                table: "SupplierDebitNotes",
                column: "SupplierId",
                principalTable: "Suppliers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_VendorPayment_Suppliers_SupplierId",
                table: "VendorPayment",
                column: "SupplierId",
                principalTable: "Suppliers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
