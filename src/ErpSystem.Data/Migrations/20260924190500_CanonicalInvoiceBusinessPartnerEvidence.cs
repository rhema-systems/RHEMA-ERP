using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260924190500_CanonicalInvoiceBusinessPartnerEvidence")]
public sealed class CanonicalInvoiceBusinessPartnerEvidence : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF EXISTS (SELECT 1 FROM [Invoices])
BEGIN
    THROW 51000, 'Canonical AR invoice cutover requires a fresh Finance transactional database. Reset Finance transactions before applying this migration; existing invoices cannot supply governed Customer role/profile evidence safely.', 1;
END
""");

        migrationBuilder.AddColumn<Guid>(
            name: "BusinessPartnerArProfileVersionId",
            table: "Invoices",
            type: "uniqueidentifier",
            nullable: false,
            defaultValue: Guid.Empty);

        migrationBuilder.AddColumn<string>(
            name: "BusinessPartnerCode",
            table: "Invoices",
            type: "nvarchar(50)",
            maxLength: 50,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "BusinessPartnerLegalName",
            table: "Invoices",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "BusinessPartnerRoleId",
            table: "Invoices",
            type: "uniqueidentifier",
            nullable: false,
            defaultValue: Guid.Empty);

        migrationBuilder.AddColumn<string>(
            name: "BusinessPartnerTin",
            table: "Invoices",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Invoices_BusinessPartnerArProfileVersionId",
            table: "Invoices",
            column: "BusinessPartnerArProfileVersionId");

        migrationBuilder.CreateIndex(
            name: "IX_Invoices_BusinessPartnerRoleId",
            table: "Invoices",
            column: "BusinessPartnerRoleId");

        migrationBuilder.AddForeignKey(
            name: "FK_Invoices_BusinessPartnerArProfileVersions_BusinessPartnerArProfileVersionId",
            table: "Invoices",
            column: "BusinessPartnerArProfileVersionId",
            principalTable: "BusinessPartnerArProfileVersions",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_Invoices_BusinessPartnerRoles_BusinessPartnerRoleId",
            table: "Invoices",
            column: "BusinessPartnerRoleId",
            principalTable: "BusinessPartnerRoles",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Invoices_BusinessPartnerArProfileVersions_BusinessPartnerArProfileVersionId",
            table: "Invoices");

        migrationBuilder.DropForeignKey(
            name: "FK_Invoices_BusinessPartnerRoles_BusinessPartnerRoleId",
            table: "Invoices");

        migrationBuilder.DropIndex(
            name: "IX_Invoices_BusinessPartnerArProfileVersionId",
            table: "Invoices");

        migrationBuilder.DropIndex(
            name: "IX_Invoices_BusinessPartnerRoleId",
            table: "Invoices");

        migrationBuilder.DropColumn(
            name: "BusinessPartnerArProfileVersionId",
            table: "Invoices");

        migrationBuilder.DropColumn(
            name: "BusinessPartnerCode",
            table: "Invoices");

        migrationBuilder.DropColumn(
            name: "BusinessPartnerLegalName",
            table: "Invoices");

        migrationBuilder.DropColumn(
            name: "BusinessPartnerRoleId",
            table: "Invoices");

        migrationBuilder.DropColumn(
            name: "BusinessPartnerTin",
            table: "Invoices");
    }
}
