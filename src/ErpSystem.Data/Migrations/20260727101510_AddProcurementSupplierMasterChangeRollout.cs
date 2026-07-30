using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProcurementSupplierMasterChangeRollout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BeneficialOwnershipJson",
                table: "BusinessPartners",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ComplianceNotes",
                table: "BusinessPartners",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ComplianceReviewDateUtc",
                table: "BusinessPartners",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ComplianceStatus",
                table: "BusinessPartners",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ComplianceValidUntilUtc",
                table: "BusinessPartners",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OwnershipVerifiedAtUtc",
                table: "BusinessPartners",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_BusinessPartners_BeneficialOwnershipJson",
                table: "BusinessPartners",
                sql: "[BeneficialOwnershipJson] IS NULL OR ISJSON([BeneficialOwnershipJson]) = 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BusinessPartners_BlacklistEvidence",
                table: "BusinessPartners",
                sql: "[IsBlacklisted] = 0 OR (NULLIF(LTRIM(RTRIM([BlacklistReason])), '') IS NOT NULL AND [BlacklistDate] IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BusinessPartners_CompliancePeriod",
                table: "BusinessPartners",
                sql: "[ComplianceReviewDateUtc] IS NULL OR [ComplianceValidUntilUtc] IS NULL OR [ComplianceValidUntilUtc] >= [ComplianceReviewDateUtc]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_BusinessPartners_BeneficialOwnershipJson",
                table: "BusinessPartners");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BusinessPartners_BlacklistEvidence",
                table: "BusinessPartners");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BusinessPartners_CompliancePeriod",
                table: "BusinessPartners");

            migrationBuilder.DropColumn(
                name: "BeneficialOwnershipJson",
                table: "BusinessPartners");

            migrationBuilder.DropColumn(
                name: "ComplianceNotes",
                table: "BusinessPartners");

            migrationBuilder.DropColumn(
                name: "ComplianceReviewDateUtc",
                table: "BusinessPartners");

            migrationBuilder.DropColumn(
                name: "ComplianceStatus",
                table: "BusinessPartners");

            migrationBuilder.DropColumn(
                name: "ComplianceValidUntilUtc",
                table: "BusinessPartners");

            migrationBuilder.DropColumn(
                name: "OwnershipVerifiedAtUtc",
                table: "BusinessPartners");
        }
    }
}
