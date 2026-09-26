using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class CanonicalSubledgerAdjustmentBusinessPartnerIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF EXISTS (SELECT 1 FROM [dbo].[SubledgerAdjustmentJournals])
                    THROW 51000, 'Canonical subledger adjustment identity requires the approved Finance transaction reset before migration. Existing CustomerId or SupplierId values cannot be reinterpreted as Business Partner profile identifiers.', 1;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_SubledgerAdjustmentJournals_BusinessPartners_CustomerId",
                table: "SubledgerAdjustmentJournals");

            migrationBuilder.DropForeignKey(
                name: "FK_SubledgerAdjustmentJournals_Suppliers_SupplierId",
                table: "SubledgerAdjustmentJournals");

            migrationBuilder.DropIndex(
                name: "IX_SubledgerAdjustmentJournals_CustomerId",
                table: "SubledgerAdjustmentJournals");

            migrationBuilder.DropIndex(
                name: "IX_SubledgerAdjustmentJournals_SupplierId",
                table: "SubledgerAdjustmentJournals");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "SubledgerAdjustmentJournals");

            migrationBuilder.DropColumn(
                name: "SupplierId",
                table: "SubledgerAdjustmentJournals");

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessPartnerApProfileVersionId",
                table: "SubledgerAdjustmentJournals",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessPartnerArProfileVersionId",
                table: "SubledgerAdjustmentJournals",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BusinessPartnerCode",
                table: "SubledgerAdjustmentJournals",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessPartnerId",
                table: "SubledgerAdjustmentJournals",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "BusinessPartnerLegalName",
                table: "SubledgerAdjustmentJournals",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BusinessPartnerName",
                table: "SubledgerAdjustmentJournals",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessPartnerRoleId",
                table: "SubledgerAdjustmentJournals",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "BusinessPartnerTaxIdentificationNumber",
                table: "SubledgerAdjustmentJournals",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubledgerAdjustmentJournals_BusinessPartnerApProfileVersionId",
                table: "SubledgerAdjustmentJournals",
                column: "BusinessPartnerApProfileVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_SubledgerAdjustmentJournals_BusinessPartnerArProfileVersionId",
                table: "SubledgerAdjustmentJournals",
                column: "BusinessPartnerArProfileVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_SubledgerAdjustmentJournals_BusinessPartnerId",
                table: "SubledgerAdjustmentJournals",
                column: "BusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_SubledgerAdjustmentJournals_BusinessPartnerRoleId",
                table: "SubledgerAdjustmentJournals",
                column: "BusinessPartnerRoleId");

            migrationBuilder.AddForeignKey(
                name: "FK_SubledgerAdjustmentJournals_BusinessPartnerApProfileVersions_BusinessPartnerApProfileVersionId",
                table: "SubledgerAdjustmentJournals",
                column: "BusinessPartnerApProfileVersionId",
                principalTable: "BusinessPartnerApProfileVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SubledgerAdjustmentJournals_BusinessPartnerArProfileVersions_BusinessPartnerArProfileVersionId",
                table: "SubledgerAdjustmentJournals",
                column: "BusinessPartnerArProfileVersionId",
                principalTable: "BusinessPartnerArProfileVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SubledgerAdjustmentJournals_BusinessPartnerRoles_BusinessPartnerRoleId",
                table: "SubledgerAdjustmentJournals",
                column: "BusinessPartnerRoleId",
                principalTable: "BusinessPartnerRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SubledgerAdjustmentJournals_BusinessPartners_BusinessPartnerId",
                table: "SubledgerAdjustmentJournals",
                column: "BusinessPartnerId",
                principalTable: "BusinessPartners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SubledgerAdjustmentJournals_BusinessPartnerApProfileVersions_BusinessPartnerApProfileVersionId",
                table: "SubledgerAdjustmentJournals");

            migrationBuilder.DropForeignKey(
                name: "FK_SubledgerAdjustmentJournals_BusinessPartnerArProfileVersions_BusinessPartnerArProfileVersionId",
                table: "SubledgerAdjustmentJournals");

            migrationBuilder.DropForeignKey(
                name: "FK_SubledgerAdjustmentJournals_BusinessPartnerRoles_BusinessPartnerRoleId",
                table: "SubledgerAdjustmentJournals");

            migrationBuilder.DropForeignKey(
                name: "FK_SubledgerAdjustmentJournals_BusinessPartners_BusinessPartnerId",
                table: "SubledgerAdjustmentJournals");

            migrationBuilder.DropIndex(
                name: "IX_SubledgerAdjustmentJournals_BusinessPartnerApProfileVersionId",
                table: "SubledgerAdjustmentJournals");

            migrationBuilder.DropIndex(
                name: "IX_SubledgerAdjustmentJournals_BusinessPartnerArProfileVersionId",
                table: "SubledgerAdjustmentJournals");

            migrationBuilder.DropIndex(
                name: "IX_SubledgerAdjustmentJournals_BusinessPartnerId",
                table: "SubledgerAdjustmentJournals");

            migrationBuilder.DropIndex(
                name: "IX_SubledgerAdjustmentJournals_BusinessPartnerRoleId",
                table: "SubledgerAdjustmentJournals");

            migrationBuilder.DropColumn(
                name: "BusinessPartnerApProfileVersionId",
                table: "SubledgerAdjustmentJournals");

            migrationBuilder.DropColumn(
                name: "BusinessPartnerArProfileVersionId",
                table: "SubledgerAdjustmentJournals");

            migrationBuilder.DropColumn(
                name: "BusinessPartnerCode",
                table: "SubledgerAdjustmentJournals");

            migrationBuilder.DropColumn(
                name: "BusinessPartnerId",
                table: "SubledgerAdjustmentJournals");

            migrationBuilder.DropColumn(
                name: "BusinessPartnerLegalName",
                table: "SubledgerAdjustmentJournals");

            migrationBuilder.DropColumn(
                name: "BusinessPartnerName",
                table: "SubledgerAdjustmentJournals");

            migrationBuilder.DropColumn(
                name: "BusinessPartnerRoleId",
                table: "SubledgerAdjustmentJournals");

            migrationBuilder.DropColumn(
                name: "BusinessPartnerTaxIdentificationNumber",
                table: "SubledgerAdjustmentJournals");

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                table: "SubledgerAdjustmentJournals",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SupplierId",
                table: "SubledgerAdjustmentJournals",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubledgerAdjustmentJournals_CustomerId",
                table: "SubledgerAdjustmentJournals",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_SubledgerAdjustmentJournals_SupplierId",
                table: "SubledgerAdjustmentJournals",
                column: "SupplierId");

            migrationBuilder.AddForeignKey(
                name: "FK_SubledgerAdjustmentJournals_BusinessPartners_CustomerId",
                table: "SubledgerAdjustmentJournals",
                column: "CustomerId",
                principalTable: "BusinessPartners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SubledgerAdjustmentJournals_Suppliers_SupplierId",
                table: "SubledgerAdjustmentJournals",
                column: "SupplierId",
                principalTable: "Suppliers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
