using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261007190000_AllowExistingCustomerPropertyProspectsWithoutLead")]
public sealed class AllowExistingCustomerPropertyProspectsWithoutLead : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_EhcPropertyEnquiryProspects_LeadId",
            table: "EhcPropertyEnquiryProspects");

        migrationBuilder.DropIndex(
            name: "IX_EhcPropertyEnquiryProspects_TenantId_LeadId",
            table: "EhcPropertyEnquiryProspects");

        migrationBuilder.DropIndex(
            name: "IX_EhcProspectDepositReceipts_LeadId",
            table: "EhcProspectDepositReceipts");

        migrationBuilder.AlterColumn<Guid>(
            name: "LeadId",
            table: "EhcPropertyEnquiryProspects",
            type: "uniqueidentifier",
            nullable: true,
            oldClrType: typeof(Guid),
            oldType: "uniqueidentifier");

        migrationBuilder.AlterColumn<Guid>(
            name: "LeadId",
            table: "EhcProspectDepositReceipts",
            type: "uniqueidentifier",
            nullable: true,
            oldClrType: typeof(Guid),
            oldType: "uniqueidentifier");

        migrationBuilder.CreateIndex(
            name: "IX_EhcPropertyEnquiryProspects_LeadId",
            table: "EhcPropertyEnquiryProspects",
            column: "LeadId");

        migrationBuilder.CreateIndex(
            name: "IX_EhcPropertyEnquiryProspects_TenantId_LeadId",
            table: "EhcPropertyEnquiryProspects",
            columns: new[] { "TenantId", "LeadId" },
            unique: true,
            filter: "[LeadId] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_EhcProspectDepositReceipts_LeadId",
            table: "EhcProspectDepositReceipts",
            column: "LeadId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF EXISTS (SELECT 1 FROM [EhcPropertyEnquiryProspects] WHERE [LeadId] IS NULL)
                OR EXISTS (SELECT 1 FROM [EhcProspectDepositReceipts] WHERE [LeadId] IS NULL)
            BEGIN
                THROW 51066, 'Cannot restore mandatory property prospect LeadId while customer-only prospect records exist.', 1;
            END
            """);

        migrationBuilder.DropIndex(
            name: "IX_EhcPropertyEnquiryProspects_LeadId",
            table: "EhcPropertyEnquiryProspects");

        migrationBuilder.DropIndex(
            name: "IX_EhcPropertyEnquiryProspects_TenantId_LeadId",
            table: "EhcPropertyEnquiryProspects");

        migrationBuilder.DropIndex(
            name: "IX_EhcProspectDepositReceipts_LeadId",
            table: "EhcProspectDepositReceipts");

        migrationBuilder.AlterColumn<Guid>(
            name: "LeadId",
            table: "EhcPropertyEnquiryProspects",
            type: "uniqueidentifier",
            nullable: false,
            defaultValue: Guid.Empty,
            oldClrType: typeof(Guid),
            oldType: "uniqueidentifier",
            oldNullable: true);

        migrationBuilder.AlterColumn<Guid>(
            name: "LeadId",
            table: "EhcProspectDepositReceipts",
            type: "uniqueidentifier",
            nullable: false,
            defaultValue: Guid.Empty,
            oldClrType: typeof(Guid),
            oldType: "uniqueidentifier",
            oldNullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_EhcPropertyEnquiryProspects_LeadId",
            table: "EhcPropertyEnquiryProspects",
            column: "LeadId");

        migrationBuilder.CreateIndex(
            name: "IX_EhcPropertyEnquiryProspects_TenantId_LeadId",
            table: "EhcPropertyEnquiryProspects",
            columns: new[] { "TenantId", "LeadId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_EhcProspectDepositReceipts_LeadId",
            table: "EhcProspectDepositReceipts",
            column: "LeadId");
    }
}
